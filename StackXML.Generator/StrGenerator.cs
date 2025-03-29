using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace StackXML.Generator
{
    [Generator]
    public class StrGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        private class ClassGenInfo
        {
            public readonly INamedTypeSymbol m_symbol;
            public readonly List<FieldGenInfo> m_fields = new List<FieldGenInfo>();

            public ClassGenInfo(INamedTypeSymbol symbol)
            {
                m_symbol = symbol;
            }
        }

        private class FieldGenInfo
        {
            public readonly IFieldSymbol m_field;
            public readonly int? m_group;
            public readonly string m_defaultValue;

            public FieldGenInfo(IFieldSymbol fieldSymbol, int? group,
                VariableDeclaratorSyntax variableDeclaratorSyntax)
            {
                m_field = fieldSymbol;
                m_group = group;
                m_defaultValue = variableDeclaratorSyntax?.Initializer?.Value.ToString();
            }
        }

        public void Execute(GeneratorExecutionContext context)
        {
            try
            {
                ExecuteInternal(context);
            } catch (Exception e)
            {
                var descriptor = new DiagnosticDescriptor(nameof(StrGenerator), "Error", e.ToString(), "Error", DiagnosticSeverity.Error, true);
                var diagnostic = Diagnostic.Create(descriptor, Location.None);
                context.ReportDiagnostic(diagnostic);
            }
        }

        public void ExecuteInternal(GeneratorExecutionContext context)
        {
            if (!(context.SyntaxReceiver is SyntaxReceiver receiver))
                return;
            
            var compilation = context.Compilation;
            
            var attributeSymbol = compilation.GetTypeByMetadataName("StackXML.Str.StrField");
            var groupAttributeSymbol = compilation.GetTypeByMetadataName("StackXML.Str.StrOptionalAttribute");

            Dictionary<INamedTypeSymbol, ClassGenInfo> classes = new Dictionary<INamedTypeSymbol, ClassGenInfo>(SymbolEqualityComparer.Default);
            
            foreach (var field in receiver.m_candidateFields)
            {
                var model = compilation.GetSemanticModel(field.SyntaxTree);
                foreach (var variable in field.Declaration.Variables)
                {
                    var fieldSymbol = ModelExtensions.GetDeclaredSymbol(model, variable) as IFieldSymbol;
                    if (fieldSymbol == null) continue;

                    var fieldAttr = fieldSymbol.GetAttributes().SingleOrDefault(ad => ad.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default));
                    var groupAttr = fieldSymbol.GetAttributes().SingleOrDefault(ad => ad.AttributeClass.Equals(groupAttributeSymbol, SymbolEqualityComparer.Default));

                    if (fieldAttr == null) continue;
                    if (!classes.TryGetValue(fieldSymbol.ContainingType, out var classInfo))
                    {
                        classInfo = new ClassGenInfo(fieldSymbol.ContainingType);
                        classes[fieldSymbol.ContainingType] = classInfo;
                    }

                    int? group = null;
                    if (groupAttr != null)
                    {
                        @group = (int)groupAttr.ConstructorArguments[0].Value;
                    }
                    classInfo.m_fields.Add(new FieldGenInfo(fieldSymbol, @group, variable));
                }
            }

            foreach (KeyValuePair<INamedTypeSymbol,ClassGenInfo> info in classes)
            {
                var classSource = ProcessClass(info.Value.m_symbol, info.Value, classes);
                context.AddSource($"{nameof(StrGenerator)}_{info.Value.m_symbol.Name}.cs", SourceText.From(classSource, Encoding.UTF8));
            }
        }
        
        private string ProcessClass(INamedTypeSymbol classSymbol, ClassGenInfo classGenInfo, Dictionary<INamedTypeSymbol, ClassGenInfo> classes)
        {
            var writer = new IndentedTextWriter(new StringWriter(), "    ");
            writer.WriteLine("using StackXML;");
            writer.WriteLine("using StackXML.Str;");
            writer.WriteLine();
            
            var scope = new NestedScope(classSymbol);
            scope.Start(writer);
            writer.WriteLine(NestedScope.GetClsString(classSymbol));
            writer.WriteLine("{");
            writer.Indent++;
            
            WriteDeserializeMethod(writer, classGenInfo, classes);
            WriteSerializeMethod(writer, classGenInfo, classes);

            writer.Indent--;
            writer.WriteLine("}");
            scope.End(writer);
            
            return writer.InnerWriter.ToString();
        }

        private void WriteDeserializeMethod(IndentedTextWriter writer, ClassGenInfo classGenInfo, Dictionary<INamedTypeSymbol, ClassGenInfo> classes)
        {
            writer.WriteLine($"public void Deserialize(ref StrReader reader)");
            writer.WriteLine("{");
            writer.Indent++;
            
            HashSet<int> groupsStarted = new HashSet<int>();
            int? currentGroup = null;
            foreach (var VARIABLE in classGenInfo.m_fields)
            {
                if (currentGroup != VARIABLE.m_group)
                {
                    if (currentGroup != null)
                    {
                        writer.Indent--;
                        writer.WriteLine("}");
                    }

                    if (VARIABLE.m_group != null)
                    {
                        const string c_conditionName = "read";

                        if (groupsStarted.Add(VARIABLE.m_group.Value))
                        {
                            writer.WriteLine($"var {c_conditionName}{VARIABLE.m_group.Value} = reader.HasRemaining();");
                        }

                        writer.WriteLine($"if ({c_conditionName}{VARIABLE.m_group.Value})");
                        writer.WriteLine("{");
                        writer.Indent++;
                    }

                    currentGroup = VARIABLE.m_group;
                }

                var typeToRead = (INamedTypeSymbol)VARIABLE.m_field.Type;
                ExtractNullable(ref typeToRead); // don't need to do anything special to assign to nullable if it is
                if (classes.ContainsKey(typeToRead))
                {
                    // todo: doesn't support other compilations
                    writer.WriteLine($"{VARIABLE.m_field.Name} = new {typeToRead.Name}();");
                    writer.WriteLine($"{VARIABLE.m_field.Name}.Deserialize(ref reader);");
                } else
                {
                    var reader = GetReaderForType(typeToRead.Name);
                    writer.WriteLine($"{VARIABLE.m_field.Name} = {reader};");
                }
            }

            if (currentGroup != null)
            {
                writer.Indent--;
                writer.WriteLine("}");
            }

            writer.Indent--;
            writer.WriteLine("}");
        }

        private void WriteSerializeMethod(IndentedTextWriter writer, ClassGenInfo classGenInfo, Dictionary<INamedTypeSymbol, ClassGenInfo> classes)
        {
            writer.WriteLine($"public void Serialize(ref StrWriter writer)");
            writer.WriteLine("{");
            writer.Indent++;
            {
                HashSet<int> allGroups = new HashSet<int>();
                foreach (var VARIABLE in classGenInfo.m_fields)
                {
                    if (VARIABLE.m_group != null) allGroups.Add(VARIABLE.m_group.Value);
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
                            boolOrs.Add($"{field.m_field.Name} != {field.m_defaultValue}");
                        } else
                        {
                            boolOrs.Add($"{field.m_field.Name} != default");
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
                foreach (var VARIABLE in classGenInfo.m_fields)
                {
                    if (currentGroup != VARIABLE.m_group)
                    {
                        if (currentGroup != null)
                        {
                            writer.Indent--;
                            writer.WriteLine("}");
                        }
                        if (VARIABLE.m_group != null)
                        {
                            writer.WriteLine($"if ({c_conditionName}{VARIABLE.m_group.Value})");
                            writer.WriteLine("{");
                            writer.Indent++;
                        }
                        currentGroup = VARIABLE.m_group;
                    }
                    
                    var typeToWrite = (INamedTypeSymbol)VARIABLE.m_field.Type;
                    var toWrite = VARIABLE.m_field.Name;
                    if (ExtractNullable(ref typeToWrite))
                    {
                        toWrite += ".Value";
                    }
                    if (classes.ContainsKey(typeToWrite))
                    {
                        // todo: doesn't support other compilations
                        writer.WriteLine($"{toWrite}.Serialize(ref writer);");
                    } else
                    {
                        var writerFunc = GetWriterForType(typeToWrite.Name, toWrite);
                        writer.WriteLine($"{writerFunc};");
                    }
                }
                if (currentGroup != null)
                {
                    writer.Indent--;
                    writer.WriteLine("}");
                }
            }
            writer.Indent--;
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
                "Int32" => $"writer.PutInt({toWrite})",
                "Double" => $"writer.PutDouble({toWrite})",
                "String" => $"writer.PutString({toWrite})",
                "ReadOnlySpan" => $"writer.PutString({toWrite})", // todo: ReadOnlySpan<char> only...
                "SpanStr" => $"writer.PutString({toWrite})",
                _ => throw new NotImplementedException($"GetWriterForType: {type}")
            };
            return result;
        }
        
        public static string GetReaderForType(string type)
        {
            var result = type switch
            {
                "Int32" => "reader.GetInt()",
                "Double" => "reader.GetDouble()",
                "String" => "reader.GetString().ToString()",
                "ReadOnlySpan" => "reader.GetString()", // todo: ReadOnlySpan<char> only...
                "SpanStr" => "reader.GetSpanString()",
                _ => throw new NotImplementedException($"GetReaderForType: {type}")
            };
            return result;
        }
        
        private class SyntaxReceiver : ISyntaxReceiver
        {
            public List<FieldDeclarationSyntax> m_candidateFields { get; } = new List<FieldDeclarationSyntax>();
            
            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is FieldDeclarationSyntax fieldDeclarationSyntax && fieldDeclarationSyntax.AttributeLists.Count > 0)
                {
                    m_candidateFields.Add(fieldDeclarationSyntax);
                }
            }
        }
    }
}