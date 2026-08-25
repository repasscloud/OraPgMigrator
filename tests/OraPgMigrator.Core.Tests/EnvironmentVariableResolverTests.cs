using OraPgMigrator.Infrastructure;

namespace OraPgMigrator.Core.Tests;

public class EnvironmentVariableResolverTests
{
    [Fact]
    public void GetRequired_MissingVariable_ThrowsWithVariableNameOnly()
    {
        var varName = $"ORAPG_TEST_MISSING_{Guid.NewGuid():N}";
        var resolver = new EnvironmentVariableResolver();

        var ex = Assert.Throws<InvalidOperationException>(() => resolver.GetRequired(varName));
        Assert.Contains(varName, ex.Message);
    }

    [Fact]
    public void GetRequired_PresentVariable_ReturnsValue()
    {
        var varName = $"ORAPG_TEST_PRESENT_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(varName, "secret-value");
        try
        {
            var resolver = new EnvironmentVariableResolver();
            Assert.Equal("secret-value", resolver.GetRequired(varName));
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, null);
        }
    }

    [Fact]
    public void GetOptional_MissingVariable_ReturnsNull()
    {
        var resolver = new EnvironmentVariableResolver();
        Assert.Null(resolver.GetOptional($"ORAPG_TEST_ABSENT_{Guid.NewGuid():N}"));
    }
}
