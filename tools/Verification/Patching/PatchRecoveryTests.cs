using System.Buffers.Binary;
using System.Security.Cryptography;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using AetherXIV.Launcher.Core;

namespace AetherXIV.Launcher.Tests;

public sealed class PatchRecoveryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "aether-patch-tests", Guid.NewGuid().ToString("N"));
    private readonly string patch;
    public PatchRecoveryTests()
    {
        Directory.CreateDirectory(root);
        patch = Path.Combine(root, "test.patch");
    }

    [Fact]
    public void PlansOnlyRemainingVersionsInCanonicalOrder()
    {
        var version = new ClientVersionInfo(ClientVersionInfo.TargetBootVersion, "2012.09.06.0000");
        Assert.Equal(LegacyPatchManifest.Entries[^1], Assert.Single(LegacyPatchApplier.GetPendingPatches(version)));
        Assert.Empty(LegacyPatchApplier.GetPendingPatches(new(ClientVersionInfo.TargetBootVersion, ClientVersionInfo.TargetGameVersion)));
        Assert.Equal(52, LegacyPatchApplier.GetPendingPatches(new(ClientVersionInfo.BaseVersion, ClientVersionInfo.BaseVersion)).Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("garbage")]
    [InlineData("2099.01.01.0000")]
    public void RejectsUnknownVersionBeforeChangingClient(string? version)
        => Assert.Throws<InvalidOperationException>(() => LegacyPatchApplier.GetPendingPatches(new(ClientVersionInfo.TargetBootVersion, version)));

    [Fact]
    public void RollbackRestoresReplacedAndDeletedFilesAndDirectories()
    {
        File.WriteAllText(Path.Combine(root, "old.dat"), "original");
        File.WriteAllText(Path.Combine(root, "obsolete.dat"), "different historical version");
        Directory.CreateDirectory(Path.Combine(root, "obsolete"));
        File.WriteAllText(Path.Combine(root, "obsolete", "keep.dat"), "directory original");
        WritePatch(Entry("old.dat", "replacement"), Entry("new/deep/new.dat", "new"),
            Entry("obsolete.dat", "", delete: true), DirectoryChunk("DELD", "obsolete"), ("FAIL", []));
        Assert.Throws<InvalidDataException>(() => LegacyPatchApplier.ApplyPatchFile(root, patch));
        Assert.Equal("original", File.ReadAllText(Path.Combine(root, "old.dat")));
        Assert.Equal("different historical version", File.ReadAllText(Path.Combine(root, "obsolete.dat")));
        Assert.Equal("directory original", File.ReadAllText(Path.Combine(root, "obsolete", "keep.dat")));
        Assert.False(Directory.Exists(Path.Combine(root, "new")));
        Assert.False(Directory.Exists(Path.Combine(root, ".aetherxiv-patch-transaction")));
        Assert.Contains("Restored", File.ReadAllText(Path.Combine(root, ".aetherxiv-patch.log")));
    }

    [Fact]
    public void CancellationAfterFileReplacementRollsBack()
    {
        File.WriteAllText(Path.Combine(root, "old.dat"), "original");
        WritePatch(Entry("old.dat", "replacement"));
        using CancellationTokenSource cancel = new();
        var progress = new CallbackProgress(update => { if (update.Message.StartsWith("Reading") && update.BytesProcessed == update.TotalBytes) cancel.Cancel(); });
        Assert.Throws<OperationCanceledException>(() => LegacyPatchApplier.ApplyPatchFile(root, patch, progress: progress, cancellationToken: cancel.Token));
        Assert.Equal("original", File.ReadAllText(Path.Combine(root, "old.dat")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RecoversProcessExitBeforeNextPatch(bool restoredBeforeExit)
    {
        string journal = Path.Combine(root, ".aetherxiv-patch-transaction");
        Directory.CreateDirectory(journal);
        string original = restoredBeforeExit ? Path.Combine(root, "old.dat") : Path.Combine(journal, "0000000000.original");
        File.WriteAllText(original, "original");
        if (!restoredBeforeExit) File.WriteAllText(Path.Combine(root, "old.dat"), "partial");
        File.WriteAllText(Path.Combine(journal, "0000000000.json"), JsonSerializer.Serialize(new { Path = "old.dat", Kind = "file" }));
        WritePatch(Entry("other.dat", "new"));
        LegacyPatchApplier.ApplyPatchFile(root, patch);
        Assert.Equal("original", File.ReadAllText(Path.Combine(root, "old.dat")));
    }

    [Fact]
    public void CommittedJournalIsCleanedWithoutUndoingFiles()
    {
        string journal = Path.Combine(root, ".aetherxiv-patch-transaction");
        Directory.CreateDirectory(journal);
        File.WriteAllText(Path.Combine(journal, "0000000000.original"), "original");
        File.WriteAllText(Path.Combine(journal, "0000000000.json"), JsonSerializer.Serialize(new { Path = "old.dat", Kind = "file" }));
        File.WriteAllText(Path.Combine(journal, "committed"), "");
        File.WriteAllText(Path.Combine(root, "old.dat"), "committed");
        WritePatch(Entry("other.dat", "new"));
        LegacyPatchApplier.ApplyPatchFile(root, patch);
        Assert.Equal("committed", File.ReadAllText(Path.Combine(root, "old.dat")));
    }

    [Fact]
    public void UsesExistingCasingAndAcceptsTrailingRootSeparator()
    {
        Directory.CreateDirectory(Path.Combine(root, "DATA"));
        File.WriteAllText(Path.Combine(root, "DATA", "FILE.DAT"), "old");
        WritePatch(Entry("data\\file.dat", "new"));
        LegacyPatchApplier.ApplyPatchFile(root + Path.DirectorySeparatorChar, patch);
        Assert.Equal("new", File.ReadAllText(Path.Combine(root, "DATA", "FILE.DAT")));
        Assert.Single(Directory.GetDirectories(root));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData(".. /outside")]
    [InlineData("/absolute")]
    [InlineData("C:\\outside")]
    [InlineData(".aetherxiv-patch-transaction/committed")]
    public void RejectsPathsOutsideClientOrInsideRecoveryData(string name)
    {
        WritePatch(Entry(name, "new"));
        Assert.Throws<InvalidDataException>(() => LegacyPatchApplier.ApplyPatchFile(root, patch));
    }

    [Fact]
    public void RejectsSymlinkWithoutModifyingTarget()
    {
        if (OperatingSystem.IsWindows()) return; // Creating symlinks requires developer mode or elevation.
        string outside = Path.Combine(root, "outside.dat");
        File.WriteAllText(outside, "original");
        File.CreateSymbolicLink(Path.Combine(root, "link.dat"), outside);
        WritePatch(Entry("link.dat", "new"));
        Assert.Throws<InvalidDataException>(() => LegacyPatchApplier.ApplyPatchFile(root, patch));
        Assert.Equal("original", File.ReadAllText(outside));
    }

    [Fact]
    public void TruncatedChunkDoesNotReplaceOriginal()
    {
        File.WriteAllText(Path.Combine(root, "old.dat"), "original");
        WritePatch(Entry("old.dat", "replacement"));
        using (var stream = File.OpenWrite(patch)) stream.SetLength(stream.Length - 1);
        Assert.Throws<EndOfStreamException>(() => LegacyPatchApplier.ApplyPatchFile(root, patch));
        Assert.Equal("original", File.ReadAllText(Path.Combine(root, "old.dat")));
    }

    [Fact]
    public void ConcurrentPatchIsRejected()
    {
        WritePatch(Entry("new.dat", "new"));
        using var locked = new FileStream(Path.Combine(root, ".aetherxiv-patch.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        Assert.Throws<IOException>(() => LegacyPatchApplier.ApplyPatchFile(root, patch));
        Assert.False(File.Exists(Path.Combine(root, "new.dat")));
    }

    [Fact]
    public void BadChunkChecksumRollsBackReplacement()
    {
        File.WriteAllText(Path.Combine(root, "old.dat"), "original");
        WritePatch(Entry("old.dat", "replacement"));
        byte[] bytes = File.ReadAllBytes(patch);
        bytes[^1] ^= 1;
        File.WriteAllBytes(patch, bytes);
        Assert.Throws<InvalidDataException>(() => LegacyPatchApplier.ApplyPatchFile(root, patch));
        Assert.Equal("original", File.ReadAllText(Path.Combine(root, "old.dat")));
    }

    [Fact]
    public void MissingWholeChunkIsDetectedByHeaderCounts()
    {
        File.WriteAllText(Path.Combine(root, "old.dat"), "original");
        WritePatch(Entry("old.dat", "replacement"), Entry("missing.dat", "missing"));
        byte[] bytes = File.ReadAllBytes(patch);
        int lastChunkSize = Entry("missing.dat", "missing").Item2.Length + 12;
        File.WriteAllBytes(patch, bytes[..^lastChunkSize]);
        Assert.Throws<InvalidDataException>(() => LegacyPatchApplier.ApplyPatchFile(root, patch));
        Assert.Equal("original", File.ReadAllText(Path.Combine(root, "old.dat")));
    }

    [Fact]
    public void FailedLaterWriteToSamePathRestoresOriginal()
    {
        File.WriteAllText(Path.Combine(root, "old.dat"), "original");
        WritePatch(Entry("old.dat", "first"), Entry("old.dat", "second"), ("FAIL", []));
        Assert.Throws<InvalidDataException>(() => LegacyPatchApplier.ApplyPatchFile(root, patch));
        Assert.Equal("original", File.ReadAllText(Path.Combine(root, "old.dat")));
    }

    [Fact]
    public void RecoveryRemovesInterruptedPayloadBeforeRetry()
    {
        string journal = Path.Combine(root, ".aetherxiv-patch-transaction");
        Directory.CreateDirectory(journal);
        Directory.CreateDirectory(Path.Combine(root, "new"));
        File.WriteAllText(Path.Combine(journal, "payload-interrupted.tmp"), "partial data");
        File.WriteAllText(Path.Combine(journal, "0000000000.json"), JsonSerializer.Serialize(new { Path = "new", Kind = "created-directory" }));
        WritePatch(Entry("other.dat", "new"));
        LegacyPatchApplier.ApplyPatchFile(root, patch);
        Assert.False(Directory.Exists(Path.Combine(root, "new")));
        Assert.False(Directory.Exists(journal));
    }

    [Fact]
    public void CreatesEmptyFiles()
    {
        WritePatch(Entry("empty.dat", ""));
        LegacyPatchApplier.ApplyPatchFile(root, patch);
        Assert.Equal(0, new FileInfo(Path.Combine(root, "empty.dat")).Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AppliesFinalHistoryPayloadAndChecksDestinationHash(bool compressed)
    {
        var entry = Entry("history.dat", "new contents", compressed: compressed);
        int prefixLength = 8 + Encoding.UTF8.GetByteCount("history.dat");
        byte[] prefix = entry.Item2[..prefixLength];
        BinaryPrimitives.WriteUInt32BigEndian(prefix.AsSpan(prefixLength - 4), 2);
        byte[] historical = entry.Item2[prefixLength..(prefixLength + 60)];
        BinaryPrimitives.WriteUInt32BigEndian(historical.AsSpan(48), 0);
        WritePatch(("ETRY", [.. prefix, .. historical, .. entry.Item2[prefixLength..]]));
        LegacyPatchApplier.ApplyPatchFile(root, patch);
        Assert.Equal("new contents", File.ReadAllText(Path.Combine(root, "history.dat")));
    }

    [Fact]
    public void RejectsWrongDestinationHashEvenWithValidChunkChecksum()
    {
        File.WriteAllText(Path.Combine(root, "old.dat"), "original");
        var entry = Entry("old.dat", "replacement");
        entry.Item2[8 + Encoding.UTF8.GetByteCount("old.dat") + 24] ^= 1;
        WritePatch(entry);
        Assert.Throws<InvalidDataException>(() => LegacyPatchApplier.ApplyPatchFile(root, patch));
        Assert.Equal("original", File.ReadAllText(Path.Combine(root, "old.dat")));
    }

    [Fact]
    public void RecoversVersionBeforePlanningAndBlocksLaunchUntilRecovery()
    {
        File.WriteAllText(Path.Combine(root, "boot.ver"), ClientVersionInfo.TargetBootVersion);
        File.WriteAllText(Path.Combine(root, "game.ver"), ClientVersionInfo.TargetGameVersion);
        File.WriteAllText(Path.Combine(root, "ffxivgame.exe"), "test");
        string journal = Path.Combine(root, ".aetherxiv-patch-transaction");
        Directory.CreateDirectory(journal);
        File.WriteAllText(Path.Combine(journal, "0000000000.original"), "2012.09.06.0000");
        File.WriteAllText(Path.Combine(journal, "0000000000.json"), JsonSerializer.Serialize(new { Path = "game.ver", Kind = "file" }));
        ClientInstall install = new(root);
        Assert.Equal(ClientInstallState.PatchRequired, install.Inspect().State);
        var report = LegacyPatchManifest.InspectLibrary(root, PatchLibraryInspectionMode.Checksum);
        Assert.Throws<InvalidOperationException>(() => LegacyPatchApplier.ApplyPatchChain(install, report));
        Assert.Equal("2012.09.06.0000", File.ReadAllText(Path.Combine(root, "game.ver")));
    }

    [Fact]
    public void InterruptedCommittedCleanupNeverRollsBackClient()
    {
        string cleanup = Path.Combine(root, ".aetherxiv-patch-transaction-cleanup-test");
        Directory.CreateDirectory(cleanup);
        File.WriteAllText(Path.Combine(cleanup, "0000000000.original"), "old");
        File.WriteAllText(Path.Combine(cleanup, "0000000000.json"), JsonSerializer.Serialize(new { Path = "old.dat", Kind = "file" }));
        File.WriteAllText(Path.Combine(root, "old.dat"), "committed");
        WritePatch(Entry("new.dat", "new"));
        LegacyPatchApplier.ApplyPatchFile(root, patch);
        Assert.Equal("committed", File.ReadAllText(Path.Combine(root, "old.dat")));
        Assert.False(Directory.Exists(cleanup));
    }

    private void WritePatch(params (string Command, byte[] Body)[] chunks)
    {
        using var stream = File.Create(patch);
        stream.Write([0x91, 0x5a, 0x49, 0x50, 0x41, 0x54, 0x43, 0x48, 13, 10, 26, 10]);
        using var header = new MemoryStream();
        U32(header, 0x0200); header.Write(Encoding.ASCII.GetBytes("HIST"));
        U32(header, (uint)chunks.Count(c => c.Command == "ETRY"));
        U32(header, (uint)chunks.Count(c => c.Command == "ADIR"));
        U32(header, (uint)chunks.Count(c => c.Command == "DELD"));
        foreach (var (command, body) in new[] { ("FHDR", header.ToArray()) }.Concat(chunks))
        {
            U32(stream, (uint)body.Length);
            stream.Write(Encoding.ASCII.GetBytes(command));
            stream.Write(body);
            U32(stream, Crc32.Compute([.. Encoding.ASCII.GetBytes(command), .. body]));
        }
    }
    private static (string, byte[]) Entry(string path, string text, bool delete = false, bool compressed = false)
    {
        byte[] payload = Encoding.UTF8.GetBytes(text);
        byte[] stored = payload;
        if (compressed)
        {
            using var zipped = new MemoryStream();
            using (var zlib = new ZLibStream(zipped, CompressionMode.Compress, leaveOpen: true)) zlib.Write(payload);
            stored = zipped.ToArray();
        }
        using var body = new MemoryStream();
        byte[] name = Encoding.UTF8.GetBytes(path);
        U32(body, (uint)name.Length); body.Write(name); U32(body, 1);
        body.Write([(byte)(delete ? 'D' : 'A'), 0, 0, 0]);
        body.Write(new byte[20]); body.Write(delete ? new byte[20] : SHA1.HashData(payload));
        body.Write([(byte)(compressed ? 'Z' : 'N'), 0, 0, 0]);
        U32(body, (uint)stored.Length); U32(body, delete ? 100u : 0); U32(body, (uint)payload.Length);
        body.Write(stored);
        return ("ETRY", body.ToArray());
    }
    private static (string, byte[]) DirectoryChunk(string command, string path)
    {
        using var body = new MemoryStream();
        byte[] name = Encoding.UTF8.GetBytes(path);
        U32(body, (uint)name.Length); body.Write(name); body.Write(new byte[8]);
        return (command, body.ToArray());
    }
    private static void U32(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[4]; BinaryPrimitives.WriteUInt32BigEndian(bytes, value); stream.Write(bytes);
    }
    private sealed class CallbackProgress(Action<PatchApplyProgress> callback) : IProgress<PatchApplyProgress>
    {
        public void Report(PatchApplyProgress value) => callback(value);
    }
    public void Dispose() => Directory.Delete(root, true);
}
