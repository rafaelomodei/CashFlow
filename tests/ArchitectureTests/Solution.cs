using System.Xml.Linq;

namespace ArchitectureTests;

/// <summary>
/// Lê as referências declaradas nos `.csproj`.
/// A verificação é feita sobre o arquivo de projeto, e não sobre o manifesto do
/// assembly compilado, porque o compilador omite do manifesto as referências que
/// nenhum tipo usa — uma referência proibida em um projeto ainda vazio passaria
/// despercebida justamente enquanto a fronteira é mais barata de corrigir.
/// </summary>
internal static class Solution
{
    private const string SolutionFileName = "CashFlow.sln";

    internal static readonly string RootPath = FindRootPath();

    internal static IReadOnlyList<string> ProjectReferencesOf(string projectName)
    {
        var projectFile = Path.Combine(RootPath, "src", projectName, $"{projectName}.csproj");

        if (!File.Exists(projectFile))
        {
            throw new FileNotFoundException($"Projeto não encontrado: {projectFile}", projectFile);
        }

        return XDocument.Load(projectFile)
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFileNameWithoutExtension(include!.Replace('\\', '/')))
            .ToList();
    }

    private static string FindRootPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException(
                $"{SolutionFileName} não encontrado a partir de {AppContext.BaseDirectory}.");
    }
}
