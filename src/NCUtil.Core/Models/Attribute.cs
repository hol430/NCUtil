namespace NCUtil.Core.Models;

public class Attribute
{
    public string Name { get; private init; }
    public object Value { get; private init; }
    public Type DataType { get; private init; }

    public Attribute(string name, object value, Type type)
    {
        Name = name;
        Value = value;
        DataType = type;
    }
}
