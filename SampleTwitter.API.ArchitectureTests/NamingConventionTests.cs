using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace SampleTwitter.API.ArchitectureTests;

public class NamingConventionTests
{
    private static readonly System.Reflection.Assembly ApiAssembly =
        typeof(Program).Assembly;

    private static readonly ArchUnitNET.Domain.Architecture Architecture = new ArchLoader()
        .LoadAssemblies(ApiAssembly)
        .Build();
    
    [Fact]
    public void Controllers_ShouldHaveNameEndingWith_Controller()
    {
        Classes().That().ResideInNamespace("SampleTwitter.API.Controllers")
            .Should().HaveNameEndingWith("Controller")
            .Check(Architecture);
    }
    
    [Fact]
    public void ServiceImplementations_ShouldHaveDescriptiveServiceSuffix()
    {
        Classes().That().ResideInNamespace("SampleTwitter.API.Services")
            .Should().HaveNameEndingWith("Service")
            .OrShould().HaveNameEndingWith("Hasher")
            .OrShould().HaveNameEndingWith("Generator")
            .OrShould().HaveNameEndingWith("Sender")
            .Check(Architecture);
    }
    
    [Fact]
    public void Abstractions_ShouldHaveNameStartingWith_I()
    {
        Interfaces().That().ResideInNamespace("SampleTwitter.API.Abstractions")
            .Should().HaveNameStartingWith("I")
            .Check(Architecture);
    }
    
    [Fact]
    public void Exceptions_ShouldHaveNameEndingWith_Exception()
    {
        Classes().That().ResideInNamespace("SampleTwitter.API.Exceptions")
            .Should().HaveNameEndingWith("Exception")
            .Check(Architecture);
    }
    
    [Fact]
    public void ExceptionHandlers_ShouldHaveNameEndingWith_ExceptionHandler()
    {
        Classes().That().ResideInNamespace("SampleTwitter.API.ExceptionHandlers")
            .Should().HaveNameEndingWith("ExceptionHandler")
            .Check(Architecture);
    }
}