using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;

namespace ArchitectureTests;

/// <summary>
/// O Domain não conhece infraestrutura (ADR-001, architecture.md §7).
///
/// Enquanto o Domain não tiver tipos, esta regra não tem o que reprovar — ela
/// existe desde já para que a fronteira esteja protegida no commit em que a
/// primeira entidade nascer, e não depois de ser violada.
/// </summary>
[Trait("Category", "Architecture")]
public class DomainFrameworkIsolationTests
{
    private static readonly string[] ForbiddenDependencies =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "RabbitMQ",
        "Microsoft.AspNetCore",
        "Microsoft.Extensions.DependencyInjection"
    ];

    [Theory]
    [InlineData("CashFlow.Domain")]
    [InlineData("Consolidation.Domain")]
    public void Domain_ShouldNotDependOnInfrastructureFrameworks(string assemblyName)
    {
        var assembly = LoadFromOutput(assemblyName);

        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(ForbiddenDependencies)
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty(
            "nenhum tipo de domínio pode depender de EF Core, Npgsql, RabbitMQ ou ASP.NET");
    }

    private static Assembly LoadFromOutput(string assemblyName) =>
        Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.dll"));
}
