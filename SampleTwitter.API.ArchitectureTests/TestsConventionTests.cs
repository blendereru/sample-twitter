using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace SampleTwitter.API.ArchitectureTests;

public class TestsConventionTests
{
    private static readonly System.Reflection.Assembly UnitTestsAssembly =
        typeof(SampleTwitter.API.UnitTests.Services.PostServiceTests).Assembly;

    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(Program).Assembly,
            UnitTestsAssembly,
            typeof(Microsoft.EntityFrameworkCore.DbContext).Assembly)
        .Build();

    [Fact]
    public void UnitTests_ShouldNotAssertDatabase()
    {
        Classes().That().ResideInAssembly(UnitTestsAssembly)
            .Should().NotDependOnAny(
                typeof(Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions),
                typeof(Queryable))
            .WithoutRequiringPositiveResults()
            .Check(Architecture);

        var forbiddenQueryPrefixes = new[]
        {
            "Find(",
            "FindAsync(",
            "GetEnumerator("
        };

        var unitTestAssemblyName = UnitTestsAssembly.GetName().Name;
        var unitTestMethods = Architecture.MethodMembers
            .Where(m => m.DeclaringType.Assembly.Name == unitTestAssemblyName)
            .ToList();

        foreach (var method in unitTestMethods)
        {
            foreach (var dep in method.MemberDependencies.OfType<ArchUnitNET.Domain.Dependencies.MethodCallDependency>())
            {
                var targetType = dep.Target;
                var targetMember = dep.TargetMember;

                var isDbTarget = targetType.FullName.StartsWith("Microsoft.EntityFrameworkCore.DbSet") ||
                                 targetType.FullName.StartsWith("Microsoft.EntityFrameworkCore.DbContext") ||
                                 targetType.FullName == "SampleTwitter.API.Data.ApplicationContext";

                if (isDbTarget)
                {
                    var isForbidden = forbiddenQueryPrefixes.Any(prefix => targetMember.Name.StartsWith(prefix, StringComparison.Ordinal));
                    Assert.False(
                        isForbidden,
                        $"Unit test method '{method.FullName}' calls forbidden database query method '{targetMember.Name}' on '{targetType.FullName}'. " +
                        "Unit tests must not query or assert on the database.");
                }
            }
        }
    }
}