using AetherXIV.Launcher.Core;
using System.Security.Cryptography;
using System.Text.Json;

namespace AetherXIV.Launcher.Tests;

// Opt-in: no retail assets are distributed or written back to the library.
public sealed class RetailPatchVerification
{
    [RetailFact]
    public void RealArchivesCheckpointCancelAndResumeWithOnlyPendingPatchesPresent()
    {
        string library = Environment.GetEnvironmentVariable("AETHERXIV_PATCH_LIBRARY")!;
        string root = Path.Combine(Path.GetTempPath(), "aether-retail-verification", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "boot.ver"), ClientVersionInfo.BaseVersion);
            File.WriteAllText(Path.Combine(root, "game.ver"), "2012.09.06.0000");
            PatchEntry[] entries = [LegacyPatchManifest.Entries[0], LegacyPatchManifest.Entries[^1]];
            PatchFileReport[] files = entries.Select(entry =>
            {
                string path = Path.Combine(library, entry.RepositoryId, "patch", entry.PatchFileName);
                return new PatchFileReport(entry, path, "", true, false, new FileInfo(path).Length, Crc32.ComputeFile(path));
            }).ToArray();
            var report = new PatchLibraryReport(library, library, "", LegacyPatchManifest.Entries, files,
                LegacyPatchManifest.Entries.Except(entries).ToArray(), [], [], PatchLibraryInspectionMode.Checksum);
            using var cancellation = new CancellationTokenSource();
            var progress = new CallbackProgress(update =>
            {
                if (update.Message.StartsWith("Finished patch 1/")) cancellation.Cancel();
            });
            Assert.Throws<OperationCanceledException>(() => LegacyPatchApplier.ApplyPatchChain(new(root), report, progress, cancellation.Token));
            Assert.Equal(ClientVersionInfo.TargetBootVersion, File.ReadAllText(Path.Combine(root, "boot.ver")));
            Assert.Equal("2012.09.06.0000", File.ReadAllText(Path.Combine(root, "game.ver")));
            Assert.True(File.Exists(Path.Combine(root, "ffxivboot.exe")));
            PatchApplyResult resumed = LegacyPatchApplier.ApplyPatchChain(new(root), report);
            Assert.Equal(1, resumed.AppliedPatchCount);
            Assert.Equal(ClientVersionInfo.TargetGameVersion, File.ReadAllText(Path.Combine(root, "game.ver")));
            Assert.True(File.Exists(Path.Combine(root, "ffxivgame.exe")));
            Assert.Equal(0, LegacyPatchApplier.ApplyPatchChain(new(root), report).AppliedPatchCount);
            Assert.False(Directory.Exists(Path.Combine(root, ".aetherxiv-patch-transaction")));
        }
        finally { Directory.Delete(root, true); }
    }

    [RetailSamplesFact]
    public void AppliesIndependentlyAuditedSamplesFromEveryRetailArchive()
    {
        string samples = Environment.GetEnvironmentVariable("AETHERXIV_PATCH_SAMPLES")!;
        string[] archives = Directory.GetDirectories(samples);
        Assert.Equal(52, archives.Length);
        foreach (string archive in archives)
        {
            string root = Path.Combine(Path.GetTempPath(), "aether-retail-samples", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var expected = JsonSerializer.Deserialize<Dictionary<string, ExpectedFile?>>(File.ReadAllText(Path.Combine(archive, "expected.json")))!;
                foreach (var entry in expected.Where(entry => entry.Value is null))
                {
                    string deletedPath = Path.Combine(root, entry.Key);
                    Directory.CreateDirectory(Path.GetDirectoryName(deletedPath)!);
                    File.WriteAllText(deletedPath, "An older or partially patched source");
                }
                LegacyPatchApplier.ApplyPatchFile(root, Path.Combine(archive, "sample.patch"));
                foreach (var entry in expected)
                {
                    string path = Path.Combine(root, entry.Key);
                    if (entry.Value is null)
                        Assert.False(File.Exists(path));
                    else
                    {
                        Assert.Equal(entry.Value.size, new FileInfo(path).Length);
                        using var input = File.OpenRead(path);
                        Assert.Equal(entry.Value.sha1, Convert.ToHexString(SHA1.HashData(input)).ToLowerInvariant());
                    }
                }
            }
            finally { Directory.Delete(root, true); }
        }
    }

    private sealed record ExpectedFile(long size, string sha1);

    private sealed class CallbackProgress(Action<PatchApplyProgress> callback) : IProgress<PatchApplyProgress>
    {
        public void Report(PatchApplyProgress value) => callback(value);
    }
}

public sealed class RetailFactAttribute : FactAttribute
{
    public RetailFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AETHERXIV_PATCH_LIBRARY")))
            Skip = "Set AETHERXIV_PATCH_LIBRARY to a user-provided ffxiv_patches directory to verify retail archives.";
    }
}

public sealed class RetailSamplesFactAttribute : FactAttribute
{
    public RetailSamplesFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AETHERXIV_PATCH_SAMPLES")))
            Skip = "Set AETHERXIV_PATCH_SAMPLES to the output of audit-retail-patches.py.";
    }
}
