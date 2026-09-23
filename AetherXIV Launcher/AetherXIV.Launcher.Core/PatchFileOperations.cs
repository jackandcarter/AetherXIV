/*
 * AetherXIV
 * Copyright (C) 2026 Demi Dev Unit
 * SPDX-License-Identifier: AGPL-3.0-or-later
 */

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("PatchingVerification")]

namespace AetherXIV.Launcher.Core;

internal static class PatchFileOperations
{
    // One initial attempt plus five retries, matching Seventh Umbral's limit.
    internal static void RetrySharingViolation(
        Action operation,
        CancellationToken cancellationToken = default,
        Action<CancellationToken>? wait = null)
    {
        for (int retries = 0; ; retries++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                operation();
                return;
            }
            catch (IOException error) when (retries < 5
                && error.HResult is unchecked((int)0x80070020) or unchecked((int)0x80070021))
            {
                // ERROR_SHARING_VIOLATION / ERROR_LOCK_VIOLATION only. Permissions,
                // disk capacity, missing paths, and other I/O failures are not transient.
                if (wait is not null)
                    wait(cancellationToken);
                else if (cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(1)))
                    cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }
}
