// This file is ported from ComputeSharp (Sergio0694/ComputeSharp),
// see LICENSE in ComputeSharp directory

using System;
using ComputeSharp.SourceGeneration.Helpers;
using Microsoft.CodeAnalysis;

namespace ComputeSharp.SourceGeneration.Extensions;

/// <summary>
/// Extension methods for <see cref="ITypeSymbol"/> types.
/// </summary>
internal static class ITypeSymbolExtensions
{
    /// <summary>
    /// Gets the method of this symbol that have a particular name.
    /// </summary>
    /// <param name="symbol">The input <see cref="ITypeSymbol"/> instance to check.</param>
    /// <param name="name">The name of the method to find.</param>
    /// <returns>The target method, if present.</returns>
    public static IMethodSymbol? GetMethod(this ITypeSymbol symbol, string name)
    {
        foreach (ISymbol memberSymbol in symbol.GetMembers(name))
        {
            if (memberSymbol is IMethodSymbol methodSymbol &&
                memberSymbol.Name == name)
            {
                return methodSymbol;
            }
        }

        return null;
    }

    /// <summary>
    /// Checks whether or not a given type symbol has a specified fully qualified metadata name.
    /// </summary>
    /// <param name="symbol">The input <see cref="ITypeSymbol"/> instance to check.</param>
    /// <param name="name">The full name to check.</param>
    /// <returns>Whether <paramref name="symbol"/> has a full name equals to <paramref name="name"/>.</returns>
    public static bool HasFullyQualifiedMetadataName(this ITypeSymbol symbol, string name)
    {
        using ImmutableArrayBuilder<char> builder = new();

        symbol.AppendFullyQualifiedMetadataName(in builder);

        return builder.WrittenSpan.SequenceEqual(name.AsSpan());
    }

    /// <summary>
    /// Checks whether or not a given <see cref="ITypeSymbol"/> inherits from a specified type.
    /// </summary>
    /// <param name="typeSymbol">The target <see cref="ITypeSymbol"/> instance to check.</param>
    /// <param name="name">The full name of the type to check for inheritance.</param>
    /// <returns>Whether or not <paramref name="typeSymbol"/> inherits from <paramref name="name"/>.</returns>
    public static bool InheritsFromFullyQualifiedMetadataName(this ITypeSymbol typeSymbol, string name)
    {
        INamedTypeSymbol? baseType = typeSymbol.BaseType;

        while (baseType is not null)
        {
            if (baseType.HasFullyQualifiedMetadataName(name))
            {
                return true;
            }

            baseType = baseType.BaseType;
        }

        return false;
    }

    /// <summary>
    /// Checks whether or not a given <see cref="ITypeSymbol"/> inherits from a specified type.
    /// </summary>
    /// <param name="typeSymbol">The target <see cref="ITypeSymbol"/> instance to check.</param>
    /// <param name="baseTypeSymbol">The <see cref="ITypeSymbol"/> instane to check for inheritance from.</param>
    /// <returns>Whether or not <paramref name="typeSymbol"/> inherits from <paramref name="baseTypeSymbol"/>.</returns>
    public static bool InheritsFromType(this ITypeSymbol typeSymbol, ITypeSymbol baseTypeSymbol)
    {
        INamedTypeSymbol? currentBaseTypeSymbol = typeSymbol.BaseType;

        while (currentBaseTypeSymbol is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(currentBaseTypeSymbol, baseTypeSymbol))
            {
                return true;
            }

            currentBaseTypeSymbol = currentBaseTypeSymbol.BaseType;
        }

        return false;
    }

    /// <summary>
    /// Checks whether or not a given <see cref="ITypeSymbol"/> implements an interface of a specified type.
    /// </summary>
    /// <param name="typeSymbol">The target <see cref="ITypeSymbol"/> instance to check.</param>
    /// <param name="interfaceSymbol">The <see cref="ITypeSymbol"/> instance to check for inheritance from.</param>
    /// <returns>Whether or not <paramref name="typeSymbol"/> has an interface of type <paramref name="interfaceSymbol"/>.</returns>
    public static bool HasInterfaceWithType(this ITypeSymbol typeSymbol, ITypeSymbol interfaceSymbol)
    {
        foreach (INamedTypeSymbol interfaceType in typeSymbol.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(interfaceType, interfaceSymbol))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the fully qualified metadata name for a given <see cref="ITypeSymbol"/> instance.
    /// </summary>
    /// <param name="symbol">The input <see cref="ITypeSymbol"/> instance.</param>
    /// <returns>The fully qualified metadata name for <paramref name="symbol"/>.</returns>
    public static string GetFullyQualifiedMetadataName(this ITypeSymbol symbol)
    {
        using ImmutableArrayBuilder<char> builder = new();

        symbol.AppendFullyQualifiedMetadataName(in builder);

        return builder.ToString();
    }

    /// <summary>
    /// Appends the fully qualified metadata name for a given symbol to a target builder.
    /// </summary>
    /// <param name="symbol">The input <see cref="ITypeSymbol"/> instance.</param>
    /// <param name="builder">The target <see cref="ImmutableArrayBuilder{T}"/> instance.</param>
    public static void AppendFullyQualifiedMetadataName(this ITypeSymbol symbol, ref readonly ImmutableArrayBuilder<char> builder)
    {
        static void BuildFrom(ISymbol? symbol, ref readonly ImmutableArrayBuilder<char> builder)
        {
            switch (symbol)
            {
                // Namespaces that are nested also append a leading '.'
                case INamespaceSymbol { ContainingNamespace.IsGlobalNamespace: false }:
                    BuildFrom(symbol.ContainingNamespace, in builder);
                    builder.Add('.');
                    builder.AddRange(symbol.MetadataName.AsSpan());
                    break;

                // Other namespaces (ie. the one right before global) skip the leading '.'
                case INamespaceSymbol { IsGlobalNamespace: false }:
                    builder.AddRange(symbol.MetadataName.AsSpan());
                    break;

                // Types with no namespace just have their metadata name directly written
                case ITypeSymbol { ContainingSymbol: INamespaceSymbol { IsGlobalNamespace: true } }:
                    builder.AddRange(symbol.MetadataName.AsSpan());
                    break;

                // Types with a containing non-global namespace also append a leading '.'
                case ITypeSymbol { ContainingSymbol: INamespaceSymbol namespaceSymbol }:
                    BuildFrom(namespaceSymbol, in builder);
                    builder.Add('.');
                    builder.AddRange(symbol.MetadataName.AsSpan());
                    break;

                // Nested types append a leading '+'
                case ITypeSymbol { ContainingSymbol: ITypeSymbol typeSymbol }:
                    BuildFrom(typeSymbol, in builder);
                    builder.Add('+');
                    builder.AddRange(symbol.MetadataName.AsSpan());
                    break;
                default:
                    break;
            }
        }

        BuildFrom(symbol, in builder);
    }
}