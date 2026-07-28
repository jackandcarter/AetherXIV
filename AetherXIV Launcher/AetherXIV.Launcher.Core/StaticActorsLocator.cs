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

namespace AetherXIV.Launcher.Core;

public static class StaticActorsLocator
{
    public const string StaticActorsFileName = "rq9q1797qvs.san";
    public const string PreparedStaticActorsFileName = "staticactors.bin";

    private static readonly string[] SourceFileNames =
    {
        StaticActorsFileName,
        PreparedStaticActorsFileName
    };

    public static string? FindSource(string clientRootPath)
    {
        if (string.IsNullOrWhiteSpace(clientRootPath))
            return null;

        string root = Path.GetFullPath(clientRootPath);
        if (File.Exists(root) && SourceFileNames.Contains(Path.GetFileName(root), StringComparer.OrdinalIgnoreCase))
            return root;

        string[] candidates =
        {
            Path.Combine(root, "client", "script", StaticActorsFileName),
            Path.Combine(root, "script", StaticActorsFileName),
            Path.Combine(root, StaticActorsFileName),
            Path.Combine(root, "client", "script", PreparedStaticActorsFileName),
            Path.Combine(root, "script", PreparedStaticActorsFileName),
            Path.Combine(root, PreparedStaticActorsFileName)
        };

        return candidates.FirstOrDefault(File.Exists) ?? FindSourceRecursively(root);
    }

    public static bool TryFindSource(string clientRootPath, out string sourcePath)
    {
        sourcePath = FindSource(clientRootPath) ?? "";
        return sourcePath.Length > 0;
    }

    public static string PrepareStaticActors(string clientRootPath, string repositoryRoot)
    {
        string? sourcePath = FindSource(clientRootPath);
        if (sourcePath == null)
            throw new FileNotFoundException("Static actors source file was not found.", StaticActorsFileName);

        string outputPath = Path.Combine(Path.GetFullPath(repositoryRoot), "Data", "staticactors.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        File.Copy(sourcePath, outputPath, true);
        return outputPath;
    }

    private static string? FindSourceRecursively(string root)
    {
        if (!Directory.Exists(root))
            return null;

        Stack<string> pending = new();
        pending.Push(root);

        while (pending.Count > 0)
        {
            string directory = pending.Pop();
            foreach (string file in SafeEnumerateFiles(directory))
            {
                if (SourceFileNames.Contains(Path.GetFileName(file), StringComparer.OrdinalIgnoreCase))
                    return file;
            }

            foreach (string childDirectory in SafeEnumerateDirectories(directory))
                pending.Push(childDirectory);
        }

        return null;
    }

    private static IEnumerable<string> SafeEnumerateFiles(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory).ToArray();
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string directory)
    {
        try
        {
            return Directory.EnumerateDirectories(directory).ToArray();
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }
}
