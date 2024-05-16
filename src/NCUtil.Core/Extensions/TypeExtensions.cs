namespace NCUtil.Core.Extensions;

public static class TypeExtensions
{
    private static readonly IDictionary<Type, string> aliases = new Dictionary<Type, string>()
    {
        { typeof(byte), "byte" },
        { typeof(sbyte), "sbyte" },
        { typeof(short), "short" },
        { typeof(ushort), "ushort" },
        { typeof(int), "int" },
        { typeof(uint), "uint" },
        { typeof(long), "long" },
        { typeof(ulong), "ulong" },
        { typeof(float), "float" },
        { typeof(double), "double" },
        { typeof(decimal), "decimal" },
        { typeof(object), "object" },
        { typeof(bool), "bool" },
        { typeof(char), "char" },
        { typeof(string), "string" },
        { typeof(void), "void" }
    };

    public static string ToFriendlyName(this Type type)
    {
        if (aliases.ContainsKey(type))
            return aliases[type];

        Type? nullable = Nullable.GetUnderlyingType(type);
        if (nullable != null)
        {
            // Nullable type.
            return $"{nullable.ToFriendlyName()}?";
        }
        if (type.IsGenericType)
        {
            string result = type.Name;
            int backtick = result.IndexOf('`');
            if (backtick > 0)
                result = result.Remove(backtick);
            IEnumerable<string> args = type
                .GetGenericArguments()
                .Select(t => t.ToFriendlyName());
            return $"{result}<{string.Join(", ", args)}>";
        }
        if (type.IsArray)
            return $"{type.GetElementType()!.ToFriendlyName()}[]";
        return type.Name;
    }
    
}
