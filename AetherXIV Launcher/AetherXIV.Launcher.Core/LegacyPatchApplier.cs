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

using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace AetherXIV.Launcher.Core;

public sealed record PatchApplyProgress(
    int CurrentPatch,
    int TotalPatches,
    string PatchFileName,
    string Message,
    long BytesProcessed,
    long TotalBytes,
    bool LogMessage);

public sealed record PatchApplyResult(int AppliedPatchCount, IReadOnlyList<string> Messages)
{
    public bool Succeeded => AppliedPatchCount >= 0; // Zero means already up to date.
}

public static class LegacyPatchApplier
{
    private static readonly byte[] PatchMagic =
    [
        0x91,
        (byte)'Z',
        (byte)'I',
        (byte)'P',
        (byte)'A',
        (byte)'T',
        (byte)'C',
        (byte)'H',
        0x0D,
        0x0A,
        0x1A,
        0x0A
    ];

    public static PatchApplyResult ApplyPatchChain(
        ClientInstall clientInstall,
        PatchLibraryReport patchLibraryReport,
        IProgress<PatchApplyProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientInstall.RootPath) || !Directory.Exists(clientInstall.RootPath))
            throw new InvalidOperationException("Client root does not exist.");

        cancellationToken.ThrowIfCancellationRequested();
        EnsureClientRootWritable(clientInstall.RootPath);
        string clientRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(clientInstall.RootPath));
        using FileStream patchLock = new(Path.Combine(clientRoot, ".aetherxiv-patch.lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        PatchTransaction.Recover(clientRoot);
        IReadOnlyList<PatchEntry> pending = GetPendingPatches(clientInstall.Inspect().Version);
        List<string> messages = new();
        if (pending.Count == 0)
            return new PatchApplyResult(0, ["Client already reports the target versions."]);

        if (patchLibraryReport.InspectionMode != PatchLibraryInspectionMode.Checksum)
            throw new InvalidOperationException("Patch library must be verified by checksum before applying patches.");

        // Select from the canonical manifest, never the order supplied by a report.
        List<PatchFileReport> patches = new();
        foreach (PatchEntry entry in pending)
        {
            PatchFileReport[] matches = patchLibraryReport.FileReports.Where(report => report.Entry == entry).ToArray();
            if (matches.Length != 1 || !matches[0].IsPatchValid(PatchLibraryInspectionMode.Checksum))
                throw new InvalidOperationException($"Required patch is missing or invalid: {entry.PatchFileName}.");
            patches.Add(matches[0]);
        }
        long totalBytes = patches.Sum(report => new FileInfo(report.PatchPath).Length);
        long completedBytes = 0;

        for (int index = 0; index < patches.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PatchFileReport patch = patches[index];
            long patchLength = new FileInfo(patch.PatchPath).Length;
            PatchApplyContext context = new(
                index + 1,
                patches.Count,
                patch.Entry.PatchFileName,
                completedBytes,
                totalBytes,
                patchLength);

            ReportProgress(
                progress,
                context,
                0,
                $"Starting patch {index + 1}/{patches.Count}: {patch.Entry.PatchFileName}",
                true);

            try
            {
                // A report can outlive the files it verified. Recheck before changing the client.
                if (new FileInfo(patch.PatchPath).Length != patch.Entry.ExpectedSizeBytes
                    || Crc32.ComputeFile(patch.PatchPath, cancellationToken) != patch.Entry.ExpectedCrc32)
                    throw new InvalidDataException("Patch changed since library verification.");
                string versionFile = patch.Entry.Repository == PatchRepository.Boot ? "boot.ver" : "game.ver";
                ApplyTransactional(clientRoot, patch.PatchPath, messages, progress, context, cancellationToken,
                    transaction => WriteVersion(clientRoot, versionFile, patch.Entry.ToVersion, transaction));
                messages.Add($"Wrote {versionFile} {patch.Entry.ToVersion}.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException(
                    $"Failed to apply patch {index + 1}/{patches.Count} {patch.Entry.PatchFileName}: {ex.Message}",
                    ex);
            }

            completedBytes += patchLength;

            ReportProgress(
                progress,
                context,
                patchLength,
                $"Finished patch {index + 1}/{patches.Count}: {patch.Entry.PatchFileName}",
                true);
        }

        progress?.Report(new PatchApplyProgress(
            patches.Count,
            patches.Count,
            patches.Count == 0 ? "" : patches[^1].Entry.PatchFileName,
            "Patch chain complete.",
            totalBytes,
            totalBytes,
            true));

        return new PatchApplyResult(patches.Count, messages);
    }

    public static IReadOnlyList<PatchEntry> GetPendingPatches(ClientVersionInfo version)
    {
        List<PatchEntry> pending = new();
        foreach (PatchRepository repository in new[] { PatchRepository.Boot, PatchRepository.Game })
        {
            PatchEntry[] chain = LegacyPatchManifest.Entries.Where(entry => entry.Repository == repository).ToArray();
            string? installed = repository == PatchRepository.Boot ? version.BootVersion : version.GameVersion;
            if (installed == chain[^1].ToVersion)
                continue;
            int start = Array.FindIndex(chain, entry => entry.FromVersion == installed);
            if (start < 0)
                throw new InvalidOperationException($"Unknown {repository} client version '{installed ?? "missing"}'. Select a supported 1.x installation before patching.");
            pending.AddRange(chain[start..]);
        }
        return pending;
    }

    private static void WriteVersion(string clientRoot, string name, string version, PatchTransaction transaction)
    {
        string path = ResolvePatchPath(clientRoot, name);
        string temporaryPath = transaction.CreateTemporaryOutputPath();
        try
        {
            File.WriteAllText(temporaryPath, version, Encoding.ASCII);
            transaction.ReplaceFile(temporaryPath, path);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public static void EnsureClientRootWritable(string clientRoot)
    {
        if (string.IsNullOrWhiteSpace(clientRoot) || !Directory.Exists(clientRoot))
            throw new InvalidOperationException("Client root does not exist.");

        string normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(clientRoot));
        string probePath = Path.Combine(normalizedRoot, $".aetherxiv-write-test-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(probePath, [0x45, 0x47]);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException(
                "Client folder is not writable. On Windows, patching a client under Program Files usually requires running AetherXIV Launcher as administrator or moving/copying the client to a writable folder.",
                ex);
        }
        catch (IOException ex)
        {
            throw new IOException($"Client folder write test failed: {ex.Message}", ex);
        }
        finally
        {
            try
            {
                if (File.Exists(probePath))
                    File.Delete(probePath);
            }
            catch
            {
                // A failed cleanup is non-fatal; the patcher has proven write access.
            }
        }
    }

    public static void ApplyPatchFile(
        string clientRoot,
        string patchPath,
        IList<string>? messages = null,
        IProgress<PatchApplyProgress>? progress = null,
        PatchApplyContext? context = null,
        CancellationToken cancellationToken = default)
    {
        string normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(clientRoot));
        EnsureClientRootWritable(normalizedRoot);
        using FileStream patchLock = new(Path.Combine(normalizedRoot, ".aetherxiv-patch.lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        PatchTransaction.Recover(normalizedRoot);
        ApplyTransactional(normalizedRoot, patchPath, messages, progress, context, cancellationToken);
    }

    private static void ApplyTransactional(string root, string patchPath, IList<string>? messages,
        IProgress<PatchApplyProgress>? progress, PatchApplyContext? context, CancellationToken cancellationToken,
        Action<PatchTransaction>? finish = null)
    {
        PatchTransaction.Recover(root);
        PatchTransaction transaction = new(root);
        Log(root, $"Starting {Path.GetFileName(patchPath)} on {System.Runtime.InteropServices.RuntimeInformation.OSDescription}.");
        try
        {
            ApplyPatchContents(root, patchPath, transaction, messages, progress, context, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            finish?.Invoke(transaction);
            transaction.Commit();
            Log(root, $"Committed {Path.GetFileName(patchPath)}.");
        }
        catch (Exception error)
        {
            Log(root, $"Failed {Path.GetFileName(patchPath)}: {error}");
            try
            {
                PatchTransaction.Recover(root);
                Log(root, "Restored files changed by the failed patch.");
            }
            catch (Exception recoveryError)
            {
                Log(root, $"Recovery incomplete: {recoveryError}");
                throw new AggregateException("Patch failed and recovery could not finish. Close the game and retry; recovery backups have been retained.", error, recoveryError);
            }
            throw;
        }
    }

    private static void Log(string root, string message)
    {
        try { File.AppendAllText(Path.Combine(root, ".aetherxiv-patch.log"), $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}"); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void ApplyPatchContents(
        string clientRoot, string patchPath, PatchTransaction transaction,
        IList<string>? messages, IProgress<PatchApplyProgress>? progress,
        PatchApplyContext? context, CancellationToken cancellationToken)
    {
        string normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(clientRoot));
        using FileStream stream = File.OpenRead(patchPath);
        PatchApplyContext localContext = context ?? new(
            1,
            1,
            Path.GetFileName(patchPath),
            0,
            stream.Length,
            stream.Length);

        byte[] header = ReadExact(stream, PatchMagic.Length);
        if (!header.SequenceEqual(PatchMagic))
            throw new InvalidDataException("Invalid ZiPatch header.");

        bool hasFileHeader = false;
        uint expectedEntries = 0, expectedAddedDirectories = 0, expectedDeletedDirectories = 0;
        uint entries = 0, addedDirectories = 0, deletedDirectories = 0;
        while (stream.Position < stream.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            byte[] chunkSizeData = ReadExactOrEnd(stream, 4);
            if (chunkSizeData.Length == 0)
                break;

            if (chunkSizeData.Length != 4)
                throw new EndOfStreamException("Unexpected end of patch chunk size.");

            uint chunkSize = BinaryPrimitives.ReadUInt32BigEndian(chunkSizeData);
            byte[] commandBytes = ReadExact(stream, 4);
            string command = Encoding.ASCII.GetString(commandBytes);
            if (!hasFileHeader && command != "FHDR")
                throw new InvalidDataException("ZiPatch file header must be the first chunk.");
            if (chunkSize > stream.Length - stream.Position - 4)
                throw new EndOfStreamException("Patch chunk extends beyond the end of the archive.");
            using LimitedReadStream chunkBody = new(stream, chunkSize,
                initialCrc: Crc32.Update(0xFFFFFFFFu, commandBytes));
            switch (command)
            {
                case "FHDR":
                    if (hasFileHeader || chunkSize != 20 || ReadUInt32BigEndian(chunkBody) != 0x0200)
                        throw new InvalidDataException("Unsupported or duplicate ZiPatch file header.");
                    string patchType = Encoding.ASCII.GetString(ReadExact(chunkBody, 4));
                    if (patchType is not ("HIST" or "DIFF"))
                        throw new InvalidDataException($"Unsupported ZiPatch type '{patchType}'.");
                    expectedEntries = ReadUInt32BigEndian(chunkBody);
                    expectedAddedDirectories = ReadUInt32BigEndian(chunkBody);
                    expectedDeletedDirectories = ReadUInt32BigEndian(chunkBody);
                    hasFileHeader = true;
                    break;
                case "APLY":
                    if (chunkSize != 12)
                        throw new InvalidDataException("Invalid ZiPatch apply options.");
                    break;
                case "ADIR":
                    addedDirectories++;
                    ExecuteDirectoryCreate(chunkBody, normalizedRoot, transaction, messages, progress, localContext);
                    break;
                case "DLED":
                case "DELD":
                    deletedDirectories++;
                    ExecuteDirectoryDelete(chunkBody, normalizedRoot, transaction, messages, progress, localContext);
                    break;
                case "ETRY":
                    entries++;
                    ExecuteFileEntry(chunkBody, normalizedRoot, transaction, messages, progress, localContext, cancellationToken);
                    break;
                default:
                    throw new InvalidDataException($"Unhandled ZiPatch command '{command}'.");
            }

            chunkBody.SkipRemaining();
            if (ReadUInt32BigEndian(stream) != chunkBody.Checksum)
                throw new InvalidDataException($"ZiPatch checksum mismatch in {command} at offset {stream.Position}.");

            ReportProgress(
                progress,
                localContext,
                stream.Position,
                $"Reading {localContext.PatchFileName}: {FormatByteCount(stream.Position)}/{FormatByteCount(stream.Length)}",
                false);
        }
        if (!hasFileHeader || entries != expectedEntries
            || addedDirectories != expectedAddedDirectories || deletedDirectories != expectedDeletedDirectories)
            throw new InvalidDataException("ZiPatch archive is incomplete: chunk counts differ from its file header.");
    }

    private static void ExecuteDirectoryCreate(
        Stream stream,
        string clientRoot,
        PatchTransaction transaction,
        IList<string>? messages,
        IProgress<PatchApplyProgress>? progress,
        PatchApplyContext context)
    {
        string directoryPath = ReadPatchPath(stream, clientRoot);
        ReportProgress(progress, context, stream.Position, $"Create directory {ToDisplayPath(clientRoot, directoryPath)}", false);

        if (Directory.Exists(directoryPath))
        {
            messages?.Add($"Directory already exists: {directoryPath}");
            return;
        }

        transaction.CreateDirectory(directoryPath);
    }

    private static void ExecuteDirectoryDelete(
        Stream stream,
        string clientRoot,
        PatchTransaction transaction,
        IList<string>? messages,
        IProgress<PatchApplyProgress>? progress,
        PatchApplyContext context)
    {
        string directoryPath = ReadPatchPath(stream, clientRoot);
        ReportProgress(progress, context, stream.Position, $"Delete directory {ToDisplayPath(clientRoot, directoryPath)}", false);

        if (!Directory.Exists(directoryPath))
        {
            messages?.Add($"Directory not found for deletion: {directoryPath}");
            return;
        }

        transaction.DeleteDirectory(directoryPath);
    }

    private static void ExecuteFileEntry(
        Stream input,
        string clientRoot,
        PatchTransaction transaction,
        IList<string>? messages,
        IProgress<PatchApplyProgress>? progress,
        PatchApplyContext context,
        CancellationToken cancellationToken)
    {
        string filePath = ReadPatchPath(input, clientRoot);
        string displayPath = ToDisplayPath(clientRoot, filePath);

        try
        {
            uint itemCount = ReadUInt32BigEndian(input);
            if (itemCount == 0)
                throw new InvalidDataException($"File entry has no items: {displayPath}.");
            uint? expectedFileSize = null;
            byte[]? expectedHash = null;
            byte finalEntryMode = 0;
            bool wrotePayload = false;

            ReportProgress(
                progress,
                context,
                input.Position,
                $"Applying {displayPath} ({itemCount} chunk{(itemCount == 1 ? "" : "s")})",
                false);

            for (uint index = 0; index < itemCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                byte entryMode = ReadFourByteMode(input, "entry mode");
                if (entryMode is not (0x41 or 0x44 or 0x4D))
                    throw new InvalidDataException($"Unknown ZiPatch entry mode 0x{entryMode:X2}.");

                ReadExact(input, 0x14); // Historical source hash; payloads are complete replacement files.
                byte[] destinationHash = ReadExact(input, 0x14);

                byte compressionMode = ReadFourByteMode(input, "compression mode");
                if (compressionMode is not (0x4E or 0x5A))
                    throw new InvalidDataException($"Unknown ZiPatch compression mode 0x{compressionMode:X2}.");
                uint compressedSize = ReadUInt32BigEndian(input);
                ReadUInt32BigEndian(input); // Historical source size.
                uint newFileSize = ReadUInt32BigEndian(input);

                if (index == itemCount - 1)
                {
                    finalEntryMode = entryMode;
                    expectedFileSize = newFileSize;
                    expectedHash = destinationHash;
                }

                if (index != itemCount - 1 && compressedSize != 0)
                    throw new InvalidDataException($"Non-final ZiPatch entry contains file data: {displayPath}.");

                if (compressedSize == 0)
                    continue;

                transaction.CreateDirectory(Path.GetDirectoryName(filePath)!);
                string temporaryPath = transaction.CreateTemporaryOutputPath();
                try
                {
                    using (FileStream output = File.Create(temporaryPath))
                    {
                        if (compressionMode == 0x4E)
                        {
                            using LimitedReadStream limitedInput = new(
                                input,
                                compressedSize,
                                bytesRead => ReportProgress(
                                    progress,
                                    context,
                                    input.Position,
                                    $"Writing {displayPath}: {FormatByteCount(bytesRead)}/{FormatByteCount(compressedSize)} raw chunk {index + 1}/{itemCount}",
                                    false));
                            CopyToWithCancellation(limitedInput, output, newFileSize, cancellationToken);
                            limitedInput.SkipRemaining();
                        }
                        else if (compressionMode == 0x5A)
                        {
                            using LimitedReadStream limitedInput = new(
                                input,
                                compressedSize,
                                bytesRead => ReportProgress(
                                    progress,
                                    context,
                                    input.Position,
                                    $"Writing {displayPath}: {FormatByteCount(bytesRead)}/{FormatByteCount(compressedSize)} compressed chunk {index + 1}/{itemCount}",
                                    false));
                            using ZLibStream zlib = new(limitedInput, CompressionMode.Decompress);
                            CopyToWithCancellation(zlib, output, newFileSize, cancellationToken);
                            limitedInput.SkipRemaining();
                        }
                        else
                        {
                            throw new InvalidDataException($"Unknown ZiPatch compression mode 0x{compressionMode:X2}.");
                        }
                    }

                    ValidatePatchedFile(temporaryPath, displayPath, newFileSize, destinationHash);
                    cancellationToken.ThrowIfCancellationRequested();
                    transaction.ReplaceFile(temporaryPath, filePath);
                    wrotePayload = true;
                }
                finally
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
            }

            // Mode 0x44 is the ZiPatch delete form when the terminal item has
            // no body and transitions a source file to size zero. Retail patch
            // D2010.09.19.0000 uses this shape for thousands of removals.
            bool deleteFile = finalEntryMode == 0x44
                && !wrotePayload
                && expectedFileSize == 0;
            if (deleteFile)
            {
                if (File.Exists(filePath))
                {
                    // HIST records describe past file states, not a required current
                    // source. Meteor deletes regardless of source hash. Preserve the
                    // original in the transaction rather than rejecting older clients.
                    transaction.DeleteFile(filePath);
                }
                messages?.Add($"Deleted file: {filePath}");
                return;
            }

            if (!wrotePayload && expectedFileSize == 0)
            {
                transaction.CreateDirectory(Path.GetDirectoryName(filePath)!);
                string temporaryPath = transaction.CreateTemporaryOutputPath();
                try
                {
                    File.WriteAllBytes(temporaryPath, []);
                    ValidatePatchedFile(temporaryPath, displayPath, 0, expectedHash!);
                    transaction.ReplaceFile(temporaryPath, filePath);
                }
                finally
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
            }
            if (expectedFileSize.HasValue && expectedHash is not null)
                ValidatePatchedFile(filePath, displayPath, expectedFileSize.Value, expectedHash);
        }
        catch (Exception error)
        {
            Log(clientRoot, $"File entry {displayPath}: {error.Message}");
            throw;
        }
    }

    private static string ReadPatchPath(Stream stream, string clientRoot)
    {
        uint pathSize = ReadUInt32BigEndian(stream);
        string relativePath = Encoding.UTF8.GetString(ReadExact(stream, checked((int)pathSize)));
        return ResolvePatchPath(clientRoot, relativePath);
    }

    internal static string ResolvePatchPath(string clientRoot, string relativePath)
    {
        string[] parts = relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts[0].StartsWith(".aetherxiv-", StringComparison.OrdinalIgnoreCase)
            || relativePath.StartsWith('/') || relativePath.StartsWith('\\')
            || parts.Any(part => part is "." or ".." || part.IndexOfAny([':', '\0', '*', '?', '<', '>', '|', '"']) >= 0 || part.EndsWith('.') || part.EndsWith(' ')))
            throw new InvalidDataException($"Invalid patch path: {relativePath}");

        // The archives describe Windows paths, even when applied on Linux or macOS.
        // Reuse existing casing so that an update does not create a second data tree.
        string path = clientRoot;
        foreach (string part in parts)
        {
            if (Directory.Exists(path))
            {
                string[] matches = Directory.EnumerateFileSystemEntries(path)
                    .Where(entry => string.Equals(Path.GetFileName(entry), part, StringComparison.OrdinalIgnoreCase))
                    .Take(2).ToArray();
                if (matches.Length > 1)
                    throw new InvalidDataException($"Ambiguous client path: {relativePath}");
                path = matches.Length == 1 ? matches[0] : Path.Combine(path, part);
            }
            else
            {
                path = Path.Combine(path, part);
            }
            // Never follow a client subdirectory or file link outside the selected tree.
            if (new FileInfo(path).LinkTarget is not null
                || (File.Exists(path) || Directory.Exists(path))
                    && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException($"Patch path contains a symbolic link or reparse point: {relativePath}");
        }
        return path;
    }

    private static void ValidatePatchedFile(
        string filePath,
        string displayPath,
        uint expectedFileSize,
        byte[] expectedHash)
    {
        long actualSize = new FileInfo(filePath).Length;
        if (actualSize != expectedFileSize)
            throw new InvalidDataException(
                $"Patched file size differs from manifest for {displayPath}: expected {expectedFileSize}, got {actualSize}.");

        if (!IsAllZero(expectedHash))
        {
            using FileStream verifyInput = File.OpenRead(filePath);
            byte[] actualHash = SHA1.HashData(verifyInput);
            if (!actualHash.SequenceEqual(expectedHash))
                throw new InvalidDataException($"Patched file hash differs from manifest for {displayPath}.");
        }
    }

    private static byte ReadFourByteMode(Stream stream, string label)
    {
        byte[] data = ReadExact(stream, 4);
        if (data[1] != 0 || data[2] != 0 || data[3] != 0)
            throw new InvalidDataException($"Invalid ZiPatch {label} bytes.");

        return data[0];
    }

    private static bool IsAllZero(byte[] data)
    {
        foreach (byte value in data)
        {
            if (value != 0)
                return false;
        }

        return true;
    }

    private static void SkipPayload(Stream stream, uint byteCount, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[128 * 1024];
        long remaining = byteCount;
        while (remaining > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
            if (read == 0)
                throw new EndOfStreamException("Patch payload ended early.");

            remaining -= read;
        }
    }

    private static void CopyToWithCancellation(Stream input, Stream output, uint expectedSize, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[128 * 1024];
        long written = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int read = input.Read(buffer, 0, buffer.Length);
            if (read == 0)
                return;

            written += read;
            if (written > expectedSize)
                throw new InvalidDataException("Patch payload exceeds its declared output size.");
            output.Write(buffer, 0, read);
        }
    }

    private static void ReportProgress(
        IProgress<PatchApplyProgress>? progress,
        PatchApplyContext context,
        long patchBytesProcessed,
        string message,
        bool logMessage)
    {
        if (progress is null)
            return;

        long boundedPatchBytes = Math.Clamp(patchBytesProcessed, 0, context.PatchLengthBytes);
        long totalBytesProcessed = Math.Clamp(context.CompletedBytesBeforePatch + boundedPatchBytes, 0, context.TotalBytes);
        if (!context.ShouldReport(totalBytesProcessed, logMessage))
            return;

        progress.Report(new PatchApplyProgress(
            context.CurrentPatch,
            context.TotalPatches,
            context.PatchFileName,
            message,
            totalBytesProcessed,
            context.TotalBytes,
            logMessage));
    }

    private static string ToDisplayPath(string clientRoot, string fullPath)
    {
        return Path.GetRelativePath(clientRoot, fullPath);
    }

    private static string FormatByteCount(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} {units[unit]}" : $"{value:0.0} {units[unit]}";
    }

    private static uint ReadUInt32BigEndian(Stream stream)
    {
        return BinaryPrimitives.ReadUInt32BigEndian(ReadExact(stream, 4));
    }

    private static uint ReadUInt32LittleEndian(Stream stream)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(ReadExact(stream, 4));
    }

    private static byte[] ReadExact(Stream stream, int length)
    {
        byte[] buffer = new byte[length];
        stream.ReadExactly(buffer);
        return buffer;
    }

    private static byte[] ReadExactOrEnd(Stream stream, int length)
    {
        byte[] buffer = new byte[length];
        int offset = 0;
        while (offset < length)
        {
            int read = stream.Read(buffer, offset, length - offset);
            if (read == 0)
            {
                if (offset == 0)
                    return [];

                return buffer[..offset];
            }

            offset += read;
        }

        return buffer;
    }
}

public sealed class PatchApplyContext
{
    private const long ByteReportThreshold = 8 * 1024 * 1024;
    private static readonly long TickReportThreshold = Stopwatch.Frequency / 4;

    private long lastReportedBytes = -1;
    private long lastReportedTicks;

    public PatchApplyContext(
        int currentPatch,
        int totalPatches,
        string patchFileName,
        long completedBytesBeforePatch,
        long totalBytes,
        long patchLengthBytes)
    {
        CurrentPatch = currentPatch;
        TotalPatches = totalPatches;
        PatchFileName = patchFileName;
        CompletedBytesBeforePatch = completedBytesBeforePatch;
        TotalBytes = totalBytes;
        PatchLengthBytes = patchLengthBytes;
        lastReportedTicks = Stopwatch.GetTimestamp();
    }

    public int CurrentPatch { get; }

    public int TotalPatches { get; }

    public string PatchFileName { get; }

    public long CompletedBytesBeforePatch { get; }

    public long TotalBytes { get; }

    public long PatchLengthBytes { get; }

    public bool ShouldReport(long totalBytesProcessed, bool logMessage)
    {
        if (logMessage)
        {
            lastReportedBytes = totalBytesProcessed;
            lastReportedTicks = Stopwatch.GetTimestamp();
            return true;
        }

        long now = Stopwatch.GetTimestamp();
        if (lastReportedBytes < 0
            || totalBytesProcessed >= TotalBytes
            || totalBytesProcessed - lastReportedBytes >= ByteReportThreshold
            || now - lastReportedTicks >= TickReportThreshold)
        {
            lastReportedBytes = totalBytesProcessed;
            lastReportedTicks = now;
            return true;
        }

        return false;
    }
}

internal sealed class LimitedReadStream : Stream
{
    private readonly Stream inner;
    private readonly Action<long>? onRead;
    private long remainingBytes;
    private long totalBytesRead;
    private uint? crc;

    public uint? Checksum => crc.HasValue ? ~crc.Value : null;

    public LimitedReadStream(Stream inner, long byteLimit, Action<long>? onRead = null, uint? initialCrc = null)
    {
        this.inner = inner;
        this.onRead = onRead;
        remainingBytes = byteLimit;
        crc = initialCrc;
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => inner.Position;
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (remainingBytes <= 0)
            return 0;

        int toRead = (int)Math.Min(count, remainingBytes);
        int read = inner.Read(buffer, offset, toRead);
        remainingBytes -= read;
        totalBytesRead += read;
        if (crc.HasValue)
            crc = Crc32.Update(crc.Value, buffer.AsSpan(offset, read));
        if (read > 0)
            onRead?.Invoke(totalBytesRead);

        return read;
    }

    public void SkipRemaining()
    {
        byte[] buffer = new byte[8192];
        while (remainingBytes > 0)
        {
            int read = Read(buffer, 0, (int)Math.Min(buffer.Length, remainingBytes));
            if (read == 0)
                throw new EndOfStreamException("Compressed patch payload ended early.");
        }
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
