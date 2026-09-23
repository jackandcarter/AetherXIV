using AetherXIV.Launcher.Core;

namespace AetherXIV.Launcher.Tests;

public sealed class PatchFileRetryTests
{
    [Theory]
    [InlineData(unchecked((int)0x80070020))]
    [InlineData(unchecked((int)0x80070021))]
    public void TemporaryLocksCanSucceedOnLastAttempt(int hresult)
    {
        int attempts = 0, waits = 0;
        PatchFileOperations.RetrySharingViolation(() =>
        {
            if (++attempts <= 5) throw new IOException("locked", hresult);
        }, wait: _ => waits++);
        Assert.Equal(6, attempts);
        Assert.Equal(5, waits);
    }

    [Fact]
    public void PersistentLockRethrowsOriginalAfterFiveRetries()
    {
        int attempts = 0, waits = 0;
        var error = new IOException("locked", unchecked((int)0x80070020));
        Assert.Same(error, Assert.Throws<IOException>(() =>
            PatchFileOperations.RetrySharingViolation(() => { attempts++; throw error; }, wait: _ => waits++)));
        Assert.Equal(6, attempts);
        Assert.Equal(5, waits);
    }

    [Theory]
    [InlineData(unchecked((int)0x80070005))] // Access denied
    [InlineData(unchecked((int)0x80070070))] // Disk full
    [InlineData(unchecked((int)0x80070002))] // File not found
    public void OtherErrorsAreNotRetried(int hresult)
    {
        int attempts = 0, waits = 0;
        Assert.Throws<IOException>(() => PatchFileOperations.RetrySharingViolation(
            () => { attempts++; throw new IOException("not transient", hresult); }, wait: _ => waits++));
        Assert.Equal(1, attempts);
        Assert.Equal(0, waits);
    }

    [Fact]
    public void CancellationStopsBeforeAnotherAttempt()
    {
        using var cancellation = new CancellationTokenSource();
        int attempts = 0;
        Assert.Throws<OperationCanceledException>(() => PatchFileOperations.RetrySharingViolation(
            () => { attempts++; throw new IOException("locked", unchecked((int)0x80070020)); },
            cancellation.Token, _ => cancellation.Cancel()));
        Assert.Equal(1, attempts);
    }
}

public sealed class WindowsFileLockFactAttribute : FactAttribute
{
    public WindowsFileLockFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
            Skip = "Requires Windows file sharing semantics.";
    }
}
