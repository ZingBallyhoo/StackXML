using System;

namespace StackXML
{
    /// <summary>
    /// Interface for types that can be read from and written to XML using <see cref="XmlReadBuffer"/> and <see cref="XmlWriteBuffer"/>
    /// </summary>
    public interface IXmlSerializable
    {
        /// <summary>Gets the name of the node to be written</summary>
        /// <returns>Name of the node to be written</returns>
        ReadOnlySpan<char> GetNodeName();

        bool ParseFullBody(ref XmlReadBuffer buffer, ReadOnlySpan<char> bodySpan, ref int end);
        
        bool ParseSubBody(ref XmlReadBuffer buffer, ReadOnlySpan<char> name, 
            ReadOnlySpan<char> bodySpan, ReadOnlySpan<char> innerBodySpan, 
            ref int end, ref int endInner);
        
        bool ParseAttribute(ref XmlReadBuffer buffer, ReadOnlySpan<char> name, ReadOnlySpan<char> value);

        void SerializeBody(ref XmlWriteBuffer buffer);

        void SerializeAttributes(ref XmlWriteBuffer buffer);
    }
}