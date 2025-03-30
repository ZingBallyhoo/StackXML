namespace StackXML.Str
{
    public interface IStrClass
    {
        void Serialize(ref StrWriter writer);
        void Deserialize(ref StrReader reader);
    }
}