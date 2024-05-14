namespace NCUtil.Core.Models;

public class Dimension
{
    public string Name { get; private init; }
    public int Size { get; private init; }

    public Dimension(string name, int size)
    {
        Name = name;
        Size = size;
    }
}
