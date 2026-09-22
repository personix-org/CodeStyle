using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Shouldly;

namespace Personix.CodeStyle.Analyzers.Tests;

/// <summary>
/// Compiles a source snippet against the running .NET runtime and returns what
/// <see cref="RequiredInitPropertyAnalyzer"/> reports on it.
/// </summary>
internal static class AnalyzerRunner
{
    private static readonly ImmutableArray<MetadataReference> References =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path)),
    ];

    /// <summary>
    /// Runs the analyzer and returns each diagnostic as <c>"&lt;id&gt; &lt;property name&gt;"</c>, in source order.
    /// </summary>
    /// <param name="source">C# source that must compile without errors.</param>
    /// <param name="isPackable">Value of the <c>IsPackable</c> MSBuild property seen by the analyzer.</param>
    public static async Task<IReadOnlyList<string>> RunAsync(string source, bool isPackable = false)
    {
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));
        var compilation = CSharpCompilation.Create(
            "Tested",
            [tree],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.ToString())
            .ShouldBeEmpty();

        var options = new AnalyzerOptions([], new BuildPropertyOptionsProvider(isPackable));
        var diagnostics = await compilation
            .WithAnalyzers([new RequiredInitPropertyAnalyzer()], options)
            .GetAnalyzerDiagnosticsAsync();

        return diagnostics
            .OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)
            .Select(diagnostic => $"{diagnostic.Id} {tree.GetText().ToString(diagnostic.Location.SourceSpan)}")
            .ToList();
    }

    private sealed class BuildPropertyOptionsProvider(bool isPackable) : AnalyzerConfigOptionsProvider
    {
        private readonly BuildPropertyOptions _options = new(isPackable);

        public override AnalyzerConfigOptions GlobalOptions => _options;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        {
            return _options;
        }

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        {
            return _options;
        }
    }

    private sealed class BuildPropertyOptions(bool isPackable) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            if (key == "build_property.IsPackable")
            {
                value = isPackable ? "true" : "false";
                return true;
            }

            value = string.Empty;
            return false;
        }
    }
}
