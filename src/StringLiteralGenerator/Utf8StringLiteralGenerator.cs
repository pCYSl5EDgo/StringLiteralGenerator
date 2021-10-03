﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StringLiteralGenerator;

[Generator]
public partial class Utf8StringLiteralGenerator : ISourceGenerator
{
    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxReceiver is not SyntaxReceiver receiver) return;

        var compilation = context.Compilation;

        var buffer = new StringBuilder();

        var group = enumerate().GroupBy(x => x.type, x => x.method);

        foreach (var g in group)
        {
            var containingType = g.Key;
            var generatedSource = Generate(containingType, g, buffer);
            var filename = GetFilename(containingType, buffer);
            context.AddSource(filename, SourceText.From(generatedSource, Encoding.UTF8));
        }

        IEnumerable<(TypeInfo type, MethodInfo method)> enumerate()
        {
            foreach (var m in receiver.CandidateMethods)
            {
                if (!IsStaticPartial(m)) continue;

                var model = compilation.GetSemanticModel(m.SyntaxTree);

                if (m.ParameterList.Parameters.Count != 0) continue;
                if (model.GetDeclaredSymbol(m) is not { } methodSymbol) continue;
                if (!ReturnsString(methodSymbol)) continue;
                if (GetUtf8Attribute(methodSymbol) is not { } value) continue;


                yield return (new(methodSymbol.ContainingType), new(methodSymbol, value));
            }
        }
    }

    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForPostInitialization(AddAttribute);
        context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
    }

    class SyntaxReceiver : ISyntaxReceiver
    {
        public List<MethodDeclarationSyntax> CandidateMethods { get; } = new List<MethodDeclarationSyntax>();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            // any field with at least one attribute is a candidate for property generation
            if (syntaxNode is MethodDeclarationSyntax methodDeclarationSyntax
                && methodDeclarationSyntax.AttributeLists.Count > 0)
            {
                CandidateMethods.Add(methodDeclarationSyntax);
            }
        }
    }
}
