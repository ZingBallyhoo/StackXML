using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance.Buffers;

namespace StackXML
{
    /// <summary>Stack based XML serializer</summary>
    public ref struct XmlWriteBuffer
    {
        private readonly ArrayPoolBufferWriter<char> m_writer;
        public XmlWriteParams m_params;
        
        /// <summary>Whether or not a node head is currently open (&gt; hasn't been written)</summary>
        private bool m_pendingNodeHeadClose;
        
        /// <summary>
        /// Create a new XmlWriteBuffer
        /// </summary>
        /// <returns>XmlWriteBuffer instance</returns>
        public static XmlWriteBuffer Create()
        {
            return new XmlWriteBuffer();
        }
        
        /// <summary>
        /// Actual XmlWriteBuffer constructor
        /// </summary>
        public XmlWriteBuffer()
        {
            m_writer = new ArrayPoolBufferWriter<char>();
            m_params = new XmlWriteParams();
            m_pendingNodeHeadClose = false;
        }
        
        /// <summary>Resize internal char buffer</summary>
        private void Resize()
        {
            m_writer.GetSpan(m_writer.Capacity*2);
        }

        /// <summary>Record of a node that is currently being written into the buffer</summary>
        public readonly ref struct NodeRecord
        {
            public readonly ReadOnlySpan<char> m_name;

            public NodeRecord(ReadOnlySpan<char> name)
            {
                m_name = name;
            }
        }
        
        /// <summary>
        /// Puts a "&gt;" character to signify the end of the current node head ("&lt;name&gt;") if it hasn't been already done
        /// </summary>
        private void CloseNodeHeadForBodyIfOpen()
        {
            if (!m_pendingNodeHeadClose) return;
            PutChar('>');
            m_pendingNodeHeadClose = false;
        }
        
        /// <summary>Start an XML node</summary>
        /// <param name="name">Name of the node</param>
        /// <returns>Record describing the node</returns>
        public NodeRecord StartNodeHead(ReadOnlySpan<char> name)
        {
            CloseNodeHeadForBodyIfOpen();
            
            PutChar('<');
            PutString(name);
            m_pendingNodeHeadClose = true;
            return new NodeRecord(name);
        }
        
        public void EndNodeHead()
        {
            CloseNodeHeadForBodyIfOpen();
        }
        
        /// <summary>End an XML node</summary>
        /// <param name="record">Record describing the open node</param>
        public void EndNode(ref NodeRecord record)
        {
            if (!m_pendingNodeHeadClose)
            {
                PutString("</");
                PutString(record.m_name);
                PutChar('>');
            } else
            {
                PutString("/>");
                m_pendingNodeHeadClose = false;
            }
        }
        
        /// <summary>Escape and put text into the buffer</summary>
        /// <param name="text">The raw text to write</param>
        public void PutCData(ReadOnlySpan<char> text)
        {
            CloseNodeHeadForBodyIfOpen();
            if (m_params.m_cdataMode == CDataMode.Off)
            {
                EncodeText(text);
                return;
            }

            PutString(XmlReadBuffer.c_cdataStart);
            if (m_params.m_cdataMode == CDataMode.OnEncoded) EncodeText(text);
            else PutString(text); // CDataMode.On
            PutString(XmlReadBuffer.c_cdataEnd);
        }
        
        public void PutAttribute(ReadOnlySpan<char> name, ReadOnlySpan<char> value)
        {
            StartAttrCommon(name);
            EncodeText(value, true);
            EndAttrCommon();
        }
        
        public void PutAttribute<T>(ReadOnlySpan<char> name, T value) where T : ISpanFormattable
        {
            StartAttrCommon(name);
            Put(value);
            EndAttrCommon();
        }

        public void PutAttribute(ReadOnlySpan<char> name, bool value)
        {
            // todo: why is bool not ISpanFormattable?
            PutAttribute(name, value ? '1' : '0');
        }

        /// <summary>Write the starting characters for an attribute (" name=''")</summary>
        /// <param name="name">Name of the attribute</param>
        private void StartAttrCommon(ReadOnlySpan<char> name)
        {
            Debug.Assert(m_pendingNodeHeadClose);
            PutChar(' ');
            PutString(name);
            PutString("='");
        }

        /// <summary>End an attribute</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)] // don't bother calling this
        private void EndAttrCommon()
        {
            PutChar('\'');
        }
        
        public void Put<T>(T value) where T : ISpanFormattable
        {
            int charsWritten;
            while (!m_params.m_stringFormatter.TryFormat(m_writer.GetSpan(), value, out charsWritten))
            {
                Resize();
            }
            m_writer.Advance(charsWritten);
        }
        
        /// <summary>Put a raw <see cref="String"/> into the buffer</summary>
        /// <param name="str">The string to write</param>
        public void PutString(string str)
        {
            if (string.IsNullOrEmpty(str)) return;
            PutString(str.AsSpan());
        }

        /// <summary>Put a raw <see cref="ReadOnlySpan{T}"/> into the buffer</summary>
        /// <param name="str">The span of text to write</param>
        public void PutString(ReadOnlySpan<char> str)
        {
            if (str.Length == 0) return;
            
            while (!str.TryCopyTo(m_writer.GetSpan()))
            {
                Resize();
            }
            m_writer.Advance(str.Length);
        }
        
        /// <summary>Put a raw <see cref="Char"/> into the buffer</summary>
        /// <param name="c">The character to write</param>
        public void PutChar(char c)
        {
            // todo: use same resize strategy as elsewhere?
            m_writer.GetSpan(1)[0] = c;
            m_writer.Advance(1);
        }
        
        public void PutObject<T>(in T value) where T : IXmlSerializable, allows ref struct
        {
            var node = StartNodeHead(value.GetNodeName());
            value.SerializeAttributes(ref this);
            value.SerializeBody(ref this);
            EndNode(ref node);
        }

        /// <summary>Allocate and return serialized XML data as a <see cref="String"/></summary>
        /// <returns>String of serialized XML</returns>
        public string ToStr()
        {
            return ToSpan().ToString();
        }

        /// <summary>
        /// Get <see cref="ReadOnlySpan{Char}"/> of used portion of the internal buffer containing serialized XML data
        /// </summary>
        /// <returns>Serialized XML data</returns>
        public ReadOnlySpan<char> ToSpan()
        {
            return m_writer.WrittenSpan;
        }

        /// <summary>Release internal buffer</summary>
        public void Dispose()
        {
            m_writer.Dispose();
        }

        /// <summary>
        /// Serialize a baseclass of <see cref="IXmlSerializable"/> to XML text
        /// </summary>
        /// <param name="obj">The object to serialize</param>
        /// <param name="cdataMode">Should text be written as CDATA</param>
        /// <returns>Serialized XML</returns>
        public static string SerializeStatic<T>(in T obj, CDataMode cdataMode=CDataMode.On) where T : IXmlSerializable, allows ref struct
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            var writer = Create();
            writer.m_params.m_cdataMode = cdataMode;
            try
            {
                writer.PutObject(obj);
                var str = writer.ToStr();
                return str;
            } finally
            {
                writer.Dispose();
            }
        }

        
        private static readonly SearchValues<char> s_escapeChars = SearchValues.Create(
        [
            '<', '>', '&'
        ]);
        
        private static readonly SearchValues<char> s_escapeCharsAttribute = SearchValues.Create(
        [
            '<', '>', '&', '\'', '\"', '\n', '\r', '\t'
        ]);

        /// <summary>Encode unescaped text into the buffer</summary>
        /// <param name="input">Unescaped text</param>
        /// <param name="attribute">True if text is for an attribute, false for an element</param>
        public void EncodeText(ReadOnlySpan<char> input, bool attribute=false)
        {
            var escapeChars = attribute ? s_escapeCharsAttribute : s_escapeChars;

            ReadOnlySpan<char> currentInput = input;
            while (true)
            {
                int escapeCharIdx = currentInput.IndexOfAny(escapeChars);
                if (escapeCharIdx == -1)
                {
                    PutString(currentInput);
                    return;
                }
                
                PutString(currentInput.Slice(0, escapeCharIdx));

                var charToEncode = currentInput[escapeCharIdx];
                PutString(charToEncode switch
                {
                    '<' => "&lt;",
                    '>' => "&gt;",
                    '&' => "&amp;",
                    '\'' => "&apos;",
                    '\"' => "&quot;",
                    '\n' => "&#xA;",
                    '\r' => "&#xD;",
                    '\t' => "&#x9;",
                    _ => throw new Exception($"unknown escape char \"{charToEncode}\". how did we get here")
                });
                currentInput = currentInput.Slice(escapeCharIdx + 1);
            }
        }
    }
}