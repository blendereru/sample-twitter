using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using SampleTwitter.API.Exceptions;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace SampleTwitter.API.ArchitectureTests;

public class ExceptionHierarchyTests
{
    private static readonly System.Reflection.Assembly ApiAssembly =
        typeof(Program).Assembly;

    private static readonly ArchUnitNET.Domain.Architecture Architecture = new ArchLoader()
        .LoadAssemblies(ApiAssembly)
        .Build();

    /// <summary>
    /// All custom exceptions in the Exceptions namespace (except EmailDeliveryException,
    /// which is an infrastructure concern) must inherit from AppException.
    /// </summary>
    [Fact]
    public void DomainExceptions_ShouldInheritFrom_AppException()
    {
        Classes().That().ResideInNamespace("SampleTwitter.API.Exceptions")
            .And().AreNotAbstract()
            .And().DoNotHaveName("EmailDeliveryException")
            .Should().BeAssignableTo(typeof(AppException))
            .Check(Architecture);
    }
}