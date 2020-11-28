using System;
using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace StackXML
{
    /// <summary>Stack based XML serializer</summary>
    public ref struct XmlWriteBuffer
    {
        /// <summary>Internal char buffer</summary>
        private char[] m_buffer;
        /// <summary>Current write offset within <see cref="m_buffer"/></summary>
        private int m_currentOffset;
        /// <summary>Whether or not a node head is currently open (&gt; hasn't been written)</summary>
        private bool m_pendingNodeHeadClose;

        public bool m_useCData;

        /// <summary>Span representing the tail of the internal buffer</summary>
        private Span<char> m_writeSpan => new Span<char>(m_buffer).Slice(m_currentOffset);
        
        /// <summary>
        /// Create a new XmlWriteBuffer
        /// </summary>
        /// <returns>XmlWriteBuffer instance</returns>
        public static XmlWriteBuffer Create()
        {
            return new XmlWriteBuffer(0);
        }
        
        /// <summary>
        /// Actual XmlWriteBuffer constructor
        /// </summary>
        /// <param name="_">blank parameter</param>
        // ReSharper disable once UnusedParameter.Local
        private XmlWriteBuffer(int _=0)
        {
            m_pendingNodeHeadClose = false;
            m_buffer = ArrayPool<char>.Shared.Rent(1024);
            m_currentOffset = 0;

            m_useCData = true;
        }
        
        /// <summary>Resize internal char buffer (<see cref="m_buffer"/>)</summary>
        private void Resize()
        {
            var newBuffer = ArrayPool<char>.Shared.Rent(m_buffer.Length * 2); // double size
            Buffer.BlockCopy(m_buffer, 0, newBuffer, 0, m_buffer.Length * sizeof(char)); // count is in bytes
            ArrayPool<char>.Shared.Return(m_buffer);
            m_buffer = newBuffer;
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
            if (m_useCData)
            {
                PutString(XmlReadBuffer.c_cdataStart);
                PutString(text);
                PutString(XmlReadBuffer.c_cdataEnd);
            } else
            {
                    PutString(text);
            }
        }
        
        public void PutAttribute(ReadOnlySpan<char> name, ReadOnlySpan<char> value)
        {
            StartAttrCommon(name);
            PutString(value);
            EndAttrCommon();
        }

        public void PutAttributeInt(ReadOnlySpan<char> name, int value)
        {
            StartAttrCommon(name);
            PutInt(value);
            EndAttrCommon();
        }
        
        public void PutAttributeUInt(ReadOnlySpan<char> name, uint value)
        {
            StartAttrCommon(name);
            PutUInt(value);
            EndAttrCommon();
        }
        
        public void PutAttributeDouble(ReadOnlySpan<char> name, double value)
        {
            StartAttrCommon(name);
            PutDouble(value);
            EndAttrCommon();
        }
        
        public void PutAttributeBoolean(ReadOnlySpan<char> name, bool value)
        {
            StartAttrCommon(name);
            PutChar(value ? '1' : '0');
            EndAttrCommon();
        }
        
        public void PutAttributeByte(ReadOnlySpan<char> name, byte value)
        {
            StartAttrCommon(name);
            PutUInt(value); // todo: hmm
            EndAttrCommon();
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

        /// <summary>Format a <see cref="Int32"/> into the buffer as text</summary>
        /// <param name="value">The value to write</param>
        public void PutInt(int value)
        {
            int charsWritten;
            while (!value.TryFormat(m_writeSpan, out charsWritten, default, CultureInfo.InvariantCulture))
            {
                Resize();
            }
            m_currentOffset += charsWritten;
        }
        
        /// <summary>Format a <see cref="UInt32"/> into the buffer as text</summary>
        /// <param name="value">The value to write</param>
        public void PutUInt(uint value)
        {
            int charsWritten;
            while (!value.TryFormat(m_writeSpan, out charsWritten, default, CultureInfo.InvariantCulture))
            {
                Resize();
            }
            m_currentOffset += charsWritten;
        }
        
        /// <summary>Format a <see cref="Double"/> into the buffer as text</summary>
        /// <param name="value">The value to write</param>
        public void PutDouble(double value)
        {
            int charsWritten;
            while (!value.TryFormat(m_writeSpan, out charsWritten, default, CultureInfo.InvariantCulture))
            {
                Resize();
            }
            m_currentOffset += charsWritten;
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
            
            while (!str.TryCopyTo(m_writeSpan))
            {
                Resize();
            }
            m_currentOffset += str.Length;
        }
        
        /// <summary>Put a raw <see cref="Char"/> into the buffer</summary>
        /// <param name="c">The character to write</param>
        public void PutChar(char c)
        {
            if (m_writeSpan.Length == 0)
            {
                Resize();
            }
            
            m_writeSpan[0] = c;
            m_currentOffset++;
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
            var fullSpan = new ReadOnlySpan<char>(m_buffer, 0, m_currentOffset);
            return fullSpan;
        }

        /// <summary>Release internal buffer</summary>
        public void Dispose()
        {
            if (m_buffer != null)
            {
                ArrayPool<char>.Shared.Return(m_buffer);
                m_buffer = null;
            }
        }

        /// <summary>
        /// Serialize a baseclass of <see cref="IXmlSerializable"/> to XML text
        /// </summary>
        /// <param name="obj">The object to serialize</param>
        /// <param name="useCData">Should text be written as CDATA</param>
        /// <returns>Serialized XML</returns>
        public static string SerializeStatic(IXmlSerializable obj, bool useCData = true)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            var writer = Create();
            writer.m_useCData = useCData;
            try
            {
                obj.Serialize(ref writer);
                var str = writer.ToStr();
                return str;
            } finally
            {
                writer.Dispose();
            }
        }

        
        private static readonly char[] s_escapeChars = new char[]
        {
            '<', '>', '&',
        };
        
        private static readonly char[] s_escapeCharsAttribute = new char[]
        {
            '<', '>', '&', '\'', '\"', '\n', '\r', '\t'
        };

        /// <summary>Encode unescaped text into the buffer</summary>
        /// <param name="input">Unescaped text</param>
        /// <param name="attribute">True if text is for an attribute, false for an element</param>
        public void EncodeText(ReadOnlySpan<char> input, bool attribute=false)
        {
            var escapeChars = new ReadOnlySpan<char>(attribute ? s_escapeCharsAttribute : s_escapeChars);

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