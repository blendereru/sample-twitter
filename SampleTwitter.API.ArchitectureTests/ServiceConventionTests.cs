using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace SampleTwitter.API.ArchitectureTests;

public class ServiceConventionTests
{
    private static readonly System.Reflection.Assembly ApiAssembly =
        typeof(Program).Assembly;

    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(ApiAssembly)
        .Build();

    private static readonly HashSet<System.Type> ModelTypes = ApiAssembly
        .GetTypes()
        .Where(t => t.Namespace == "SampleTwitter.API.Models")
        .ToHashSet();

    [Fact]
    public void AllServices_PublicMethods_ShouldNotReturnRawModels()
    {
        var serviceTypes = ApiAssembly
            .GetTypes()
            .Where(t => t.Namespace == "SampleTwitter.API.Services"
                        && t.Name.EndsWith("Service")
                        && t is { IsClass: true, IsAbstract: false });

        foreach (var serviceType in serviceTypes)
        {
            var methods = serviceType.GetMethods(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);

            foreach (var method in methods)
            {
                AssertReturnTypeDoesNotContainModels(method, serviceType.Name);
            }
        }
    }

    [Fact]
    public void AllServiceInterfaces_Methods_ShouldNotReturnRawModels()
    {
        var serviceInterfaces = ApiAssembly
            .GetTypes()
            .Where(t => t.Namespace == "SampleTwitter.API.Abstractions"
                        && t.IsInterface
                        && t.Name.StartsWith("I")
                        && t.Name.EndsWith("Service"));

        foreach (var iface in serviceInterfaces)
        {
            var methods = iface.GetMethods();

            foreach (var method in methods)
            {
                AssertReturnTypeDoesNotContainModels(method, iface.Name);
            }
        }
    }
    
    [Fact]
    public void AllControllers_ShouldNotDependOn_Models()
    {
        Classes().That().ResideInNamespace("SampleTwitter.API.Controllers")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespace("SampleTwitter.API.Models"))
            .Check(Architecture);
    }

    private static void AssertReturnTypeDoesNotContainModels(System.Reflection.MethodInfo method, string ownerName)
    {
        var returnType = method.ReturnType;

        Assert.False(ModelTypes.Contains(returnType),
            $"{ownerName}.{method.Name} returns raw model {returnType.Name}");

        if (returnType.IsGenericType)
        {
            foreach (var genericArg in returnType.GetGenericArguments())
            {
                Assert.False(ModelTypes.Contains(genericArg),
                    $"{ownerName}.{method.Name} returns raw model {genericArg.Name} " +
                    $"via {returnType.Name}<{genericArg.Name}>");
            }
        }
    }
}