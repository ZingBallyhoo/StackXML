using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace StackXML.Generator
{
    [Generator]
    public class XmlGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        private class ClassGenInfo
        {
            public readonly INamedTypeSymbol m_symbol;
            public readonly List<FieldGenInfo> m_fields = new List<FieldGenInfo>();
            public readonly List<FieldGenInfo> m_bodies = new List<FieldGenInfo>();

            public string m_className;

            public ClassGenInfo(INamedTypeSymbol symbol)
            {
                m_symbol = symbol;
            }
        }

        private class FieldGenInfo
        {
            public readonly IFieldSymbol m_field;
            
            public string m_xmlName;

            public char? m_splitChar;
            
            public FieldGenInfo(IFieldSymbol fieldSymbol)
            {
                m_field = fieldSymbol;
            }

            public bool IsList() => m_field.Type.Name == "List";
            public bool IsPrimitive() => m_field.Type.IsValueType || m_field.Type.Name == "String";
        }
        
        
        public void Execute(GeneratorExecutionContext context)
        {
            //Debugger.Launch();
            try
            {
                ExecuteInternal(context);
            } catch (Exception e)
            {
                var descriptor = new DiagnosticDescriptor(nameof(XmlGenerator), "Error", e.ToString(), "Error", DiagnosticSeverity.Error, true);
                var diagnostic = Diagnostic.Create(descriptor, Location.None);
                context.ReportDiagnostic(diagnostic);
            }
        }
        
        public void ExecuteInternal(GeneratorExecutionContext context)
        {
            if (!(context.SyntaxReceiver is SyntaxReceiver receiver))
                return;
            
            var compilation = context.Compilation;
            
            var bodyAttributeSymbol = compilation.GetTypeByMetadataName("StackXML.XmlBody");
            var fieldAttributeSymbol = compilation.GetTypeByMetadataName("StackXML.XmlField");
            var classAttributeSymbol = compilation.GetTypeByMetadataName("StackXML.XmlCls");
            var splitStringAttributeSymbol = compilation.GetTypeByMetadataName("StackXML.XmlSplitStr");

            var classes = new Dictionary<INamedTypeSymbol, ClassGenInfo>(SymbolEqualityComparer.Default);

            foreach (var @class in receiver.m_candidateClasses)
            {
                var model = compilation.GetSemanticModel(@class.SyntaxTree);
                var symbol = model.GetDeclaredSymbol(@class);
                
                if (symbol == null) continue;
                
                var classAttr = symbol.GetAttributes().SingleOrDefault(ad =>
                    ad.AttributeClass != null &&
                    ad.AttributeClass.Equals(classAttributeSymbol, SymbolEqualityComparer.Default));

                if (classAttr == null) continue;
                var xmlName = classAttr.ConstructorArguments[0].Value.ToString();

                var classGenInfo = new ClassGenInfo(symbol);
                classGenInfo.m_className = xmlName;
                classes[symbol] = classGenInfo;
            }
            
            foreach (var field in receiver.m_candidateFields)
            {
                var model = compilation.GetSemanticModel(field.SyntaxTree);
                foreach (var variable in field.Declaration.Variables)
                {
                    var fieldSymbol = ModelExtensions.GetDeclaredSymbol(model, variable) as IFieldSymbol;
                    if (fieldSymbol == null) continue;

                    var fieldAttr = fieldSymbol.GetAttributes().SingleOrDefault(ad => ad.AttributeClass.Equals(fieldAttributeSymbol, SymbolEqualityComparer.Default));
                    var bodyAttr = fieldSymbol.GetAttributes().SingleOrDefault(ad => ad.AttributeClass.Equals(bodyAttributeSymbol, SymbolEqualityComparer.Default));
                    var splitAttr = fieldSymbol.GetAttributes().SingleOrDefault(ad => ad.AttributeClass.Equals(splitStringAttributeSymbol, SymbolEqualityComparer.Default));
                    
                    if (fieldAttr == null && bodyAttr == null) continue;
                    
                    if (!classes.TryGetValue(fieldSymbol.ContainingType, out var classInfo))
                    {
                        classInfo = new ClassGenInfo(fieldSymbol.ContainingType);
                        classes[fieldSymbol.ContainingType] = classInfo;
                    }

                    var fieldInfo = new FieldGenInfo(fieldSymbol);
                    if (fieldAttr != null)
                    {
                        fieldInfo.m_xmlName = fieldAttr.ConstructorArguments[0].Value.ToString();
                        classInfo.m_fields.Add(fieldInfo);
                    }
                    if (bodyAttr != null)
                    {
                        fieldInfo.m_xmlName = bodyAttr.ConstructorArguments[0].Value?.ToString();
                        classInfo.m_bodies.Add(fieldInfo);
                    }
                    if (splitAttr != null)
                    {
                        fieldInfo.m_splitChar = (char)splitAttr.ConstructorArguments[0].Value;
                    }
                }
            }

            foreach (KeyValuePair<INamedTypeSymbol,ClassGenInfo> info in classes)
            {
                var classSource = ProcessClass(info.Value, classes);
                if (classSource == null) continue;
                context.AddSource($"{nameof(XmlGenerator)}_{info.Value.m_symbol}.cs", SourceText.From(classSource, Encoding.UTF8));
            }
        }
        
        private string ProcessClass(ClassGenInfo classGenInfo, Dictionary<INamedTypeSymbol, ClassGenInfo> map)
        {
            var classSymbol = classGenInfo.m_symbol;

            var writer = new IndentedTextWriter(new StringWriter(), "    ");
            writer.WriteLine("using System;");
            writer.WriteLine("using System.IO;");
            writer.WriteLine("using System.Collections.Generic;");
            writer.WriteLine("using StackXML;");
            writer.WriteLine("using StackXML.Str;");
            writer.WriteLine();

            var scope = new NestedScope(classSymbol);
            scope.Start(writer);
            
            writer.Write(NestedScope.GetClsString(classSymbol));
            if (classSymbol.BaseType == null || classSymbol.BaseType.ToString() == "object") // can it be null... idk
            {
                writer.Write(" : IXmlSerializable");
            }
            writer.WriteLine();
            writer.WriteLine("{");
            writer.Indent++;

            if (classGenInfo.m_className != null)
            {
                writer.WriteLine("public override ReadOnlySpan<char> GetNodeName()");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine($"return \"{classGenInfo.m_className}\";");
                writer.Indent--;
                writer.WriteLine("}");
            }

            if (classGenInfo.m_fields.Count > 0)
            {
                WriteAttrParseMethod(writer, classGenInfo);
                WriteAttSerializeMethod(writer, classGenInfo);
            }

            if (classGenInfo.m_bodies.Count > 0)
            {
                WriteParseBodyMethods(writer, classGenInfo, map);
                WriteSerializeBodyMethod(writer, classGenInfo);
            }
            
            writer.Indent--;
            writer.WriteLine("}"); // end class
            scope.End(writer);

            return writer.InnerWriter.ToString();
        }
        
        
        private void WriteAttrParseMethod(IndentedTextWriter writer, ClassGenInfo classGenInfo)
        {
            writer.WriteLine("public override bool ParseAttribute(ref XmlReadBuffer buffer, ReadOnlySpan<char> name, SpanStr value)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("if (base.ParseAttribute(ref buffer, name, value)) return true;");
            
            writer.WriteLine("switch (name)");
            writer.WriteLine("{");
            writer.Indent++;

            foreach (var field in classGenInfo.m_fields.OrderBy(x => x.m_xmlName.Length))
            {
                writer.WriteLine($"case \"{field.m_xmlName}\": {{");
                writer.Indent++;

                if (field.m_splitChar != null)
                {
                    var typeToRead = ((INamedTypeSymbol) field.m_field.Type).TypeArguments[0].Name;

                    writer.WriteLine($"var lst = new System.Collections.Generic.List<{typeToRead}>();");
                    writer.WriteLine($"var reader = new StrReader(value, '{field.m_splitChar}');");
                    var readerMethod = StrGenerator.GetReaderForType(typeToRead);

                    writer.WriteLine("while (reader.HasRemaining())");
                    writer.WriteLine("{");
                    writer.Indent++;
                    writer.WriteLine($"lst.Add({readerMethod});");
                    writer.Indent--;
                    writer.WriteLine("}");

                    writer.WriteLine($"{field.m_field.Name} = lst;");
                } else
                {
                    var readCommand = GetParseAttributeAction(field);
                    writer.WriteLine($"this.{field.m_field.Name} = {readCommand};");
                }

                writer.WriteLine("return true;");
                writer.Indent--;
                writer.WriteLine("}");
            }

            writer.Indent--;
            writer.WriteLine("}");

            writer.WriteLine("return false;");
            writer.Indent--;
            writer.WriteLine("}");
        }

        private void WriteAttSerializeMethod(IndentedTextWriter writer, ClassGenInfo classGenInfo)
        {
            writer.WriteLine("public override void SerializeAttributes(ref XmlWriteBuffer buffer)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("base.SerializeAttributes(ref buffer);");
            foreach (var field in classGenInfo.m_fields)
            {
                if (field.m_splitChar != null)
                {
                    var typeToRead = ((INamedTypeSymbol) field.m_field.Type).TypeArguments[0].Name;

                    writer.WriteLine("{");
                    writer.Indent++;

                    writer.WriteLine($"using var writer = new StrWriter('{field.m_splitChar}');");
                    writer.WriteLine($"foreach (var val in {field.m_field.Name})");
                    writer.WriteLine("{");
                    writer.Indent++;
                    writer.WriteLine($"{StrGenerator.GetWriterForType(typeToRead, "val")};");
                    writer.Indent--;
                    writer.WriteLine("}");
                    writer.WriteLine($"buffer.PutAttribute(\"{field.m_xmlName}\", writer.m_builtSpan);");
                    writer.WriteLine("writer.Dispose();");

                    writer.Indent--;
                    writer.WriteLine("}");
                } else
                {
                    var writerAction = GetPutAttributeAction(field);
                    writer.WriteLine(writerAction);
                }
            }

            writer.Indent--;
            writer.WriteLine("}");
        }

        private static string GetPutAttributeAction(FieldGenInfo field)
        {
            var writerAction = field.m_field.Type.Name switch
            {
                "String" => $"buffer.PutAttribute(\"{field.m_xmlName}\", {field.m_field.Name});",
                "Byte" => $"buffer.PutAttributeByte(\"{field.m_xmlName}\", {field.m_field.Name});",
                "Int32" => $"buffer.PutAttributeInt(\"{field.m_xmlName}\", {field.m_field.Name});",
                "UInt32" => $"buffer.PutAttributeUInt(\"{field.m_xmlName}\", {field.m_field.Name});",
                "Double" => $"buffer.PutAttributeDouble(\"{field.m_xmlName}\", {field.m_field.Name});",
                "Boolean" => $"buffer.PutAttributeBoolean(\"{field.m_xmlName}\", {field.m_field.Name});",
                _ => throw new Exception($"no attribute writer for type {field.m_field.Type.Name}")
            };
            return writerAction;
        }
        
        private static string GetParseAttributeAction(FieldGenInfo field)
        {
            var readCommand = field.m_field.Type.Name switch
            {
                "String" => "value.ToString()",
                "Byte" => "StrReader.ParseByte(value)",
                "Int32" => "StrReader.ParseInt(value)",
                "UInt32" => "StrReader.ParseUInt(value)",
                "Double" => "StrReader.ParseDouble(value)",
                "Boolean" => "StrReader.InterpretBool(value)",
                _ => throw new NotImplementedException($"no attribute reader for {field.m_field.Type.Name}")
            };
            return readCommand;
        }
        
        private void WriteParseBodyMethods(IndentedTextWriter writer, ClassGenInfo classGenInfo, Dictionary<INamedTypeSymbol, ClassGenInfo> map)
        {
            var classSymbol = classGenInfo.m_symbol;

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
                throw new Exception($"{classSymbol.Name} needs inline body and sub body");

            if (needsInlineBody)
            {
                writer.WriteLine("public override bool ParseFullBody(ref XmlReadBuffer buffer, ReadOnlySpan<char> bodySpan, ref int end)");
                writer.WriteLine("{");
                writer.Indent++;
                foreach (var body in classGenInfo.m_bodies)
                {
                    if (body.m_field.Type.Name == "String")
                    {
                        writer.WriteLine(
                            $"{body.m_field.Name} = buffer.DeserializeCDATA(bodySpan, out end).ToString();");
                    } else
                    {
                        throw new NotImplementedException($"Xml:WriteParseBodyMethods: how to inline body {body.m_field.Type.IsNativeIntegerType}");
                    }
                }
                writer.WriteLine("return true;");
                writer.Indent--;
                writer.WriteLine("}");
            } else if (needsSubBody)
            {
                WriteListConstructor(writer, classGenInfo);
                WriteParseSubBodyMethod(writer, classGenInfo, map);
            }
        }

        private void WriteParseSubBodyMethod(IndentedTextWriter writer, ClassGenInfo classGenInfo,
            Dictionary<INamedTypeSymbol, ClassGenInfo> map)
        {
            writer.WriteLine("public override bool ParseSubBody(ref XmlReadBuffer buffer, ReadOnlySpan<char> name, ReadOnlySpan<char> bodySpan, ReadOnlySpan<char> innerBodySpan, ref int end, ref int endInner)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("if (base.ParseSubBody(ref buffer, name, bodySpan, innerBodySpan, ref end, ref endInner)) return true;");
            
            writer.WriteLine("switch (name)");
            writer.WriteLine("{");
            writer.Indent++;
            
            foreach (var field in classGenInfo.m_bodies)
            {
                var isList = field.IsList();

                ClassGenInfo classToParse = null;
                if (!field.IsPrimitive())
                {
                    ITypeSymbol type;
                    if (isList)
                    {
                        type = ((INamedTypeSymbol) field.m_field.Type).TypeArguments[0];
                    } else
                    {
                        type = field.m_field.Type;
                    }

                    // todo: doesn't support types from other compilations
                    classToParse = map[(INamedTypeSymbol) type];
                }

                var nameToCheck = field.m_xmlName ??
                                  classToParse?.m_className ?? throw new InvalidDataException("no body name??");
                
                writer.WriteLine($"case \"{nameToCheck}\": {{");
                writer.Indent++;

                if (classToParse != null)
                {
                    if (isList)
                    {
                        writer.WriteLine(
                            $"{field.m_field.Name}.Add(buffer.Read<{classToParse.m_symbol}>(bodySpan, out end));");
                    } else
                    {
                        writer.WriteLine(
                            $"if ({field.m_field.Name} != null) throw new InvalidDataException(\"duplicate non-list body {nameToCheck}\");");
                        writer.WriteLine(
                            $"{field.m_field.Name} = buffer.Read<{classToParse.m_symbol}>(bodySpan, out end);");
                    }
                } else if (field.m_field.Type.Name == "String")
                {
                    writer.WriteLine(
                        $"{field.m_field.Name} = buffer.DeserializeCDATA(innerBodySpan, out endInner).ToString();");
                } else
                {
                    throw new Exception($"can't read body of type {field.m_field.Type.Name}");
                }
                
                writer.WriteLine("return true;");
                writer.Indent--;
                writer.WriteLine("}");
            }
            
            writer.Indent--;
            writer.WriteLine("}");

            writer.WriteLine("return false;");
            writer.Indent--;
            writer.WriteLine("}");
        }

        private void WriteListConstructor(IndentedTextWriter writer, ClassGenInfo classGenInfo)
        {
            writer.WriteLine($"public {classGenInfo.m_symbol.Name}()");
            writer.WriteLine("{");
            writer.Indent++;
            foreach (var body in classGenInfo.m_bodies)
            {
                if (!body.IsList()) continue;
                writer.WriteLine($"{body.m_field.Name} = new {body.m_field.Type}();");
            }
            writer.Indent--;
            writer.WriteLine("}");
        }

        private void WriteSerializeBodyMethod(IndentedTextWriter writer, ClassGenInfo classGenInfo)
        {
            writer.WriteLine("public override void SerializeBody(ref XmlWriteBuffer buffer)");
            writer.WriteLine("{");
            writer.Indent++;
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
                    
                    writer.WriteLine($"foreach (var obj in {field.m_field.Name})");
                    writer.WriteLine("{");
                    writer.Indent++;
                    writer.WriteLine("obj.Serialize(ref buffer);");
                    writer.Indent--;
                    writer.WriteLine("}");
                } else if (!field.IsPrimitive()) // another IXmlSerializable
                {
                    writer.WriteLine($"{field.m_field.Name}.Serialize(ref buffer);");
                } else
                {
                    if (field.m_xmlName != null)
                    {
                        writer.WriteLine("{");
                        writer.Indent++;
                        writer.WriteLine($"var node = buffer.StartNodeHead(\"{field.m_xmlName}\");");
                    }
                    if (field.m_field.Type.Name == "String")
                    {
                        writer.WriteLine($"buffer.PutCData({field.m_field.Name});");
                    } else
                    {
                        throw new Exception($"how to put sub body {field.m_field.Type.Name}");
                    }
                    if (field.m_xmlName != null)
                    {
                        writer.WriteLine($"buffer.EndNode(ref node);");
                        writer.Indent--;
                        writer.WriteLine("}");
                    }
                }
            }
            writer.Indent--;
            writer.WriteLine("}");
        }
        
        private class SyntaxReceiver : ISyntaxReceiver
        {
            public List<FieldDeclarationSyntax> m_candidateFields { get; } = new List<FieldDeclarationSyntax>();
            public List<ClassDeclarationSyntax> m_candidateClasses { get; } = new List<ClassDeclarationSyntax>();
            
            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is FieldDeclarationSyntax fieldDeclarationSyntax && fieldDeclarationSyntax.AttributeLists.Count > 0)
                {
                    m_candidateFields.Add(fieldDeclarationSyntax);
                }
                if (syntaxNode is ClassDeclarationSyntax classDeclarationSyntax && classDeclarationSyntax.AttributeLists.Count > 0)
                {
                    m_candidateClasses.Add(classDeclarationSyntax);
                }
            }
        }
    }
}