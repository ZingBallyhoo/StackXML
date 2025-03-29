using StackXML.Str;

namespace StackXML
{
    public struct XmlReadParams
    {
        /// <summary>Type of text blocks to deserialize</summary>
        public CDataMode m_cdataMode = CDataMode.On;
        
        /// <summary>
        /// Maximum object that can be reached before an exception will be thrown to protect the application
        /// </summary>
        public int m_maxDepth = 50;
        
        public IStrParser m_stringParser = StandardStrParser.s_instance;

        public XmlReadParams()
        {
        }
    }
}