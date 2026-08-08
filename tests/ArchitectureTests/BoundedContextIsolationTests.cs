using FluentAssertions;

namespace ArchitectureTests;

/// <summary>
/// Fronteira entre os dois contextos (ADR-002): o único acoplamento aceito é
/// `Shared.Contracts`, e ele é de esquema, não de código executável.
/// </summary>
[Trait("Category", "Architecture")]
public class BoundedContextIsolationTests
{
    private const string CashFlowPrefix = "CashFlow.";
    private const string ConsolidationPrefix = "Consolidation.";
    private const string SharedContracts = "Shared.Contracts";

    private static readonly string[] CashFlowProjects =
    [
        "CashFlow.Domain",
        "CashFlow.Application",
        "CashFlow.Infrastructure",
        "CashFlow.Api"
    ];

    private static readonly string[] ConsolidationProjects =
    [
        "Consolidation.Domain",
        "Consolidation.Application",
        "Consolidation.Infrastructure",
        "Consolidation.Api",
        "Consolidation.Worker"
    ];

    public static TheoryData<string, string> ProjectsAndForeignPrefixes()
    {
        var data = new TheoryData<string, string>();

        foreach (var project in CashFlowProjects)
        {
            data.Add(project, ConsolidationPrefix);
        }

        foreach (var project in ConsolidationProjects)
        {
            data.Add(project, CashFlowPrefix);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ProjectsAndForeignPrefixes))]
    public void Context_ShouldNotReferenceTheOtherContext(string project, string foreignPrefix)
    {
        var references = Solution.ProjectReferencesOf(project);

        references.Should().NotContain(reference => reference.StartsWith(foreignPrefix, StringComparison.Ordinal),
            "Cash Flow e Consolidation se comunicam apenas por evento assíncrono");
    }

    [Fact]
    public void SharedContracts_ShouldNotReferenceAnyProject()
    {
        var references = Solution.ProjectReferencesOf(SharedContracts);

        references.Should().BeEmpty(
            "Shared.Contracts contém apenas contratos de evento — sem regra de negócio e sem dependências");
    }
}
