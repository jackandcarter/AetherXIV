/*
 * AetherXIV
 * Copyright (C) 2026 Demi Dev Unit
 *
 * This file is part of AetherXIV.
 * See THIRD_PARTY_NOTICES.md for historical and third-party attribution.
 *
 * AetherXIV is free software: you may redistribute it and/or modify it
 * under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * SPDX-License-Identifier: AGPL-3.0-or-later
 */

using System.Text.Json;

namespace AetherXIV.Launcher.Core;

// One archive is a recovery unit. Journal records reach disk before the corresponding
// rename, and originals stay on the same volume until the archive and version commit.
internal sealed class PatchTransaction
{
    private const string JournalName = ".aetherxiv-patch-transaction";
    private readonly string root;
    private readonly string journal;
    private int sequence;
    private readonly CancellationToken cancellationToken;

    public PatchTransaction(string root, CancellationToken cancellationToken = default)
    {
        this.root = root;
        this.cancellationToken = cancellationToken;
        journal = Path.Combine(root, JournalName);
        Directory.CreateDirectory(journal);
    }

    public static void Recover(string root)
    {
        foreach (string completed in Directory.GetDirectories(root, JournalName + "-cleanup-*"))
            TryDeleteCompleted(completed);
        string journal = Path.Combine(root, JournalName);
        if (!Directory.Exists(journal))
            return;
        if ((File.GetAttributes(journal) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Patch recovery directory must not be a symbolic link.");
        if (!File.Exists(Path.Combine(journal, "committed")))
        {
            foreach (string recordPath in Directory.GetFiles(journal, "*.json").OrderDescending(StringComparer.Ordinal))
            {
                Record record = JsonSerializer.Deserialize<Record>(File.ReadAllText(recordPath))
                    ?? throw new InvalidDataException("Invalid patch recovery record.");
                if (record.Kind is not ("created-directory" or "created-file" or "file" or "directory"))
                    throw new InvalidDataException("Unknown patch recovery operation.");
                string target = LegacyPatchApplier.ResolvePatchPath(root, record.Path);
                string backup = Path.ChangeExtension(recordPath, ".original");
                if (record.Kind == "created-directory")
                {
                    if (Directory.Exists(target))
                        Directory.Delete(target, false);
                }
                else if (record.Kind == "created-file")
                {
                    if (File.Exists(target))
                        PatchFileOperations.RetrySharingViolation(() => File.Delete(target));
                }
                else if (record.Kind == "file" && File.Exists(backup))
                {
                    PatchFileOperations.RetrySharingViolation(() => File.Move(backup, target, true));
                }
                else if (record.Kind == "directory" && Directory.Exists(backup))
                {
                    if (Directory.Exists(target))
                        Directory.Delete(target, true);
                    Directory.Move(backup, target);
                }
                // A missing backup means either the original rename never happened,
                // or recovery already restored this record before an interruption.
                File.Delete(recordPath);
            }
        }
        if (File.Exists(Path.Combine(journal, "committed")))
            RetireCommittedJournal(journal);
        else
            Directory.Delete(journal, true);
    }

    private static void RetireCommittedJournal(string journal)
    {
        // Rename before recursive cleanup: an exit after deleting the commit marker
        // must never turn leftover committed backups into an active rollback journal.
        string completed = journal + "-cleanup-" + Guid.NewGuid().ToString("N");
        Directory.Move(journal, completed);
        TryDeleteCompleted(completed);
    }

    private static void TryDeleteCompleted(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Patch cleanup directory must not be a symbolic link.");
        try { Directory.Delete(path, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public string CreateTemporaryOutputPath() => Path.Combine(journal, $"payload-{Guid.NewGuid():N}.tmp");

    public void CreateDirectory(string path)
    {
        if (Directory.Exists(path))
            return;
        string parent = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(parent))
            CreateDirectory(parent);
        Save(path, "created-directory");
        Directory.CreateDirectory(path);
    }

    public void ReplaceFile(string temporaryPath, string path)
    {
        Preserve(path, directory: false);
        PatchFileOperations.RetrySharingViolation(() => File.Move(temporaryPath, path, true), cancellationToken);
    }

    public void DeleteFile(string path) => Preserve(path, directory: false);

    public void DeleteDirectory(string path) => Preserve(path, directory: true);

    private void Preserve(string path, bool directory)
    {
        bool exists = directory ? Directory.Exists(path) : File.Exists(path);
        string backup = Save(path, exists ? directory ? "directory" : "file" : "created-file");
        if (exists)
        {
            if (directory)
                Directory.Move(path, backup);
            else
                PatchFileOperations.RetrySharingViolation(() => File.Move(path, backup), cancellationToken);
        }
    }

    private string Save(string path, string kind)
    {
        string recordPath = Path.Combine(journal, $"{sequence++:D10}.json");
        // Publish only complete records so a process exit while writing cannot leave
        // an unreadable record for an operation that has not started.
        string temporary = recordPath + ".tmp";
        using (FileStream output = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(output, new Record(Path.GetRelativePath(root, path), kind));
            output.Flush(flushToDisk: true);
        }
        File.Move(temporary, recordPath);
        return Path.ChangeExtension(recordPath, ".original");
    }

    public void Commit()
    {
        using (FileStream marker = new(Path.Combine(journal, "committed"), FileMode.CreateNew))
            marker.Flush(flushToDisk: true);
        // A cleanup failure must never undo an already committed archive.
        try { RetireCommittedJournal(journal); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private sealed record Record(string Path, string Kind);
}
