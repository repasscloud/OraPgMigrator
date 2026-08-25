using OraPgMigrator.Infrastructure.FileSystem;

namespace OraPgMigrator.Core.Tests.FileSystem;

public class OutputPathNormalizerTests
{
    [Fact]
    public void LocalWindowsPath_PassesThroughUnchanged() =>
        Assert.Equal(@"C:\data_export", OutputPathNormalizer.Normalize(@"C:\data_export"));

    [Fact]
    public void ExistingUncPath_PassesThroughUnchanged() =>
        Assert.Equal(@"\\server\folder\path", OutputPathNormalizer.Normalize(@"\\server\folder\path"));

    [Fact]
    public void ForwardSlashUncPath_IsNormalizedToBackslashUnc() =>
        Assert.Equal(@"\\server\folder\path", OutputPathNormalizer.Normalize("//server/folder/path"));

    [Fact]
    public void ForwardSlashLocalPath_IsNormalizedToBackslashes() =>
        Assert.Equal(@"C:\data_export\sub", OutputPathNormalizer.Normalize("C:/data_export/sub"));

    [Fact]
    public void PosixStylePath_PassesThroughUnchanged() =>
        Assert.Equal("/tmp/data_export", OutputPathNormalizer.Normalize("/tmp/data_export"));

    [Fact]
    public void RelativePath_PassesThroughUnchanged() =>
        Assert.Equal("data/export", OutputPathNormalizer.Normalize("data/export"));

    [Fact]
    public void IsUncPath_DetectsUncRoot()
    {
        Assert.True(OutputPathNormalizer.IsUncPath(OutputPathNormalizer.Normalize("//server/share")));
        Assert.False(OutputPathNormalizer.IsUncPath(OutputPathNormalizer.Normalize(@"C:\data")));
    }
}
