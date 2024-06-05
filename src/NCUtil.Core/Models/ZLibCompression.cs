namespace NCUtil.Core.Models;

public class ZLibCompression : ICompressionAlgorithm
{
    public bool Shuffle { get; private init; }
    public int DeflateLevel { get; private init; }

    public ZLibCompression(bool shuffle, int deflateLevel)
    {
        Shuffle = shuffle;
        DeflateLevel = deflateLevel;
    }

    public override string ToString()
    {
        return $"zlib L{DeflateLevel} (shuffle = {Shuffle})";
    }
}
