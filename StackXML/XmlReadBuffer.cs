using System;
using System.IO;
using StackXML.Logging;

namespace StackXML
{
    /// <summary>Stack based XML deserializer</summary>
    public ref struct XmlReadBuffer
    {
        private const string c_commentStart = "<!--";
        private const string c_commentEnd = "-->";
        private const string c_declarationEnd = "?>";
        public const string c_cdataStart = "<![CDATA[";
        public const string c_cdataEnd = "]]>";

        /// <summary>Abort parsing immediately</summary>
        public bool m_abort;
        
        /// <summary>
        /// If false, will parse raw text instead of expecting CDATA blocks
        /// </summary>
        public bool m_useCData;

        /// <summary>Current depth of calls to <see cref="ReadInto"/></summary>
        public int m_depth;

        /// <summary>
        /// Maximum depth depth that calls to <see cref="ReadInto"/> can happen before an exception will be thrown to
        /// protect the application
        /// </summary>
        public static int s_maxDepth = 50;
        
        private static readonly ILog s_logger = LogProvider.GetLogger(nameof(XmlReadBuffer)); 

        /// <summary>
        /// Parses XML node attributes
        /// </summary>
        /// <param name="currSpan">Text span</param>
        /// <param name="closeBraceIdx">Index in <param name="currSpan"/> which is at the end of the node declaration</param>
        /// <param name="position">Starting position within <param name="currSpan"/></param>
        /// <param name="obj">Object to receive parsed data</param>
        /// <exception cref="InvalidDataException">Unable to parse data</exception>
        /// <returns>Position within <param name="currSpan"/> which is at the end of the attribute list</returns>
        private int DeserializeAttributes(ReadOnlySpan<char> currSpan, int closeBraceIdx, int position, IXmlSerializable obj)
        {
            s_logger.Debug("test");
            while (position < closeBraceIdx)
            {
                var spaceSpan = currSpan.Slice(position, closeBraceIdx-position);
                if (spaceSpan[0] == ' ')
                {
                    position++;
                    continue;
                }
                
                var eqIdx = spaceSpan.IndexOf('=');
                if (eqIdx == -1)
                {
                    break;
                }
                var attributeName = spaceSpan.Slice(0, eqIdx);

                var quoteType = spaceSpan[eqIdx + 1];
                if (quoteType != '\'' && quoteType != '\"') throw new InvalidDataException($"invalid quote char {quoteType}");

                var attributeValueSpan = spaceSpan.Slice(eqIdx + 2);
                var quoteEndIdx = attributeValueSpan.IndexOf(quoteType);
                if (quoteEndIdx == -1) throw new InvalidDataException("unable to find pair end quote");

                var attributeValue = attributeValueSpan.Slice(0, quoteEndIdx);

                var nameHash = IXmlSerializable.HashName(attributeName);
                var assigned = obj.ParseAttribute(ref this,nameHash, attributeValue);
                if (m_abort) return -1;
                if (!assigned)
                {
                    s_logger.Warn("[XmlReadBuffer]: unhandled attribute {AttributeName} on {ClassName}. \"{Value}\"",
                        attributeName.ToString(), obj.GetType().ToString(), attributeValue.ToString());
                }
                        
                position += attributeName.Length + attributeValue.Length + 2 + 1; // ='' -- 3 chars
            }
            return position;
        }
        
        /// <summary>Parse an XML node and children into structured class <param name="obj"/></summary>
        /// <param name="span">Text to parse</param>
        /// <param name="obj">Object to receive parsed data</param>
        /// <returns>Position within <param name="span"/> that the node ends at</returns>
        /// <exception cref="InvalidDataException">Unable to parse data</exception>
        /// <exception cref="Exception">Internal error</exception>
        private int ReadInto(ReadOnlySpan<char> span, IXmlSerializable obj)
        {
            m_depth++;
            if (m_depth >= s_maxDepth)
            {
                throw new Exception($"maximum depth {s_maxDepth} reached");
            }
            var primary = true;
            for (var i = 0; i < span.Length;)
            {
                var currSpan = span.Slice(i);

                if (currSpan[0] != '<')
                {
                    var idxOfAngleBracket = currSpan.IndexOf('<');
                    if (idxOfAngleBracket == -1) break;
                    i += idxOfAngleBracket;
                    continue;
                }

                if (currSpan.Length > 1)
                {
                    // no need to check length here.. name has to be at least 1 char lol
                    if (currSpan[1] == '/')
                    {
                        // current block has ended
                        m_depth--;
                        return i+2; // todo: hmm. this make caller responsible for aligning again
                    }
                    if (currSpan[1] == '?')
                    {
                        // skip xml declaration
                        // e.g <?xml version='1.0'?>
                    
                        var declarationEnd = currSpan.IndexOf(c_declarationEnd);
                        if (declarationEnd == -1) throw new InvalidDataException("where is declaration end");
                        i += declarationEnd+c_declarationEnd.Length;
                        continue;
                    }
                    if (currSpan.StartsWith(c_commentStart))
                    {
                        var commentEnd = currSpan.IndexOf(c_commentEnd);
                        if (commentEnd == -1) throw new InvalidDataException("where is comment end");
                        i += commentEnd+c_commentEnd.Length;
                        continue;
                    }
                    if (currSpan[1] == '!')
                    {
                        throw new Exception("xml data type definitions are not supported");
                    }
                }
                    
                var closeBraceIdx = currSpan.IndexOf('>');
                var spaceIdx = currSpan.IndexOf(' ');
                if (spaceIdx > closeBraceIdx) spaceIdx = -1; // we are looking for a space in the node declaration
                var nameEndIdx = Math.Min(closeBraceIdx, spaceIdx);
                if (nameEndIdx == -1) nameEndIdx = closeBraceIdx; // todo min of 1 and -1 is -1
                if (nameEndIdx == -1) throw new InvalidDataException("unable to find end of node name");

                var noBody = false;
                if (currSpan[nameEndIdx - 1] == '/')
                {
                    // <lightning/>
                    noBody = true;
                    nameEndIdx -= 1;
                }
                var nodeName = currSpan.Slice(1, nameEndIdx - 1);

                const int unassignedIdx = int.MinValue;

                if (primary)
                {
                    // read actual node
                    
                    int afterAttrs;

                    if (spaceIdx != -1)
                    {
                        afterAttrs = spaceIdx+1; // skip space
                        afterAttrs = DeserializeAttributes(currSpan, closeBraceIdx, afterAttrs, obj);
                        if (m_abort)
                        {
                            m_depth--;
                            return -1;
                        }
                    } else
                    {
                        afterAttrs = closeBraceIdx;
                    }
                    
                    var afterAttrsChar = currSpan[afterAttrs];
                    
                    if (noBody || afterAttrsChar == '/')
                    {
                        // no body
                        m_depth--;
                        return i + closeBraceIdx + 1;
                    }
                    
                    primary = false;

                    if (afterAttrsChar != '>')
                        throw new InvalidDataException(
                            "char after attributes should have been the end of the node, but it isn't");
                    
                    var bodySpan = currSpan.Slice(closeBraceIdx+1);

                    var endIdx = unassignedIdx;
                    
                    var handled = obj.ParseFullBody(ref this, bodySpan, ref endIdx);
                    if (m_abort)
                    {
                        m_depth--;
                        return -1;
                    }

                    if (handled)
                    {
                        if (endIdx == unassignedIdx) throw new Exception("endIdx should be set if returning true from ParseFullBody");
                        
                        var fullSpanIdx = afterAttrs + 1 + endIdx;

                        // should be </nodeName>
                        if (currSpan[fullSpanIdx] != '<' || 
                            currSpan[fullSpanIdx + 1] != '/' ||
                            !currSpan.Slice(fullSpanIdx + 2, nodeName.Length).SequenceEqual(nodeName) ||
                            currSpan[fullSpanIdx + 2 + nodeName.Length] != '>')
                        {
                            
                            throw new InvalidDataException("Unexpected data after handling full body");
                        }
                        i += fullSpanIdx + 2 + nodeName.Length;
                        continue;
                    } else
                    {
                        i += closeBraceIdx+1;
                        continue;
                    }
                } else
                {
                    // read child nodes
                    
                    // todo: i would like to use nullable but the language doesn't like it (can't "out int" into "ref int?")
                    var endIdx = unassignedIdx;
                    var endInnerIdx = unassignedIdx;
                    
                    var innerBodySpan = currSpan.Slice(closeBraceIdx+1);
                    var nodeNameHash = IXmlSerializable.HashName(nodeName);
                    var parsedSub = obj.ParseSubBody(ref this, nodeNameHash, 
                        currSpan, innerBodySpan, 
                        ref endIdx, ref endInnerIdx);
                    if (m_abort)
                    {
                        m_depth--;
                        return -1;
                    }
                    if (parsedSub)
                    {
                        if (endIdx != unassignedIdx)
                        {
                            i += endIdx;
                            continue;
                        } else if (endInnerIdx != unassignedIdx)
                        {
                            // (3 + nodeName.Length) = </name>
                            i += closeBraceIdx + 1 + endInnerIdx + (3 + nodeName.Length);
                            continue;
                        } else
                        {
                            throw new Exception("one of endIdx or endInnerIdx should be set if returning true from ParseSubBody");
                        }
                    } else
                    {
                        throw new InvalidDataException($"Unknown sub body {nodeName.ToString()} on {obj.GetType()}");
                    }
                }
                
#pragma warning disable 162
                throw new Exception("bottom of parser loop should be unreachable");
#pragma warning restore 162
            }
            m_depth--;
            return span.Length;
        }
        
        private ReadOnlySpan<char> DeserializeElementRawInnerText(ReadOnlySpan<char> span, out int endEndIdx)
        {
            endEndIdx = span.IndexOf('<'); // find start of next node
            if (endEndIdx == -1) throw new InvalidDataException("unable to find end of text");
            return span.Slice(0, endEndIdx);
        }

        /// <summary>
        /// Deserialize XML element inner text. Switches between CDATA and raw text on <see cref="m_useCData"/>
        /// </summary>
        /// <param name="span">Span at the beginning of the element's inner text</param>
        /// <param name="endEndIdx">The index of the end of the text within <see cref="span"/></param>
        /// <returns>Deserialized inner text data</returns>
        /// <exception cref="InvalidDataException">The bounds of the text could not be determined</exception>
        public ReadOnlySpan<char> DeserializeCDATA(ReadOnlySpan<char> span, out int endEndIdx)
        {
            if (!m_useCData)
            {
                return DeserializeElementRawInnerText(span, out endEndIdx);
            }
            // todo: CDATA cannot contain the string "]]>" anywhere in the XML document.

            if (!span.StartsWith(c_cdataStart)) throw new InvalidDataException("invalid cdata start");

            var endIdx = span.IndexOf(c_cdataEnd);
            if (endIdx == -1) throw new InvalidDataException("unable to find end of cdata");

            var stringData = span.Slice(c_cdataStart.Length, endIdx - c_cdataStart.Length);

            endEndIdx = c_cdataEnd.Length + endIdx;
            return stringData;
        }

        /// <summary>
        /// Create a new instance of <typeparam name="T"/> and parse into it
        /// </summary>
        /// <param name="span">Text to parse</param>
        /// <param name="end">Index into <param name="span"/> that is at the end of the node</param>
        /// <typeparam name="T">Type to parse</typeparam>
        /// <returns>The created instance</returns>
        public T Read<T>(ReadOnlySpan<char> span, out int end) where T: IXmlSerializable, new()
        {
            var t = new T();
            end = ReadInto(span, t);
            return t;
        }
        
        /// <summary>
        /// The same as <see cref="Read{T}(System.ReadOnlySpan{char},out int)"/> but without the `end` out parameter
        /// </summary>
        /// <param name="span">Text to parse</param>
        /// <typeparam name="T">Type to parse</typeparam>
        /// <returns>The created instance</returns>
        public T Read<T>(ReadOnlySpan<char> span) where T: IXmlSerializable, new()
        {
            return Read<T>(span, out _);
        }
        
        /// <summary>
        /// Parse into a new instance <typeparam name="T"/> without manually creating a XmlReadBuffer
        /// </summary>
        /// <param name="span">Text to parse</param>
        /// <param name="useCData"><see cref="m_useCData"/></param>
        /// <typeparam name="T">Type to parse</typeparam>
        /// <returns>The created instance</returns>
        public static T ReadStatic<T>(ReadOnlySpan<char> span, bool useCData=true) where T: IXmlSerializable, new()
        {
            var reader = new XmlReadBuffer
            {
                m_useCData = useCData
            };
            return reader.Read<T>(span);
        }
    }
}