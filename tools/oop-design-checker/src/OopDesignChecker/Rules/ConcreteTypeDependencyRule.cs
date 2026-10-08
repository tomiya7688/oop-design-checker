using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class ConcreteTypeDependencyRule : IAnalysisRule
{
    public RuleDescriptor Descriptor { get; } =
        new("OOP305", "Concrete dependency despite abstraction", DesignDiagnosticSeverity.Warning);

    public IEnumerable<DesignDiagnostic> Analyze(AnalysisContext context)
    {
        var consumerTypeUsages = new Dictionary<ISymbol, ConsumerTypeUsage>(
            SymbolEqualityComparer.Default
        );

        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot(context.CancellationToken);

            foreach (
                var constructor in root.DescendantNodes().OfType<ConstructorDeclarationSyntax>()
            )
            {
                foreach (var parameter in constructor.ParameterList.Parameters)
                {
                    if (
                        semanticModel.GetDeclaredSymbol(parameter, context.CancellationToken)
                            is not IParameterSymbol parameterSymbol
                        || parameterSymbol.Type
                            is not INamedTypeSymbol { TypeKind: TypeKind.Class } concreteType
                    )
                    {
                        continue;
                    }

                    var abstractions = ProjectAbstractionClassifier.FindMeaningfulAbstractions(
                        concreteType
                    );
                    if (abstractions.Count == 0)
                    {
                        continue;
                    }

                    var consumerType = parameterSymbol.ContainingSymbol.ContainingType;
                    if (!consumerTypeUsages.TryGetValue(consumerType, out var consumerTypeUsage))
                    {
                        consumerTypeUsage = CreateConsumerTypeUsage(context, consumerType);
                        consumerTypeUsages.Add(consumerType, consumerTypeUsage);
                    }

                    var abstraction = abstractions.FirstOrDefault(candidate =>
                        !UsesConcreteOnlyContract(
                            context,
                            parameterSymbol,
                            concreteType,
                            candidate,
                            consumerTypeUsage
                        )
                    );
                    if (abstraction is null)
                    {
                        continue;
                    }

                    yield return DiagnosticFactory.Create(
                        Descriptor,
                        parameter.Identifier.GetLocation(),
                        $"This constructor depends on concrete type {concreteType.Name} even though project abstraction {abstraction.Name} covers the behavior this consumer uses. Depend on the abstraction when replacement is part of the design.",
                        parameterSymbol.ToDisplayString()
                    );
                }
            }
        }
    }

    private static bool UsesConcreteOnlyContract(
        AnalysisContext context,
        IParameterSymbol parameter,
        INamedTypeSymbol concreteType,
        INamedTypeSymbol abstraction,
        ConsumerTypeUsage consumerTypeUsage
    )
    {
        var trackedSymbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default) { parameter };
        TrackAssignedMembers(parameter, consumerTypeUsage, trackedSymbols);

        return UsesConcreteOnlyMember(consumerTypeUsage, trackedSymbols, concreteType, abstraction)
            || RequiresConcreteArgumentType(
                context,
                consumerTypeUsage,
                trackedSymbols,
                abstraction
            );
    }

    private static ConsumerTypeUsage CreateConsumerTypeUsage(
        AnalysisContext context,
        INamedTypeSymbol consumerType
    )
    {
        var memberAccessesByReceiver = new Dictionary<ISymbol, List<ISymbol>>(
            SymbolEqualityComparer.Default
        );
        var argumentsBySymbol = new Dictionary<ISymbol, List<ITypeSymbol>>(
            SymbolEqualityComparer.Default
        );
        var assignedMembersByParameter = new Dictionary<ISymbol, List<ISymbol>>(
            SymbolEqualityComparer.Default
        );

        foreach (var syntaxReference in consumerType.DeclaringSyntaxReferences)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (
                syntaxReference.GetSyntax(context.CancellationToken)
                is not ClassDeclarationSyntax declaration
            )
            {
                continue;
            }

            var semanticModel = context.Project.GetSemanticModel(declaration.SyntaxTree);
            foreach (
                var access in declaration.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
            )
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                AddUsage(
                    memberAccessesByReceiver,
                    semanticModel
                        .GetSymbolInfo(access.Expression, context.CancellationToken)
                        .Symbol,
                    semanticModel.GetSymbolInfo(access, context.CancellationToken).Symbol
                );
            }

            foreach (var argument in declaration.DescendantNodes().OfType<ArgumentSyntax>())
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                AddUsage(
                    argumentsBySymbol,
                    semanticModel
                        .GetSymbolInfo(argument.Expression, context.CancellationToken)
                        .Symbol,
                    semanticModel
                        .GetTypeInfo(argument.Expression, context.CancellationToken)
                        .ConvertedType
                );
            }

            foreach (
                var constructor in declaration
                    .DescendantNodes()
                    .OfType<ConstructorDeclarationSyntax>()
            )
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (
                    semanticModel.GetDeclaredSymbol(constructor, context.CancellationToken)
                    is not IMethodSymbol constructorSymbol
                )
                {
                    continue;
                }

                foreach (
                    var assignment in constructor
                        .DescendantNodes()
                        .OfType<AssignmentExpressionSyntax>()
                )
                {
                    var valueSymbol = semanticModel
                        .GetSymbolInfo(assignment.Right, context.CancellationToken)
                        .Symbol;
                    var targetSymbol = semanticModel
                        .GetSymbolInfo(assignment.Left, context.CancellationToken)
                        .Symbol;
                    if (
                        valueSymbol is not null
                        && targetSymbol is IFieldSymbol or IPropertySymbol
                        && SymbolEqualityComparer.Default.Equals(
                            targetSymbol.ContainingType,
                            constructorSymbol.ContainingType
                        )
                    )
                    {
                        AddUsage(assignedMembersByParameter, valueSymbol, targetSymbol);
                    }
                }
            }
        }

        return new ConsumerTypeUsage(
            memberAccessesByReceiver,
            argumentsBySymbol,
            assignedMembersByParameter
        );
    }

    private static void TrackAssignedMembers(
        IParameterSymbol parameter,
        ConsumerTypeUsage consumerTypeUsage,
        HashSet<ISymbol> trackedSymbols
    )
    {
        if (!consumerTypeUsage.AssignedMembersByParameter.TryGetValue(parameter, out var members))
        {
            return;
        }

        foreach (var member in members)
        {
            trackedSymbols.Add(member);
        }
    }

    private static bool UsesConcreteOnlyMember(
        ConsumerTypeUsage consumerTypeUsage,
        HashSet<ISymbol> trackedSymbols,
        INamedTypeSymbol concreteType,
        INamedTypeSymbol abstraction
    )
    {
        foreach (var trackedSymbol in trackedSymbols)
        {
            if (
                !consumerTypeUsage.MemberAccessesByReceiver.TryGetValue(
                    trackedSymbol,
                    out var accessedMembers
                )
            )
            {
                continue;
            }

            foreach (var accessedMember in accessedMembers)
            {
                if (accessedMember.ContainingType?.SpecialType == SpecialType.System_Object)
                {
                    continue;
                }

                if (!IsProvidedByAbstraction(concreteType, abstraction, accessedMember))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool RequiresConcreteArgumentType(
        AnalysisContext context,
        ConsumerTypeUsage consumerTypeUsage,
        HashSet<ISymbol> trackedSymbols,
        INamedTypeSymbol abstraction
    )
    {
        foreach (var trackedSymbol in trackedSymbols)
        {
            if (
                !consumerTypeUsage.ArgumentsBySymbol.TryGetValue(
                    trackedSymbol,
                    out var convertedTypes
                )
            )
            {
                continue;
            }

            foreach (var convertedType in convertedTypes)
            {
                if (
                    !context
                        .Project.Compilation.ClassifyConversion(abstraction, convertedType)
                        .IsImplicit
                )
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsProvidedByAbstraction(
        INamedTypeSymbol concreteType,
        INamedTypeSymbol abstraction,
        ISymbol accessedMember
    )
    {
        foreach (var contractType in abstraction.AllInterfaces.Prepend(abstraction))
        {
            foreach (var contractMember in contractType.GetMembers())
            {
                var implementation = concreteType.FindImplementationForInterfaceMember(
                    contractMember
                );
                if (
                    SymbolEqualityComparer.Default.Equals(implementation, accessedMember)
                    || SymbolEqualityComparer.Default.Equals(
                        implementation?.OriginalDefinition,
                        accessedMember.OriginalDefinition
                    )
                )
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void AddUsage<T>(
        Dictionary<ISymbol, List<T>> usagesBySymbol,
        ISymbol? symbol,
        T? usage
    )
        where T : class
    {
        if (symbol is null || usage is null)
        {
            return;
        }

        if (!usagesBySymbol.TryGetValue(symbol, out var usages))
        {
            usages = [];
            usagesBySymbol.Add(symbol, usages);
        }

        usages.Add(usage);
    }

    private sealed record ConsumerTypeUsage(
        IReadOnlyDictionary<ISymbol, List<ISymbol>> MemberAccessesByReceiver,
        IReadOnlyDictionary<ISymbol, List<ITypeSymbol>> ArgumentsBySymbol,
        IReadOnlyDictionary<ISymbol, List<ISymbol>> AssignedMembersByParameter
    );
}
