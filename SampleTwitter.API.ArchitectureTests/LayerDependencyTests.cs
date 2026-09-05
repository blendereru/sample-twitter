using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace SampleTwitter.API.ArchitectureTests;

public class LayerDependencyTests
{
    private static readonly System.Reflection.Assembly ApiAssembly =
        typeof(Program).Assembly;

    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(ApiAssembly)
        .Build();

    private readonly IObjectProvider<IType> _controllerLayer =
        Classes().That().ResideInNamespace("SampleTwitter.API.Controllers").As("Controllers");

    private readonly IObjectProvider<IType> _serviceLayer =
        Classes().That().ResideInNamespace("SampleTwitter.API.Services").As("Services");

    private readonly IObjectProvider<IType> _abstractionLayer =
        Types().That().ResideInNamespace("SampleTwitter.API.Abstractions").As("Abstractions");

    private readonly IObjectProvider<IType> _modelLayer =
        Classes().That().ResideInNamespace("SampleTwitter.API.Models").As("Models");

    private readonly IObjectProvider<IType> _dataLayer =
        Classes().That().ResideInNamespace("SampleTwitter.API.Data").As("Data");
    
    [Fact]
    public void Controllers_ShouldNotDependOn_ServiceImplementations()
    {
        Types().That().Are(_controllerLayer)
            .Should().NotDependOnAny(_serviceLayer)
            .Check(Architecture);
    }
    
    [Fact]
    public void Controllers_ShouldNotDependOn_DataLayer()
    {
        Types().That().Are(_controllerLayer)
            .Should().NotDependOnAny(_dataLayer)
            .Check(Architecture);
    }
    
    [Fact]
    public void Services_ShouldNotDependOn_Controllers()
    {
        Types().That().Are(_serviceLayer)
            .Should().NotDependOnAny(_controllerLayer)
            .Check(Architecture);
    }
    
    [Fact]
    public void Models_ShouldNotDependOn_Services()
    {
        Types().That().Are(_modelLayer)
            .Should().NotDependOnAny(_serviceLayer)
            .Check(Architecture);
    }

    [Fact]
    public void Models_ShouldNotDependOn_Controllers()
    {
        Types().That().Are(_modelLayer)
            .Should().NotDependOnAny(_controllerLayer)
            .Check(Architecture);
    }
    
    [Fact]
    public void Abstractions_ShouldNotDependOn_ServiceImplementations()
    {
        Types().That().Are(_abstractionLayer)
            .Should().NotDependOnAny(_serviceLayer)
            .Check(Architecture);
    }

    [Fact]
    public void Abstractions_ShouldNotDependOn_Controllers()
    {
        Types().That().Are(_abstractionLayer)
            .Should().NotDependOnAny(_controllerLayer)
            .Check(Architecture);
    }

    [Fact]
    public void Abstractions_ShouldNotDependOn_DataLayer()
    {
        Types().That().Are(_abstractionLayer)
            .Should().NotDependOnAny(_dataLayer)
            .Check(Architecture);
    }
}