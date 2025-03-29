namespace StackXML
{
    public enum CDataMode
    {
        /// <summary>Use CData sections for text. Text in the CData block will not be encoded</summary>
        On,
        /// <summary>Use encoded text sections</summary>
        Off,
        /// <summary>Use CData sections containing encoded text. Against normal XML spec</summary>
        OnEncoded
    }
}