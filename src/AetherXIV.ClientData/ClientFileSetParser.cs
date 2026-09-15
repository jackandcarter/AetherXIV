/*
 * AetherXIV
 * Copyright (C) 2026 Demi Dev Unit
 *
 * This file is part of AetherXIV.
 * See THIRD_PARTY_NOTICES.md for historical and third-party attribution.
 *
 * SPDX-License-Identifier: AGPL-3.0-or-later
 */

using System.Text;

namespace AetherXIV.ClientData;

/// <summary>
/// Parses the small CSV-like file-set catalogs embedded in the 1.x client.
/// It extracts references only; it does not assign meaning to the referenced resource.
/// </summary>
public static class ClientFileSetParser
{
    public static ClientFileSetDocument Parse(ReadOnlySpan<byte> bytes)
    {
        string text = Encoding.UTF8.GetString(bytes);
        List<ClientFileSetEntry> entries = [];
        List<ClientFileSetParseIssue> issues = [];
        bool hasHeader = false;
        int lineNumber = 0;
        int charOffset = 0;

        foreach (string rawLine in text.Split('\n'))
        {
            lineNumber++;
            string line = rawLine.TrimEnd('\r');
            int lineOffset = Encoding.UTF8.GetByteCount(text.AsSpan(0, charOffset));
            charOffset += rawLine.Length + 1;
            string trimmed = line.TrimStart('\uFEFF', ' ', '\t');

            if (trimmed.Length == 0)
                continue;

            if (trimmed.StartsWith("#fileSet", StringComparison.Ordinal))
            {
                hasHeader = true;
                continue;
            }

            if (!trimmed.StartsWith("#file", StringComparison.Ordinal))
                continue;

            if (!TryParseCsvFields(trimmed, out List<string> fields, out string? error))
            {
                issues.Add(new(lineNumber, lineOffset, error ?? "Invalid CSV line."));
                continue;
            }

            if (fields.Count < 3)
            {
                issues.Add(new(lineNumber, lineOffset, $"Expected at least 3 fields, found {fields.Count}."));
                continue;
            }

            entries.Add(new(
                lineNumber,
                lineOffset,
                NormalizeField(fields[1]),
                NormalizeField(fields[2]),
                fields.Count > 3 ? NormalizeField(fields[3]) : string.Empty,
                fields.Count));
        }

        bool complete = issues.Count == 0 && (bytes.IsEmpty || bytes[^1] is (byte)'\n' or (byte)'\r');
        if (!complete && !bytes.IsEmpty)
            issues.Add(new(lineNumber, bytes.Length, "Input does not end on a complete newline boundary."));

        return new(hasHeader, complete, bytes.Length, entries, issues);
    }

    private static string NormalizeField(string value)
    {
        return value.TrimEnd('\0');
    }

    private static bool TryParseCsvFields(string line, out List<string> fields, out string? error)
    {
        fields = [];
        error = null;
        StringBuilder field = new();
        bool quoted = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                {
                    quoted = !quoted;
                }

                continue;
            }

            if (c == ',' && !quoted)
            {
                fields.Add(field.ToString());
                field.Clear();
                continue;
            }

            field.Append(c);
        }

        if (quoted)
        {
            error = "Unterminated quoted CSV field.";
            return false;
        }

        fields.Add(field.ToString());
        return true;
    }
}
