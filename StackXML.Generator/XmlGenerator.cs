using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using ComputeSharp.SourceGeneration.Extensions;
using ComputeSharp.SourceGeneration.Helpers;
using ComputeSharp.SourceGeneration.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StackXML.Generator
{
    [Generator]
    public class XmlGenerator : IIncrementalGenerator
    {
        private record ClassGenInfo
        {
            public required string m_shortName;
            public required HierarchyInfo m_hierarchy;
            public required bool m_hasBaseType;

            public required string? m_className;
            
            public EquatableArray<FieldGenInfo> m_fields;
            public EquatableArray<FieldGenInfo> m_bodies;
        }

        private record FieldGenInfo
        {
            public required string m_fieldName;
            public required string? m_xmlName;
            
            public required string m_shortTypeName;
            public required string m_qualifiedTypeName;
            
            public required bool m_isValueType;
            public required bool m_isList;
            
            public required string? m_elementTypeShortName;
            public required string? m_elementTypeQualifiedName;
            

            public char? m_splitChar;
            
            public bool IsList() => m_isList;
            public bool IsString() => m_shortTypeName == "String";
            public bool IsPrimitive() => m_isValueType || IsString();
        }
        
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var typeDeclarations = context.SyntaxProvider.ForAttributeWithMetadataName(
                "StackXML.XmlCls",
                (syntaxNode, _) => syntaxNode is ClassDeclarationSyntax { AttributeLists.Count: > 0 }, TransformClass);
            
            context.RegisterSourceOutput(typeDeclarations, Process);
        }

        private static ClassGenInfo TransformClass(GeneratorAttributeSyntaxContext syntaxContext, CancellationToken token)
        {
            var typeSymbol = (INamedTypeSymbol)syntaxContext.TargetSymbol;

            var classInfo = new ClassGenInfo
            {
                m_shortName =  typeSymbol.Name,
                m_hierarchy = HierarchyInfo.From(typeSymbol), 
                m_className = syntaxContext.Attributes[0].ConstructorArguments[0].Value?.ToString(),
                m_hasBaseType = typeSymbol.BaseType!.GetFullyQualifiedMetadataName() != "System.Object"
            };

            var bodies = new List<FieldGenInfo>();
            var fields = new List<FieldGenInfo>();

            foreach (var member in typeSymbol.GetMembers())
            {
                if (member is not IFieldSymbol fieldSymbol)
                {
                    continue;
                }

                var isField = member.TryGetAttributeWithFullyQualifiedMetadataName("StackXML.XmlField", out var xmlFieldAttribute);
                var isBody = member.TryGetAttributeWithFullyQualifiedMetadataName("StackXML.XmlBody", out var xmlBodyAttribute);
                if (!isField && !isBody)
                {
                    continue;
                }
                
                var isList = fieldSymbol.Type.GetFullyQualifiedMetadataName() == "System.Collections.Generic.List`1";
                var elementType = fieldSymbol.Type;
                if (isList)
                {
                    elementType = ((INamedTypeSymbol)fieldSymbol.Type).TypeArguments[0];
                }

                string? xmlName;
                if (isField)
                {
                    xmlName = xmlFieldAttribute!.ConstructorArguments[0].Value!.ToString();
                }
                else
                {
                    // body

                    xmlName = xmlBodyAttribute!.ConstructorArguments[0].Value?.ToString();
                    if (xmlName == null && elementType.TryGetAttributeWithFullyQualifiedMetadataName("StackXML.XmlCls", out var innerClsAttribute))
                    {
                        // todo: does this break incremental?
                        xmlName = innerClsAttribute.ConstructorArguments[0].Value?.ToString();
                    }
                }

                var memberInfo = new FieldGenInfo
                {
                    m_fieldName = fieldSymbol.Name,
                    m_xmlName = xmlName,
                    
                    m_shortTypeName = fieldSymbol.Type.Name,
                    m_qualifiedTypeName = fieldSymbol.Type.GetFullyQualifiedName(),
                    
                    m_elementTypeShortName = elementType.Name,
                    m_elementTypeQualifiedName = elementType.GetFullyQualifiedName(),
                    
                    m_isValueType = elementType.IsValueType,
                    m_isList = isList
                };

                if (member.TryGetAttributeWithFullyQualifiedMetadataName("StackXML.XmlSplitStr", out var xmlSplitStrAttribute))
                {
                    memberInfo.m_splitChar = (char)xmlSplitStrAttribute.ConstructorArguments[0].Value!;
                }

                if (isField)
                {
                    fields.Add(memberInfo);
                }
                else
                {
                    bodies.Add(memberInfo);
                }
            }

            classInfo.m_fields = fields.ToImmutableArray().AsEquatableArray();
            classInfo.m_bodies = bodies.ToImmutableArray().AsEquatableArray();
            return classInfo;
        }
        
        private static void Process(SourceProductionContext productionContext, ClassGenInfo classGenInfo)
        {
            using var w = new IndentedTextWriter();

            w.WriteLine("using System;");
            w.WriteLine("using System.IO;");
            w.WriteLine("using System.Collections.Generic;");
            w.WriteLine("using StackXML;");
            w.WriteLine("using StackXML.Str;");
            w.WriteLine();

            string[] baseTypes = [];
            if (!classGenInfo.m_hasBaseType)
            {
                baseTypes = ["IXmlSerializable"];
            }

            var callbacks = new List<IndentedTextWriter.Callback<ClassGenInfo>>();
            if (classGenInfo.m_className != null)
            {
                callbacks.Add(Process_GetClassName);
            }
            if (classGenInfo.m_fields.Length > 0)
            {
                callbacks.Add(Process_Fields);
            }
            if (classGenInfo.m_bodies.Length > 0)
            {
                callbacks.Add(Process_Bodies);
            }

            classGenInfo.m_hierarchy.WriteSyntax(classGenInfo, w, baseTypes, callbacks.ToArray());
            productionContext.AddSource($"{classGenInfo.m_hierarchy.FullyQualifiedMetadataName}.cs", w.ToString());
        }
        
        private static void Process_GetClassName(ClassGenInfo classGenInfo, IndentedTextWriter writer)
        {
            writer.WriteLine("public override ReadOnlySpan<char> GetNodeName()");
            writer.WriteLine("{");
            writer.IncreaseIndent();
            writer.WriteLine($"return \"{classGenInfo.m_className}\";");
            writer.DecreaseIndent();
            writer.WriteLine("}");
        }
        
        private static void Process_Fields(ClassGenInfo classGenInfo, IndentedTextWriter writer)
        {
            WriteAttrParseMethod(classGenInfo, writer);
            writer.WriteLine();
            WriteAttSerializeMethod(classGenInfo, writer);
        }
        
        private static void Process_Bodies(ClassGenInfo classGenInfo, IndentedTextWriter writer)
        {
            WriteParseBodyMethods(classGenInfo, writer);
            writer.WriteLine();
            WriteSerializeBodyMethod(classGenInfo, writer);
        }
        
        
        private static void WriteAttrParseMethod(ClassGenInfo classGenInfo, IndentedTextWriter writer)
        {
            writer.WriteLine("public override bool ParseAttribute(ref XmlReadBuffer buffer, ReadOnlySpan<char> name, ReadOnlySpan<char> value)");
            writer.WriteLine("{");
            writer.IncreaseIndent();
            writer.WriteLine("if (base.ParseAttribute(ref buffer, name, value)) return true;");
            
            writer.WriteLine("switch (name)");
            writer.WriteLine("{");
            writer.IncreaseIndent();

            foreach (var field in classGenInfo.m_fields.OrderBy(x => x.m_xmlName!.Length))
            {
                writer.WriteLine($"case \"{field.m_xmlName}\": {{");
                writer.IncreaseIndent();

                if (field.m_splitChar != null)
                {
                    writer.WriteLine($"var lst = new {field.m_qualifiedTypeName}();");
                    writer.WriteLine($"var reader = new StrReader(value, '{field.m_splitChar}', buffer.m_params.m_stringParser);");
                    var readerMethod = StrGenerator.GetReaderForType(field.m_elementTypeShortName!, field.m_elementTypeQualifiedName!);

                    writer.WriteLine("while (reader.HasRemaining())");
                    writer.WriteLine("{");
                    writer.IncreaseIndent();
                    writer.WriteLine($"lst.Add({readerMethod});");
                    writer.DecreaseIndent();
                    writer.WriteLine("}");

                    writer.WriteLine($"this.{field.m_fieldName} = lst;");
                } else
                {
                    var readCommand = GetParseAttributeAction(field);
                    writer.WriteLine($"this.{field.m_fieldName} = {readCommand};");
                }

                writer.WriteLine("return true;");
                writer.DecreaseIndent();
                writer.WriteLine("}");
            }

            writer.DecreaseIndent();
            writer.WriteLine("}");

            writer.WriteLine("return false;");
            writer.DecreaseIndent();
            writer.WriteLine("}");
        }

        private static void WriteAttSerializeMethod(ClassGenInfo classGenInfo, IndentedTextWriter writer)
        {
            writer.WriteLine("public override void SerializeAttributes(ref XmlWriteBuffer buffer)");
            writer.WriteLine("{");
            writer.IncreaseIndent();
            writer.WriteLine("base.SerializeAttributes(ref buffer);");
            foreach (var field in classGenInfo.m_fields)
            {
                if (field.m_splitChar != null)
                {
                    writer.WriteLine("{");
                    writer.IncreaseIndent();

                    writer.WriteLine($"using var writer = new StrWriter('{field.m_splitChar}', buffer.m_params.m_stringFormatter);");
                    writer.WriteLine($"foreach (var val in {field.m_fieldName})");
                    writer.WriteLine("{");
                    writer.IncreaseIndent();
                    writer.WriteLine($"{StrGenerator.GetWriterForType(field.m_elementTypeShortName!, "val")};");
                    writer.DecreaseIndent();
                    writer.WriteLine("}");
                    writer.WriteLine($"buffer.PutAttribute(\"{field.m_xmlName}\", writer.m_builtSpan);");
                    writer.WriteLine("writer.Dispose();");

                    writer.DecreaseIndent();
                    writer.WriteLine("}");
                } else
                {
                    var writerAction = GetPutAttributeAction(field);
                    writer.WriteLine(writerAction);
                }
            }

            writer.DecreaseIndent();
            writer.WriteLine("}");
        }

        private static string GetPutAttributeAction(FieldGenInfo field)
        {
            var writerAction = field.m_elementTypeShortName switch
            {
                _ => $"buffer.PutAttribute(\"{field.m_xmlName}\", {field.m_fieldName});"
            };
            return writerAction;
        }
        
        private static string GetParseAttributeAction(FieldGenInfo field)
        {
            var readCommand = field.m_shortTypeName switch
            {
                "String" => "value.ToString()",
                _ => $"buffer.m_params.m_stringParser.Parse<{field.m_qualifiedTypeName}>(value)"
            };
            return readCommand;
        }
        
        private static void WriteParseBodyMethods(ClassGenInfo classGenInfo, IndentedTextWriter writer)
        {
            var needsInlineBody = false;
            var needsSubBody = false;

            foreach (var field in classGenInfo.m_bodies)
            {
                if (field.IsPrimitive() && field.m_xmlName == null)
                {
                    needsInlineBody = true;
                } else
                {
                    needsSubBody = true;
                }
            }

            if (needsInlineBody && needsSubBody)
                throw new Exception($"{classGenInfo.m_hierarchy.FullyQualifiedMetadataName} needs inline body and sub body");

            if (needsInlineBody)
            {
                writer.WriteLine("public override bool ParseFullBody(ref XmlReadBuffer buffer, ReadOnlySpan<char> bodySpan, ref int end)");
                writer.WriteLine("{");
                writer.IncreaseIndent();
                foreach (var body in classGenInfo.m_bodies)
                {
                    if (body.IsString())
                    {
                        writer.WriteLine($"{body.m_fieldName} = buffer.DeserializeCDATA(bodySpan, out end).ToString();");
                    } else
                    {
                        throw new NotImplementedException($"Xml:WriteParseBodyMethods: how to inline body {body.m_qualifiedTypeName}");
                    }
                }
                writer.WriteLine("return true;");
                writer.DecreaseIndent();
                writer.WriteLine("}");
            } else if (needsSubBody)
            {
                WriteListConstructor(classGenInfo, writer);
                writer.WriteLine();
                WriteParseSubBodyMethod(classGenInfo, writer);
            }
        }

        private static void WriteParseSubBodyMethod(ClassGenInfo classGenInfo, IndentedTextWriter writer)
        {
            writer.WriteLine("public override bool ParseSubBody(ref XmlReadBuffer buffer, ReadOnlySpan<char> name, ReadOnlySpan<char> bodySpan, ReadOnlySpan<char> innerBodySpan, ref int end, ref int endInner)");
            writer.WriteLine("{");
            writer.IncreaseIndent();
            writer.WriteLine("if (base.ParseSubBody(ref buffer, name, bodySpan, innerBodySpan, ref end, ref endInner)) return true;");
            
            writer.WriteLine("switch (name)");
            writer.WriteLine("{");
            writer.IncreaseIndent();
            
            foreach (var field in classGenInfo.m_bodies)
            {
                var nameToCheck = field.m_xmlName ?? throw new InvalidDataException("no body name??");
                
                writer.WriteLine($"case \"{nameToCheck}\": {{");
                writer.IncreaseIndent();

                if (field.IsString())
                {
                    writer.WriteLine($"{field.m_fieldName} = buffer.DeserializeCDATA(innerBodySpan, out endInner).ToString();");
                } else if (field.IsList())
                {
                    writer.WriteLine($"{field.m_fieldName}.Add(buffer.Read<{field.m_elementTypeQualifiedName}>(bodySpan, out end));");
                } else
                {
                    writer.WriteLine($"if ({field.m_fieldName} != null) throw new InvalidDataException(\"duplicate non-list body {nameToCheck}\");");
                    writer.WriteLine($"{field.m_fieldName} = buffer.Read<{field.m_elementTypeQualifiedName}>(bodySpan, out end);");
                }
                
                writer.WriteLine("return true;");
                writer.DecreaseIndent();
                writer.WriteLine("}");
            }
            
            writer.DecreaseIndent();
            writer.WriteLine("}");

            writer.WriteLine("return false;");
            writer.DecreaseIndent();
            writer.WriteLine("}");
        }

        private static void WriteListConstructor(ClassGenInfo classGenInfo, IndentedTextWriter writer)
        {
            writer.WriteLine($"public {classGenInfo.m_shortName}()");
            writer.WriteLine("{");
            writer.IncreaseIndent();
            foreach (var body in classGenInfo.m_bodies)
            {
                if (!body.IsList()) continue;
                writer.WriteLine($"{body.m_fieldName} = new {body.m_qualifiedTypeName}();");
            }
            writer.DecreaseIndent();
            writer.WriteLine("}");
        }

        private static void WriteSerializeBodyMethod(ClassGenInfo classGenInfo, IndentedTextWriter writer)
        {
            writer.WriteLine("public override void SerializeBody(ref XmlWriteBuffer buffer)");
            writer.WriteLine("{");
            writer.IncreaseIndent();
            writer.WriteLine("base.SerializeBody(ref buffer);");

            foreach (var field in classGenInfo.m_bodies)
            {
                var isList = field.IsList();
                if (isList)
                {
                    if (field.IsPrimitive())
                    {
                        throw new Exception("for xml body of type list<T>, T must be IXmlSerializable");
                    }
                    
                    writer.WriteLine($"foreach (var obj in {field.m_fieldName})");
                    writer.WriteLine("{");
                    writer.IncreaseIndent();
                    writer.WriteLine("obj.Serialize(ref buffer);");
                    writer.DecreaseIndent();
                    writer.WriteLine("}");
                } else if (!field.IsPrimitive()) // another IXmlSerializable
                {
                    writer.WriteLine($"{field.m_fieldName}.Serialize(ref buffer);");
                } else
                {
                    if (field.m_xmlName != null)
                    {
                        writer.WriteLine("{");
                        writer.IncreaseIndent();
                        writer.WriteLine($"var node = buffer.StartNodeHead(\"{field.m_xmlName}\");");
                    }
                    
                    if (field.IsString())
                    {
                        writer.WriteLine($"buffer.PutCData({field.m_fieldName});");
                    } else
                    {
                        throw new Exception($"how to put sub body {field.m_qualifiedTypeName}");
                    }
                    
                    if (field.m_xmlName != null)
                    {
                        writer.WriteLine($"buffer.EndNode(ref node);");
                        writer.DecreaseIndent();
                        writer.WriteLine("}");
                    }
                }
            }
            writer.DecreaseIndent();
            writer.WriteLine("}");
        }
    }
}