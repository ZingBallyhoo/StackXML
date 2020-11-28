using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace StackXML.Generator
{
    /// <summary>Helper class for generating partial parts of nested types</summary>
    public class NestedScope
    {
        private readonly List<string> m_containingClasses;
        private readonly string m_namespace;
        
        public NestedScope(INamedTypeSymbol classSymbol)
        {
            m_namespace = classSymbol.ContainingNamespace?.ToDisplayString();
            
            m_containingClasses = new List<string>();
            var containingSymbol = classSymbol.ContainingSymbol;
            while (!containingSymbol.Equals(classSymbol.ContainingNamespace, SymbolEqualityComparer.Default))
            {
                var containingNamedType = (INamedTypeSymbol) containingSymbol;
                m_containingClasses.Add(GetClsString(containingNamedType));
                containingSymbol = containingSymbol.ContainingSymbol;
            }

            m_containingClasses.Reverse();
        }

        private static string TypeKindToStr(TypeKind kind)
        {
            // node: kind.ToString() = "structure" :((
            return kind switch
            {
                TypeKind.Class => "class",
                TypeKind.Struct => "struct",
                _ => throw new Exception($"Unhandled kind {kind} in {nameof(TypeKindToStr)}")
            };
        }

        public static string GetClsString(INamedTypeSymbol namedTypeSymbol)
        {
            // {public/private...} {ref} partial {class/struct} {name}
            var str =
                $"{namedTypeSymbol.DeclaredAccessibility.ToString().ToLowerInvariant()} {(namedTypeSymbol.IsRefLikeType ? "ref " : string.Empty)}partial {TypeKindToStr(namedTypeSymbol.TypeKind)} {namedTypeSymbol.Name}";
            return str;
        }

        public void Start(IndentedTextWriter writer)
        {
            if (m_namespace != null)
            {
                writer.WriteLine($"namespace {m_namespace}");
                writer.WriteLine("{");
                writer.Indent++;
            }
            
            foreach (var containingClass in m_containingClasses)
            {
                writer.WriteLine($"{containingClass}");
                writer.WriteLine("{");
                writer.Indent++;
            }
        }

        public void End(IndentedTextWriter writer)
        {
            foreach (var _ in m_containingClasses)
            {
                writer.Indent--;
                writer.WriteLine("}"); // end container
            }

            if (m_namespace != null)
            {
                writer.Indent--;
                writer.WriteLine("}"); // end namespace
            }
        }
    }
}