namespace UnitTests;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using System.Xml.XPath;

using Innago.Shared.HealthChecks.TcpHealthProbe;

using PublicApiGenerator;

using VerifyTests.DiffPlex;

using Xunit.OpenCategories;

[Category($"{nameof(TcpHealthProbeService)}")]
public class TcpHealthProbeServiceApprovalTests
{
    private const string SourceFolder = "src/";
    private const string ProjectFolder = "TcpHealthProbe";
    private const string ProjectName = $"{TcpHealthProbeServiceApprovalTests.ProjectFolder}.csproj";

    static TcpHealthProbeServiceApprovalTests()
    {
        try
        {
            VerifyDiffPlex.Initialize(OutputType.Minimal);
        }
        catch
        {
            // do nothing
        }
    }

    [Theory]
    [ClassData(typeof(TargetFrameworksTheoryData))]
    [SuppressMessage("Major Code Smell", "S3885:\"Assembly.Load\" should be used")]
    public Task ApproveApi(string framework)
    {
        var dllName = $"{GetProjectElement("/Project/PropertyGroup/AssemblyName")?.Value ?? TcpHealthProbeServiceApprovalTests.ProjectFolder}.dll";
        
        string configuration = typeof(TcpHealthProbeServiceApprovalTests).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;

        string assemblyFile = CombinedPaths(TcpHealthProbeServiceApprovalTests.SourceFolder,
            TcpHealthProbeServiceApprovalTests.ProjectFolder,
            "bin",
            configuration,
            framework,
            dllName);

        Assembly assembly = Assembly.LoadFile(assemblyFile);
        string publicApi = assembly.GeneratePublicApi(options: null);

        return Verifier
            .Verify(publicApi)
            .ScrubLinesContaining("FrameworkDisplayName")
            .UseDirectory(Path.Combine("ApprovedApi"))
            .UseFileName(framework)
            .DisableDiff();
    }

    [SuppressMessage("Major Code Smell", "S1144:Unused private types or members should be removed", Justification = "False Positive")]
    private class TargetFrameworksTheoryData : TheoryData<string>
    {
        public TargetFrameworksTheoryData()
        {
            XElement? targetFrameworks = GetProjectElement("/Project/PropertyGroup/TargetFramework") ??
                                         GetProjectElement("/Project/PropertyGroup/TargetFrameworks");

            this.AddRange(targetFrameworks!.Value.Split(';'));
        }
    }

    private static XElement? GetProjectElement(string expression)
    {
        string csproj = CombinedPaths(TcpHealthProbeServiceApprovalTests.SourceFolder, TcpHealthProbeServiceApprovalTests.ProjectFolder, TcpHealthProbeServiceApprovalTests.ProjectName);
        XDocument project = XDocument.Load(csproj);
        return project.XPathSelectElement(expression);
    }

    private static string GetSolutionDirectory([CallerFilePath] string path = "") =>
        Path.Combine(Path.GetDirectoryName(path)!, "..", "..");

    private static string CombinedPaths(params string[] paths) =>
        Path.GetFullPath(Path.Combine(paths.Prepend(GetSolutionDirectory()).ToArray()));
}