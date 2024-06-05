using NCUtil.Core.Models;
using NCUtil.Core.Logging;
using Attribute = NCUtil.Core.Models.Attribute;
using NCUtil.Core.Interop;

namespace NCUtil.Core.Extensions;

public static class NetCDFExtensions
{
    /// <summary>
    /// Name of the time dimension.
    /// </summary>
    private const string dimTime = "time";

    /// <summary>
    /// Name of the time variable.
    /// </summary>
    private const string varTime = "time";

    /// <summary>
    /// Name of the calendar attribute.
    /// </summary>
    private const string attrCalendar = "calendar";

    /// <summary>
    /// Name of the units attribute.
    /// </summary>
    private const string attrUnits = "units";

    private static readonly IDictionary<Type, NCType> typeLookup = new Dictionary<Type, NCType>()
    {
        { typeof(short), NCType.NC_SHORT },
        { typeof(int), NCType.NC_INT },
        { typeof(long), NCType.NC_INT64 },

        { typeof(ushort), NCType.NC_USHORT },
        { typeof(uint), NCType.NC_UINT },
        { typeof(ulong), NCType.NC_UINT64 },

        { typeof(float), NCType.NC_FLOAT },
        { typeof(double), NCType.NC_DOUBLE },

        { typeof(sbyte), NCType.NC_BYTE },
        { typeof(byte), NCType.NC_UBYTE },
        { typeof(char), NCType.NC_CHAR },
        { typeof(string), NCType.NC_STRING },
    };

    private static readonly IDictionary<NCType, int> dataSizes = new Dictionary<NCType, int>()
    {
        { NCType.NC_SHORT, sizeof(short) },
        { NCType.NC_INT, sizeof(int) },
        { NCType.NC_INT64, sizeof(long) },

        { NCType.NC_USHORT, sizeof(ushort) },
        { NCType.NC_UINT, sizeof(uint) },
        { NCType.NC_UINT64, sizeof(ulong) },

        { NCType.NC_FLOAT, sizeof(float) },
        { NCType.NC_DOUBLE, sizeof(double) },

        { NCType.NC_BYTE, sizeof(sbyte) },
        { NCType.NC_UBYTE, sizeof(byte) },
        { NCType.NC_CHAR, sizeof(char) },
        // { NCType.NC_STRING, sizeof(char) },
    };

    public static NCType ToNCType(this Type type)
    {
        if (typeLookup.ContainsKey(type))
            return typeLookup[type];

        throw new Exception($"Type {type.FullName} has no netcdf equivalent");
    }

    public static Type ToType(this NCType type)
    {
        // All values of the NCType enum have an entry in this dictionary, so
        // this should never throw unless ucar add a new NetCDF type in the future.
        // TODO: how does this behave for user-defined types?
        return typeLookup.First(pair => pair.Value == type).Key;
    }

    /// <summary>
    /// Convert a file open mode to an integer that may be passed to native lib.
    /// TBI: NC_SHARE.
    /// </summary>
    /// <param name="mode">File open mode.</param>
    public static OpenMode ToOpenMode(this NetCDFFileMode mode)
    {
        switch (mode)
        {
            case NetCDFFileMode.Read:
                return OpenMode.NC_NOWRITE;
            case NetCDFFileMode.Write:
            case NetCDFFileMode.Append:
                return OpenMode.NC_WRITE;
            default:
                throw new NotImplementedException($"Unknown file mode: {mode}");
        }
    }

    public static int DataSize(this NCType type)
    {
        if (dataSizes.ContainsKey(type))
            return dataSizes[type];
        throw new Exception($"Unknown data size for type {type}");
    }

    public static bool IsTime(this Dimension dimension)
    {
        return dimension.Name == dimTime;
    }

    public static Dimension GetTimeDimension(this NetCDFFile file)
    {
        return file.GetDimension(dimTime);
    }

    public static Variable GetTimeVariable(this NetCDFFile file)
    {
        return file.GetVariable(varTime);
    }

    public static void CopyMetadataTo(this NetCDFFile from, NetCDFFile to)
    {
        foreach (Attribute attribute in from.Attributes)
        {
            Log.Diagnostic("Setting attribute {0} in output file", attribute.Name);
            to.CreateAttribute(attribute.Name, attribute.Value, attribute.DataType);
        }
    }

    public static Attribute GetAttribute(this Variable variable, string name)
    {
        foreach (Attribute attribute in variable.Attributes)
            if (attribute.Name == name)
                return attribute;
        throw new InvalidOperationException($"Variable {variable.Name} has no {name} attribute");
    }

    public static Calendar ParseCalendar(this string attribute)
    {
        switch (attribute)
        {
            case "standard":
            case "gregorian":
                return Calendar.Standard;
            case "proleptic_gregorian":
                return Calendar.ProlepticGregorian;
            case "julian":
                return Calendar.Julian;
            case "noleap":
            case "365_day":
                return Calendar.NoLeap;
            case "360_day":
                return Calendar.EqualLength;
            case "none":
            case "":
                return Calendar.None;
            default:
                throw new InvalidOperationException($"Unable to parse calendar type from attribute value: '{attribute}'");
        }
    }

    public static string ReadStringAttribute(this Variable variable, string name)
    {
        // Get the attribute.
        Attribute attribute = variable.GetAttribute(name);

        // Verify that it's a string attribute.
        if (attribute.Value is not string)
            throw new InvalidOperationException($"Unable to read attribute {name} of variable {variable.Name}: attribute type in netcdf file is of type {attribute.DataType.ToFriendlyName()}, and the attribute value is of type {attribute.Value.GetType().ToFriendlyName()}");

        return (string)attribute.Value;
    }

    public static Calendar GetCalendar(this Variable variable)
    {
        if (variable.Name != varTime)
            throw new InvalidOperationException($"Attempted to get calendar for non-time variable");

        string value = variable.ReadStringAttribute(attrCalendar);
        return ParseCalendar(value);
    }

    public static string GetUnits(this Variable variable)
    {
        return variable.ReadStringAttribute(attrUnits);
    }

    public static string EnumToString(this Enum e)
    {
        return Enum.GetName(e.GetType(), e)!;
    }
}
