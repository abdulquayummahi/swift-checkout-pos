using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.EntityFrameworkCore;
using SwiftCheckoutPOS.DAL.Context;
using SwiftCheckoutPOS.Tests.Integration.Infrastructure;

namespace SwiftCheckoutPOS.Tests.Integration;

public sealed class FrameworkProofTests
{
    [Fact]
    public void ConfiguredProviderIsSqlServerWithRetriesAndNet10()
    {
        Assert.Equal(".NETCoreApp,Version=v10.0",
            typeof(FrameworkProofTests).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        using var context = new ApplicationDbContext(SchemaScript.Options(TestConnectionGuard.FromEnvironment()));
        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", context.Database.ProviderName);
        Assert.True(context.Database.CreateExecutionStrategy().RetriesOnFailure);
    }
}
