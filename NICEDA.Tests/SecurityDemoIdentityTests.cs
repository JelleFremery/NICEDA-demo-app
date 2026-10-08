using NICEDA.ApiService.Security;

namespace NICEDA.Tests;

public sealed class SecurityDemoIdentityTests
{
    [Fact]
    public void Resolve_JaneIsLimitedToHerEuCustomers()
    {
        var jane = DemoIdentity.Resolve("jane");

        Assert.True(jane.CanAccess("betabit", "EU"));
        Assert.True(jane.CanAccess("fenetre", null));
        Assert.True(jane.CanAccess("techorama", "eu"));
        Assert.False(jane.CanAccess("contoso", "NA"));
        Assert.False(jane.CanAccess("betabit", "NA"));
    }

    [Fact]
    public void Resolve_AdminCanAccessAllCustomers()
    {
        var admin = DemoIdentity.Resolve("admin");

        Assert.True(admin.CanAccess("contoso", "NA"));
        Assert.True(admin.CanAccess("fabrikam", "NA"));
    }

    [Fact]
    public void Resolve_UnknownIdentityIsDenied()
    {
        Assert.Throws<UnauthorizedAccessException>(() => DemoIdentity.Resolve("not-a-demo-user"));
    }
}
