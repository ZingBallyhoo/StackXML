using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using ComputeSharp.SourceGeneration.Extensions;
using ComputeSharp.SourceGeneration.Helpers;
using ComputeSharp.SourceGeneration.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StackXML.Generator
{
    [Generator]
    [SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1035:Do not use APIs banned for analyzers")]
    public class StrGenerator : IIncrementalGenerator
    {
        private record ClassGenInfo
        {
            public required HierarchyInfo m_hierarchy;
            public EquatableArray<FieldGenInfo> m_fields;
        }

        private record FieldGenInfo
        {
            public readonly HierarchyInfo m_ownerHierarchy;
            public readonly int? m_group;
            public readonly string? m_defaultValue;
            
            public readonly string m_fieldName;
            public readonly string m_typeShortName;
            public readonly string m_typeQualifiedName;
            public readonly bool m_isNullable;
            public readonly bool m_isStrBody;

            public FieldGenInfo(IFieldSymbol fieldSymbol, VariableDeclaratorSyntax variableDeclaratorSyntax)
            {
                m_ownerHierarchy = HierarchyInfo.From(fieldSymbol.ContainingType);
                m_defaultValue = variableDeclaratorSyntax.Initializer?.Value.ToString();
                
                m_fieldName = fieldSymbol.Name;
                
                var type = (INamedTypeSymbol)fieldSymbol.Type;
                m_isNullable = ExtractNullable(ref type);
                
                m_typeShortName = type.Name;
                m_typeQualifiedName = type.GetFullyQualifiedName();
                
                if (fieldSymbol.TryGetAttributeWithFullyQualifiedMetadataName("StackXML.Str.StrOptionalAttribute", out var optionalAttribute))
                {
                    m_group = (int)optionalAttribute.ConstructorArguments[0].Value!;
                }

                foreach (var member in type.GetMembers())
                {
                    if (member is not IFieldSymbol childFieldSymbol)
                    {
                        continue;
                    }

                    if (!childFieldSymbol.TryGetAttributeWithFullyQualifiedMetadataName("StackXML.Str.StrField", out _))
                    {
                        continue;
                    }
                    
                    m_isStrBody = true;
                    break;
                }
            }
        }
        
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var fieldDeclarations = context.SyntaxProvider.ForAttributeWithMetadataName(
                "StackXML.Str.StrField",
                (syntaxNode, _) => syntaxNode is VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax { Parent: FieldDeclarationSyntax { Parent: TypeDeclarationSyntax, AttributeLists.Count: > 0 } } },
                TransformField);
            
            // group by containing type
            var typeDeclarations = fieldDeclarations.GroupBy(static x => x.m_ownerHierarchy, static x => x).Select((x, token) => new ClassGenInfo
            {
                m_hierarchy = x.Key,
                m_fields = x.Right
            });
            
            context.RegisterSourceOutput(typeDeclarations, Process);
        }

        private static FieldGenInfo TransformField(GeneratorAttributeSyntaxContext context, CancellationToken token)
        {
            return new FieldGenInfo((IFieldSymbol)context.TargetSymbol, (VariableDeclaratorSyntax)context.TargetNode);
        }
        
        private static void Process(SourceProductionContext productionContext, ClassGenInfo classGenInfo)
        {
            using var w = new IndentedTextWriter();
            
            w.WriteLine("using StackXML;");
            w.WriteLine("using StackXML.Str;");
            w.WriteLine();
            
            classGenInfo.m_hierarchy.WriteSyntax(classGenInfo, w, ["IStrClass"], [ProcessClass]);
            productionContext.AddSource($"{classGenInfo.m_hierarchy.FullyQualifiedMetadataName}.cs", w.ToString());
        }
        
        private static void ProcessClass(ClassGenInfo classGenInfo, IndentedTextWriter writer)
        {
            WriteDeserializeMethod(classGenInfo, writer);
            writer.WriteLine();
            WriteSerializeMethod(classGenInfo, writer);
        }

        private static void WriteDeserializeMethod(ClassGenInfo classGenInfo, IndentedTextWriter writer)
        {
            writer.WriteLine("public void Deserialize(ref StrReader reader)");
            writer.WriteLine("{");
            writer.IncreaseIndent();
            
            HashSet<int> groupsStarted = new HashSet<int>();
            int? currentGroup = null;
            foreach (var field in classGenInfo.m_fields)
            {
                if (currentGroup != field.m_group)
                {
                    if (currentGroup != null)
                    {
                        writer.DecreaseIndent();
                        writer.WriteLine("}");
                    }

                    if (field.m_group != null)
                    {
                        const string c_conditionName = "read";

                        if (groupsStarted.Add(field.m_group.Value))
                        {
                            writer.WriteLine($"var {c_conditionName}{field.m_group.Value} = reader.HasRemaining();");
                        }

                        writer.WriteLine($"if ({c_conditionName}{field.m_group.Value})");
                        writer.WriteLine("{");
                        writer.IncreaseIndent();
                    }

                    currentGroup = field.m_group;
                }
                
                if (field.m_isStrBody)
                {
                    writer.WriteLine($"{field.m_fieldName} = new {field.m_typeQualifiedName}();");
                    writer.WriteLine($"{field.m_fieldName}.Deserialize(ref reader);");
                } else
                {
                    var reader = GetReaderForType(field.m_typeShortName, field.m_typeQualifiedName);
                    writer.WriteLine($"{field.m_fieldName} = {reader};");
                }
            }

            if (currentGroup != null)
            {
                writer.DecreaseIndent();
                writer.WriteLine("}");
            }

            writer.DecreaseIndent();
            writer.WriteLine("}");
        }

        private static void WriteSerializeMethod(ClassGenInfo classGenInfo, IndentedTextWriter writer)
        {
            writer.WriteLine("public void Serialize(ref StrWriter writer)");
            writer.WriteLine("{");
            writer.IncreaseIndent();
            {
                HashSet<int> allGroups = new HashSet<int>();
                foreach (var field in classGenInfo.m_fields)
                {
                    if (field.m_group != null) allGroups.Add(field.m_group.Value);
                }

                const string c_conditionName = "doGroup";
                
                HashSet<int> setupGroups = new HashSet<int>();
                List<string> groupConditions = new List<string>();
                foreach (var field in classGenInfo.m_fields)
                {
                    if (field.m_group != null && setupGroups.Add(field.m_group.Value))
                    {
                        List<string> boolOrs = new List<string>();
                        foreach (var existingGroup in allGroups)
                        {
                            if (existingGroup <= field.m_group) continue;
                            boolOrs.Add($"{c_conditionName}{existingGroup}");
                        }

                        if (field.m_defaultValue != null)
                        {
                            boolOrs.Add($"{field.m_fieldName} != {field.m_defaultValue}");
                        } else
                        {
                            boolOrs.Add($"{field.m_fieldName} != default");
                        }
                        groupConditions.Add($"bool {c_conditionName}{field.m_group} = {string.Join(" || ", boolOrs)};");
                    }
                }

                groupConditions.Reverse();
                foreach (var condition in groupConditions)
                {
                    writer.WriteLine(condition);
                }
                
                int? currentGroup = null;
                foreach (var field in classGenInfo.m_fields)
                {
                    if (currentGroup != field.m_group)
                    {
                        if (currentGroup != null)
                        {
                            writer.DecreaseIndent();
                            writer.WriteLine("}");
                        }
                        if (field.m_group != null)
                        {
                            writer.WriteLine($"if ({c_conditionName}{field.m_group.Value})");
                            writer.WriteLine("{");
                            writer.IncreaseIndent();
                        }
                        currentGroup = field.m_group;
                    }
                    
                    var toWrite = field.m_fieldName;
                    if (field.m_isNullable)
                    {
                        toWrite += ".Value";
                    }
                    if (field.m_isStrBody)
                    {
                        writer.WriteLine($"{toWrite}.Serialize(ref writer);");
                    } else
                    {
                        var writerFunc = GetWriterForType(field.m_fieldName, toWrite);
                        writer.WriteLine($"{writerFunc};");
                    }
                }
                if (currentGroup != null)
                {
                    writer.DecreaseIndent();
                    writer.WriteLine("}");
                }
            }
            writer.DecreaseIndent();
            writer.WriteLine("}");
        }

        private static bool ExtractNullable(ref INamedTypeSymbol type)
        {
            if (type.Name != "Nullable") return false;
            type = (INamedTypeSymbol)type.TypeArguments[0];
            return true;
        }

        public static string GetWriterForType(string type, string toWrite)
        {
            var result = type switch
            {
                _ => $"writer.Put({toWrite})"
            };
            return result;
        }
        
        public static string GetReaderForType(string shortName, string qualifiedName)
        {
            var result = shortName switch
            {
                "String" => "reader.GetString().ToString()",
                "ReadOnlySpan" => "reader.GetString()", // todo: ReadOnlySpan<char> only...
                "SpanStr" => "reader.GetSpanString()",
                _ => $"reader.Get<{qualifiedName}>()"
            };
            return result;
        }
    }
}