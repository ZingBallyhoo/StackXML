using StackXML.Str;

namespace StackXML
{
    public struct XmlWriteParams
    {
        /// <summary>Type of text blocks to serialize</summary>
        public CDataMode m_cdataMode = CDataMode.On;
        
        public IStrFormatter m_stringFormatter = BaseStrFormatter.s_instance;
        
        public XmlWriteParams()
        {
        }
    }
}