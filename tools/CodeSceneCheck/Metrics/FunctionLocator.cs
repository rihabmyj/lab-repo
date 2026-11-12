using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeSceneCheck.Metrics;

/// <summary>A named function: method, constructor, local function or property accessor.</summary>
public sealed record FunctionInfo(string Name, string QualifiedName, SyntaxNode Node, int StartLine, int EndLine)
{
    public int ParameterCount => Node switch
    {
        BaseMethodDeclarationSyntax m => m.ParameterList.Parameters.Count,
        LocalFunctionStatementSyntax l => l.ParameterList.Parameters.Count,
        _ => 0
    };
}

public static class FunctionLocator
{
    public static IReadOnlyList<FunctionInfo> AllFunctions(SyntaxNode root) =>
        root.DescendantNodes()
            .Select(ToFunction)
            .OfType<FunctionInfo>()
            .ToList();

    /// <summary>
    /// Finds a function by simple or qualified name ("CreateOrder", "OrderingEndpoints.CreateOrder").
    /// Overloads are disambiguated with the reported start line when one of them contains it.
    /// Line numbers are only a tie-breaker: they shift as soon as the file is edited.
    /// </summary>
    public static IReadOnlyList<FunctionInfo> Find(SyntaxNode root, string name, int? startLine)
    {
        var matches = AllFunctions(root)
            .Where(f => f.Name == name || f.QualifiedName == name || f.QualifiedName.EndsWith("." + name, StringComparison.Ordinal))
            .ToList();

        if (matches.Count > 1 && startLine is int line)
        {
            var containing = matches.Where(f => f.StartLine <= line && line <= f.EndLine).ToList();
            if (containing.Count > 0)
            {
                return containing;
            }
        }

        return matches;
    }

    private static FunctionInfo? ToFunction(SyntaxNode node)
    {
        var name = node switch
        {
            MethodDeclarationSyntax m => m.Identifier.Text,
            ConstructorDeclarationSyntax c => c.Identifier.Text,
            LocalFunctionStatementSyntax l => l.Identifier.Text,
            AccessorDeclarationSyntax { Body: not null } or AccessorDeclarationSyntax { ExpressionBody: not null } =>
                AccessorName((AccessorDeclarationSyntax)node),
            _ => null
        };

        if (name is null)
        {
            return null;
        }

        var span = node.GetLocation().GetLineSpan();
        return new FunctionInfo(name, Qualify(node, name), node, span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1);
    }

    private static string? AccessorName(AccessorDeclarationSyntax accessor) =>
        accessor.Parent?.Parent is BasePropertyDeclarationSyntax property
            ? $"{PropertyName(property)}.{accessor.Keyword.Text}"
            : null;

    private static string PropertyName(BasePropertyDeclarationSyntax property) => property switch
    {
        PropertyDeclarationSyntax p => p.Identifier.Text,
        EventDeclarationSyntax e => e.Identifier.Text,
        _ => "this"
    };

    private static string Qualify(SyntaxNode node, string name)
    {
        var owners = node.Ancestors()
            .Select(a => a switch
            {
                BaseTypeDeclarationSyntax t => t.Identifier.Text,
                LocalFunctionStatementSyntax l => l.Identifier.Text,
                MethodDeclarationSyntax m => m.Identifier.Text,
                _ => null
            })
            .OfType<string>()
            .Reverse();

        return string.Join('.', owners.Append(name));
    }
}
