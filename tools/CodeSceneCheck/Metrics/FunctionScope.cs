using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeSceneCheck.Metrics;

/// <summary>
/// Walks the code that belongs to one function. Lambdas count as part of the enclosing function
/// (as CodeScene does); nested local functions do not, because they are measured on their own.
/// </summary>
public static class FunctionScope
{
    public static IEnumerable<SyntaxNode> Descendants(SyntaxNode function) =>
        function.DescendantNodes(n => n == function || n is not LocalFunctionStatementSyntax)
            .Where(n => n is not LocalFunctionStatementSyntax);

    public static IEnumerable<SyntaxNode> Children(SyntaxNode node) =>
        node.ChildNodes().Where(n => n is not LocalFunctionStatementSyntax);
}
