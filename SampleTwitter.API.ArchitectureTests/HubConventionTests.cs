using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using Microsoft.AspNetCore.SignalR;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace SampleTwitter.API.ArchitectureTests;

public class HubConventionTests
{
    private static readonly System.Reflection.Assembly ApiAssembly =
        typeof(Program).Assembly;

    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(ApiAssembly, typeof(Hub).Assembly)
        .Build();

    [Fact]
    public void Hubs_ShouldHaveNameEndingWith_Hub()
    {
        Classes().That().AreAssignableTo(typeof(Hub))
            .And().ResideInAssembly(ApiAssembly)
            .Should().HaveNameEndingWith("Hub")
            .Check(Architecture);
    }

    [Fact]
    public void Hubs_ShouldNotDependOn_DataLayer_Or_Models()
    {
        Classes().That().AreAssignableTo(typeof(Hub))
            .And().ResideInAssembly(ApiAssembly)
            .Should().NotDependOnAny(
                Types().That().ResideInNamespace("SampleTwitter.API.Data")
                    .Or().ResideInNamespace("SampleTwitter.API.Models"))
            .Check(Architecture);
    }

    [Fact]
    public void Models_ShouldNotDependOn_SignalR()
    {
        Classes().That().ResideInNamespace("SampleTwitter.API.Models")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespaceMatching(@"Microsoft\.AspNetCore\.SignalR.*"))
            .Check(Architecture);
    }
}