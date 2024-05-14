namespace NCUtil.Core.Models;

public class Variable
{
    public string Name { get; private init; }
    public IReadOnlyList<string> Dimensions { get; private init; }
    public Type DataType { get; private init; }
    public IReadOnlyList<Attribute> Attributes { get; private init; }
    public ZLibOptions Zlib { get; private init; }
    public IReadOnlyList<int> ChunkSizes { get; private init; }
    public ChunkMode Chunking { get; private init; }

    public Variable(string name, IEnumerable<string> dimensions, Type type, IEnumerable<Attribute> attributes, ZLibOptions zlib, ChunkMode chunking, IEnumerable<int> chunkSizes)
    {
        Name = name;
        Dimensions = dimensions.ToList();
        DataType = type;
        Attributes = attributes.ToList();
        Zlib = zlib;
        ChunkSizes = chunkSizes.ToList();
        Chunking = chunking;
    }
}
