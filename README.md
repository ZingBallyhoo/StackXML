# StackXML
Stack based zero*-allocation XML serializer and deserializer powered by C# 9 source generators.

## Why
Premature optimisation :)

## Setup
- StackXML targets netstandard2.1 which means back to .NET Core 3.0 is supported, but I would recommend .NET 5
- Add the folowing to your project to reference the serializer and enable the source generator
```xml
<ItemGroup>
    <ProjectReference Include="..\StackXML\StackXML.csproj" />
    <ProjectReference Include="..\StackXML.Generator\StackXML.Generator.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
``` 
- The common entrypoint for deserializing is `XmlReadBuffer.ReadStatic(ReadOnlySpan<char>)`
- The common entrypoint for serializing is `XmlWriteBuffer.SerializeStatic(IXmlSerializable)`
  - This method returns a string, to avoid this allocation you will need create your own instance of XmlWriteBuffer and ensure it is disposed safely like `SerializeStatic` does. The `ToSpan` method returns the char span containing the serialized text

## Features
- Fully structured XML serialization and deserialization with 0 allocations, apart from the output data structure when deserializing. Serialization uses a pooled buffer from `ArrayPool<char>.Shared` that is released when the serializer is disposed.
  - `XmlReadBuffer` handles deserialization
  - `XmlWriteBuffer` handles serialization
  - `XmlCls` maps a type to an element
    - Used for the serializer to know what the element name should be
    - Used by the deserializer to map to IXmlSerializable bodies with no explicit name
  - `XmlField` maps to attributes
  - `XmlBody` maps to child elements
  - `IXmlSerializable` (not actually an interface, see quirks) represents a type that can be read from or written to XML
    - Can be manually added as a base, or the source generator will add it automatically to any type that has XML attributes
- Parsing delimited attributes into typed lists
  - `<test list='1,2,3,4,6,7,8,9'>`
  - `[XmlField("list")] [XmlSplitStr(',')] public List<int> m_list;`
  - Using StrReader and StrWriter, see below
- StrReader and StrWriter classes, for reading and writing (comma usually) delimited strings with 0 allocations.
  - Can be used in a fully structed way by adding `StrField` attributes to fields on a `ref partial struct` (not compatible with XmlSplitStr, maybe future consideration)

## Quirks
- Encoding and decoding of text is not supporterd, but its something I really do need to add.
  - Encode implementation is done and benchmarked, see `XmlWriteBuffer.EncodeText` and `XmlEncodeBenchmark`
- Invalid data between elements is ignored
  - `<test>anything here is completely missed<testInner/><test/>`
- Spaces between attributes is not required by the deserializer
  - e.g `<test one='aa'two='bb'>` 
- XmlSerializer must be disposed otherwise the pooled buffer will be leaked.
  - XmlSerializer.SerializeStatic gives of an example of how this should be done in a safe way
- Data types can only be classes, not structs.
  - All types must inherit from IXmlSerializable (either manually or added by the source generator) which is actually an abstract class and not an interface
  - Using structs would be possible but I don't think its worth the box
- Types from another assembly can't be used as a field/body. Needs fixing
- All elements in the data to parse must be defined in the type in one way or another, otherwise an exception will be thrown.
  - The deserializer relies on complete parsing and has no way of skipping elements
- Comments within a primitive type body are not stripped and will be included in the parsed value (future consideration...)
  - `<n><!--this will be included in the string-->hi<n>`
- Null strings are currently output exactly the same as empty strings... might need changing
- The source generator emits a parameterless constructor on all XML types that initalizes `List<T>` bodies to an empty list
  - Trying to serialize a null list currently crashes the serializer....
- Agnostic logging through [LibLog](https://github.com/damianh/LibLog)

## Performance
Very simple benchmark, loading a single element and getting the string value of its attribute `attribute`
``` ini

BenchmarkDotNet=v0.12.1, OS=Windows 10.0.17134.1845 (1803/April2018Update/Redstone4)
Intel Core i5-6600K CPU 3.50GHz (Skylake), 1 CPU, 4 logical and 4 physical cores
.NET Core SDK=5.0.100
  [Host]     : .NET Core 5.0.0 (CoreCLR 5.0.20.51904, CoreFX 5.0.20.51904), X64 RyuJIT
  DefaultJob : .NET Core 5.0.0 (CoreC CoreCLRLR 5.0.20.51904, CoreFX 5.0.20.51904), X64 RyuJIT
```
|        Method |         Mean |      Error |     StdDev |  Ratio | RatioSD |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------------- |-------------:|-----------:|-----------:|-------:|--------:|-------:|------:|------:|----------:|
|    ReadBuffer |     95.81 ns |   0.983 ns |   0.872 ns |   1.00 |    0.00 | 0.0178 |     - |     - |      56 B |
|    XmlReader  |  1,866.22 ns |  37.250 ns |  79.383 ns |  19.57 |    0.87 | 3.3216 |     - |     - |   10424 B |
|    XDocument  |  2,286.97 ns |  45.784 ns | 124.560 ns |  24.48 |    1.16 | 3.4313 |     - |     - |   10776 B |
|   XmlDocument |  2,869.48 ns |  44.058 ns |  39.057 ns |  29.96 |    0.60 | 3.9196 |     - |     - |   12328 B |
| XmlSerializer | 10,386.07 ns | 152.481 ns | 142.631 ns | 108.44 |    1.49 | 4.7150 |     - |     - |   14882 B |

## Example data classes
### Simple Attribute
```xml
<test attribute='value'/>
```
```csharp
[XmlCls("test"))]
public partial class Test
{
    [XmlField("attribute")]
    public string m_attribute;
}
```
### Text body
```xml
<test2>
    <name><![CDATA[Hello world]]></name>
</test2>
```
CData can be diabled by setting `useCData` to false for reading and writing
```xml
<test2>
    <name>Hello world</name>
</test2>
```
```csharp
[XmlCls("test2"))]
public partial class Test2
{
    [XmlBody("name")]
    public string m_name;
}
```
### Lists
```xml
<container>
    <listItem name="hey" age='25'/>
    <listItem name="how" age='2'/>
    <listItem name="are" age='4'/>
    <listItem name="you" age='53'/>
</container>
```
```csharp
[XmlCls("listItem"))]
public partial class ListItem
{
    [XmlField("name")]
    public string m_name;
    
    [XmlField("name")]
    public int m_age; // could also be byte, uint etc
}

[XmlCls("container")]
public partial class ListContainer
{
    [XmlBody()]
    public List<ListItem> m_items; // no explicit name, is taken from XmlCls
}
```
### Delimited attributes
```xml
<musicTrack id='5' artists='5,6,1,24,535'>
    <n><![CDATA[Awesome music]]></n>
    <tags>cool</tags>
    <tags>awesome</tags>
    <tags>fresh</tags>
</musicTrack>
```
```csharp
[XmlCls("musicTrack"))]
public partial class MusicTrack
{
    [XmlField("id")]
    public int m_id;
    
    [XmlBody("n")]
    public string m_name;
    
    [XmlField("artists"), XmlSplitStr(',')]
    public List<int> m_artists;
    
    [XmlBody("tags")]
    public List<string> m_tags;
}
```