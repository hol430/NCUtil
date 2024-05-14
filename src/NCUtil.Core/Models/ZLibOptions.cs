namespace NCUtil.Core.Models;

public class ZLibOptions
{
    public bool Shuffle { get; private init; }
    public int DeflateLevel { get; private init; }

    public ZLibOptions(bool shuffle, int deflateLevel)
    {
        Shuffle = shuffle;
        DeflateLevel = deflateLevel;
    }
}
