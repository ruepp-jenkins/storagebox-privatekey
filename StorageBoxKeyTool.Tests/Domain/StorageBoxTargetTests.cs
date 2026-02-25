using StorageBoxKeyTool.Api.Domain;

namespace StorageBoxKeyTool.Tests.Domain;

public sealed class StorageBoxTargetTests
{
    [Fact]
    public void DerivedProperties_AreComputedFromLogin()
    {
        var target = new StorageBoxTarget("u999999-sub314");

        Assert.Equal("u999999-sub314", target.Login);
        Assert.Equal("u999999-sub314.your-storagebox.de", target.Host);
        Assert.Equal(23, target.Port);
        Assert.Equal(StorageBoxTarget.StorageBoxSshPort, target.Port);
    }
}
