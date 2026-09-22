using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Personix.CodeStyle.Analyzers;

/// <summary>
/// Reports properties whose accessors allow more than the code in the compilation does with them:
/// a setter written only while the object is being created (PERSONIX005), and an <c>init</c>
/// property without a default that every creation site sets (PERSONIX006).
/// </summary>
/// <remarks>
/// The analysis sees one compilation. In a packable project, properties whose setter is visible
/// outside the assembly are skipped, because other assemblies may write them.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RequiredInitPropertyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>PERSONIX005 — the setter is only used during object creation and should be <c>init</c>.</summary>
    public static readonly DiagnosticDescriptor UseInitAccessor = new(
        id: "PERSONIX005",
        title: "Property is only assigned during object creation",
        messageFormat: "Property '{0}' is only assigned while the object is being created; declare its setter as 'init'",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/personix-org/CodeStyle#personix005",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>PERSONIX006 — every creation site sets the <c>init</c> property, so it should be <c>required</c>.</summary>
    public static readonly DiagnosticDescriptor UseRequiredModifier = new(
        id: "PERSONIX006",
        title: "Init property is set at every object creation",
        messageFormat: "Property '{0}' has no default value and every object creation sets it; declare it as 'required'",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/personix-org/CodeStyle#personix006",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [UseInitAccessor, UseRequiredModifier];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // Writes in generated code count as usage, diagnostics located there are not reported.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var usage = new PropertyUsage(context.Compilation.Assembly, IsPackable(context.Options));

        context.RegisterOperationAction(operationContext => usage.RecordReference(operationContext), OperationKind.PropertyReference);
        context.RegisterOperationAction(operationContext => usage.RecordCreation((IObjectCreationOperation)operationContext.Operation), OperationKind.ObjectCreation);
        context.RegisterCompilationEndAction(usage.Report);
    }

    private static bool IsPackable(AnalyzerOptions options)
    {
        return options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.IsPackable", out var value)
            && string.Equals(value, "true", System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Collects, for one compilation, where each property is written and which types get created.
    /// </summary>
    private sealed class PropertyUsage(IAssemblySymbol assembly, bool isPackable)
    {
        private readonly ConcurrentDictionary<IPropertySymbol, byte> _writtenDuringCreation = new(SymbolEqualityComparer.Default);
        private readonly ConcurrentDictionary<IPropertySymbol, byte> _writtenInConstructor = new(SymbolEqualityComparer.Default);
        private readonly ConcurrentDictionary<IPropertySymbol, byte> _writtenElsewhere = new(SymbolEqualityComparer.Default);
        private readonly ConcurrentDictionary<IPropertySymbol, byte> _omittedAtCreation = new(SymbolEqualityComparer.Default);
        private readonly ConcurrentDictionary<INamedTypeSymbol, byte> _createdTypes = new(SymbolEqualityComparer.Default);

        public void RecordReference(OperationAnalysisContext context)
        {
            var reference = (IPropertyReferenceOperation)context.Operation;
            var property = reference.Property.OriginalDefinition;

            if (property.IsStatic || property.IsIndexer)
            {
                return;
            }

            switch (ClassifyWrite(reference, context.ContainingSymbol))
            {
                case WriteKind.ObjectInitializer:
                    _writtenDuringCreation.TryAdd(property, 0);
                    break;
                case WriteKind.Constructor:
                    _writtenDuringCreation.TryAdd(property, 0);
                    _writtenInConstructor.TryAdd(property, 0);
                    break;
                case WriteKind.Other:
                    _writtenElsewhere.TryAdd(property, 0);
                    break;
            }
        }

        public void RecordCreation(IObjectCreationOperation creation)
        {
            if (creation.Type is not INamedTypeSymbol createdType)
            {
                return;
            }

            var assigned = new HashSet<IPropertySymbol>(
                (creation.Initializer?.Initializers ?? [])
                    .OfType<ISimpleAssignmentOperation>()
                    .Select(assignment => assignment.Target)
                    .OfType<IPropertyReferenceOperation>()
                    .Select(target => target.Property.OriginalDefinition),
                SymbolEqualityComparer.Default);

            for (var type = createdType.OriginalDefinition; type is not null; type = type.BaseType?.OriginalDefinition)
            {
                if (!SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, assembly))
                {
                    break;
                }

                _createdTypes.TryAdd(type, 0);

                foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
                {
                    if (!assigned.Contains(property))
                    {
                        _omittedAtCreation.TryAdd(property, 0);
                    }
                }
            }
        }

        public void Report(CompilationAnalysisContext context)
        {
            foreach (var property in _writtenDuringCreation.Keys)
            {
                if (property.SetMethod is { IsInitOnly: false }
                    && !_writtenElsewhere.ContainsKey(property)
                    && TryGetDeclaration(property, out var declaration))
                {
                    context.ReportDiagnostic(Diagnostic.Create(UseInitAccessor, declaration.Identifier.GetLocation(), property.Name));
                }
            }

            foreach (var type in _createdTypes.Keys)
            {
                foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
                {
                    if (property.SetMethod is { IsInitOnly: true }
                        && !property.IsRequired
                        && !_omittedAtCreation.ContainsKey(property)
                        && !_writtenInConstructor.ContainsKey(property)
                        && !_writtenElsewhere.ContainsKey(property)
                        && TryGetDeclaration(property, out var declaration)
                        && declaration.Initializer is null)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(UseRequiredModifier, declaration.Identifier.GetLocation(), property.Name));
                    }
                }
            }
        }

        /// <summary>
        /// Finds the property's declaration when its accessors are this compilation's to change.
        /// </summary>
        private bool TryGetDeclaration(IPropertySymbol property, out PropertyDeclarationSyntax declaration)
        {
            declaration = null!;

            if (property.IsStatic
                || property.IsIndexer
                || property.IsAbstract
                || property.IsVirtual
                || property.IsOverride
                || property.ContainingType.TypeKind == TypeKind.Interface
                || !SymbolEqualityComparer.Default.Equals(property.ContainingAssembly, assembly)
                || ImplementsInterfaceMember(property)
                || (isPackable && IsVisibleOutsideAssembly(property.SetMethod!)))
            {
                return false;
            }

            if (property.DeclaringSyntaxReferences.Length != 1
                || property.DeclaringSyntaxReferences[0].GetSyntax() is not PropertyDeclarationSyntax syntax)
            {
                return false;
            }

            declaration = syntax;
            return true;
        }

        private static WriteKind ClassifyWrite(IPropertyReferenceOperation reference, ISymbol containingSymbol)
        {
            IOperation target = reference;
            while (target.Parent is ITupleOperation tuple)
            {
                target = tuple;
            }

            if (target.Parent is IIncrementOrDecrementOperation increment && increment.Target == target)
            {
                return WriteKind.Other;
            }

            if (target.Parent is not IAssignmentOperation assignment || assignment.Target != target)
            {
                return WriteKind.None;
            }

            if (assignment is not ISimpleAssignmentOperation || target != reference)
            {
                return WriteKind.Other;
            }

            if (reference.Instance is IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ImplicitReceiver }
                && assignment.Parent is IObjectOrCollectionInitializerOperation { Parent: IObjectCreationOperation or IWithOperation })
            {
                return WriteKind.ObjectInitializer;
            }

            if (reference.Instance is IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance }
                && containingSymbol is IMethodSymbol { MethodKind: MethodKind.Constructor, IsStatic: false } constructor
                && DerivesFromOrEquals(constructor.ContainingType, reference.Property.ContainingType)
                && !IsInsideNestedFunction(assignment))
            {
                return WriteKind.Constructor;
            }

            return WriteKind.Other;
        }

        private static bool IsInsideNestedFunction(IOperation operation)
        {
            for (var current = operation.Parent; current is not null; current = current.Parent)
            {
                if (current is IAnonymousFunctionOperation or ILocalFunctionOperation)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool DerivesFromOrEquals(INamedTypeSymbol? type, INamedTypeSymbol baseType)
        {
            for (var current = type; current is not null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, baseType.OriginalDefinition))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ImplementsInterfaceMember(IPropertySymbol property)
        {
            return property.ExplicitInterfaceImplementations.Length > 0
                || property.ContainingType.AllInterfaces
                    .SelectMany(@interface => @interface.GetMembers().OfType<IPropertySymbol>())
                    .Any(member => SymbolEqualityComparer.Default.Equals(property.ContainingType.FindImplementationForInterfaceMember(member), property));
        }

        private static bool IsVisibleOutsideAssembly(ISymbol symbol)
        {
            for (var current = symbol; current is not null and not INamespaceSymbol; current = current.ContainingSymbol)
            {
                if (current.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal))
                {
                    return false;
                }
            }

            return true;
        }
    }

    private enum WriteKind
    {
        None,
        ObjectInitializer,
        Constructor,
        Other,
    }
}
