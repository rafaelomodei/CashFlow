using FluentAssertions;

namespace ArchitectureTests;

/// <summary>
/// Regra de dependência da Clean Architecture: as setas apontam sempre para
/// dentro (ADR-001, architecture.md §7).
/// </summary>
[Trait("Category", "Architecture")]
public class LayerDependencyTests
{
    [Theory]
    [InlineData("CashFlow.Domain")]
    [InlineData("Consolidation.Domain")]
    public void Domain_ShouldNotReferenceAnyOtherProject(string domainProject)
    {
        var references = Solution.ProjectReferencesOf(domainProject);

        references.Should().BeEmpty(
            "o Domain é o centro da arquitetura e não pode depender de nenhuma outra camada");
    }

    [Theory]
    [InlineData("CashFlow.Application", "CashFlow.Infrastructure")]
    [InlineData("Consolidation.Application", "Consolidation.Infrastructure")]
    public void Application_ShouldNotReferenceInfrastructure(string applicationProject, string infrastructureProject)
    {
        var references = Solution.ProjectReferencesOf(applicationProject);

        references.Should().NotContain(infrastructureProject,
            "a camada de aplicação depende de abstrações, nunca de implementação de infraestrutura");
    }

    [Theory]
    [InlineData("CashFlow.Domain", "CashFlow.Infrastructure")]
    [InlineData("Consolidation.Domain", "Consolidation.Infrastructure")]
    public void Domain_ShouldNotReferenceInfrastructure(string domainProject, string infrastructureProject)
    {
        var references = Solution.ProjectReferencesOf(domainProject);

        references.Should().NotContain(infrastructureProject,
            "o Domain não conhece EF Core, RabbitMQ nem ASP.NET");
    }

    [Theory]
    [InlineData("CashFlow.Infrastructure")]
    [InlineData("Consolidation.Infrastructure")]
    public void Infrastructure_ShouldNotReferenceApi(string infrastructureProject)
    {
        var references = Solution.ProjectReferencesOf(infrastructureProject);

        references.Should().NotContain(reference => reference.EndsWith(".Api", StringComparison.Ordinal),
            "a infraestrutura implementa portas da aplicação e não conhece a camada de API");
    }
}
