using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using Microsoft.AspNetCore.Mvc;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace SampleTwitter.API.ArchitectureTests;

public class ControllerConventionTests
{
    private static readonly System.Reflection.Assembly ApiAssembly =
        typeof(Program).Assembly;

    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(ApiAssembly)
        .Build();
    
    [Fact]
    public void Controllers_ShouldInheritFrom_ControllerBase()
    {
        Classes().That().ResideInNamespace("SampleTwitter.API.Controllers")
            .Should().BeAssignableTo(typeof(ControllerBase))
            .Check(Architecture);
    }
    
    [Fact]
    public void Controllers_ShouldHave_ApiControllerAttribute()
    {
        Classes().That().ResideInNamespace("SampleTwitter.API.Controllers")
            .Should().HaveAnyAttributes(typeof(ApiControllerAttribute))
            .Check(Architecture);
    }
    
    [Fact]
    public void Controllers_ShouldHave_RouteAttribute()
    {
        Classes().That().ResideInNamespace("SampleTwitter.API.Controllers")
            .Should().HaveAnyAttributes(typeof(RouteAttribute))
            .Check(Architecture);
    }
}