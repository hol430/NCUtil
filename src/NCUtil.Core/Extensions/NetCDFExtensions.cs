using NCUtil.Core.Models;
using NCUtil.Core.Logging;
using Attribute = NCUtil.Core.Models.Attribute;
using NCUtil.Core.Interop;
using System.Reflection;
using System.Dynamic;

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

    /// <summary>
    /// Name of the 'standard_name' attribute, as specified by the cf spec.
    /// </summary>
    private const string attrStandardName = "standard_name";

    /// <summary>
    /// Standard name of the longitude variable, as specified by the cf spec.
    /// </summary>
    private const string stdLongitude = "longitude";

    /// <summary>
    /// Standard name of the latitude variable, as specified by the cf spec.
    /// </summary>
    private const string stdLatitude = "latitude";

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
        if (variable.TryGetAttribute(name, out Attribute? attribute))
            return attribute!;
        throw new InvalidOperationException($"Variable {variable.Name} has no {name} attribute");
    }

    public static bool TryGetAttribute(this Variable variable, string name, out Attribute? attribute)
    {
        foreach (Attribute attr in variable.Attributes)
        {
            if (attr.Name == name)
            {
                attribute = attr;
                return true;
            }
        }
        attribute = null;
        return false;
    }

    public static bool TryGetVariable(this NetCDFFile file, string name, out Variable? variable)
    {
        foreach (Variable var in file.Variables)
        {
            if (var.Name.Equals(name, StringComparison.InvariantCulture))
            {
                variable = var;
                return true;
            }
        }
        variable = null;
        return false;
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

    public static bool HasStandardName(this Variable variable, string name)
    {
        if (!variable.TryGetAttribute(attrStandardName, out Attribute? attr))
            // This variable doesn't have a standard name attribute.
            return false;

        if (attr == null)
            return false;

        if (attr.Value is string str)
            return str == name;

        // Maybe we should return false if it's not a string attribute.
        return attr.ToString() == name;
    }

    public static bool IsLongitude(this Variable variable)
    {
        if (variable.HasStandardName(stdLongitude))
            return true;
        string name = variable.Name.ToLower();
        return name == "lon" || name == "longitude";
    }

    public static bool IsLatitude(this Variable variable)
    {
        if (variable.HasStandardName(stdLatitude))
            return true;
        string name = variable.Name.ToLower();
        return name == "lat" || name == "latitude";
    }

    private static Dimension FindDimension(this NetCDFFile file, Func<Variable, bool> predicate)
    {
        foreach (Dimension dimension in file.Dimensions)
            // This assumes a variable with the same name as the dimension exists.
            if (file.TryGetVariable(dimension.Name, out Variable? variable))
                if (variable!.Dimensions.Count == 1 && variable.Dimensions[0] == dimension.Name && predicate(variable!))
                    return dimension;
        throw new NotImplementedException($"Failed to find dimension");
    }

    public static Dimension GetLongitudeDimension(this NetCDFFile file)
    {
        return FindDimension(file, IsLongitude);
    }

    public static Dimension GetLatitudeDimension(this NetCDFFile file)
    {
        return FindDimension(file, IsLatitude);
    }

    public static Variable GetLongitudeVariable(this NetCDFFile file)
    {
        return file.Variables.First(IsLongitude);
    }

    public static Variable GetLatitudeVariable(this NetCDFFile file)
    {
        return file.Variables.First(IsLatitude);
    }

    public static int IndexOfValue(this Variable variable, double value, double eps = 1e-6)
    {
        if (variable.Dimensions.Count != 1)
            throw new InvalidOperationException($"Unable to get index of value {value}: variable {variable.Name} is not 1-dimensional");
        MutableRange range = new MutableRange();
        range.Start = 0;
        range.Count = (int)variable.GetLength(); // fixme - this is a bug
        Array data = variable.Read([range]);

        // This will fail if the variable is float!
        double match = data.Cast<double>().MinBy(x => Math.Abs(x - value));
        if (Math.Abs(match - value) > eps)
            return -1;

        // Exact comparison should be possible here - we haven't modified the
        // value we read before.
        return data.Cast<double>().IndexOf(x => x == match);
    }

    /// <summary>
    /// Read a timeseries for a variable which has 3 dimensions: latitude,
    /// longitude, and time.
    /// </summary>
    /// <param name="variable">The variable to read.</param>
    /// <param name="lon">Longitude of the data to be read.</param>
    /// <param name="lat">Latitude of the data to be read.</param>
    public static Array ReadTimeseries(this NetCDFFile file, Variable variable, double lon, double lat)
    {
        if (variable.Dimensions.Count != 3)
            throw new InvalidOperationException($"Can only extract timeseries for 3-dimensional variables. Variable {variable.Name} has {variable.Dimensions.Count} dimensions.");

        Variable timeVariable   = file.GetTimeVariable();
        Variable lonVariable    = file.GetLongitudeVariable();
        Variable latVariable    = file.GetLatitudeVariable();

        Dimension timeDimension = file.GetDimension(timeVariable.Name);
        Dimension lonDimension  = file.GetDimension(lonVariable.Name);
        Dimension latDimension  = file.GetDimension(latVariable.Name);

        int dimLon  = variable.Dimensions.IndexOf(lonDimension.Name, StringComparison.InvariantCulture);
        int dimLat  = variable.Dimensions.IndexOf(latDimension.Name, StringComparison.InvariantCulture);
        int dimTime = variable.Dimensions.IndexOf(timeDimension.Name, StringComparison.InvariantCulture);

        int lonIndex = lonVariable.IndexOfValue(lon);
        int latIndex = latVariable.IndexOfValue(lat);

        if (lonIndex < 0)
            throw new InvalidOperationException($"Longitude {lon} not found in netcdf file");
        if (latIndex < 0)
            throw new InvalidOperationException($"Latitude {lat} not found in netcdf file");

        MutableRange[] hyperslab = new MutableRange[3];
        hyperslab[dimTime] = new MutableRange();
        hyperslab[dimTime].Start = 0;
        hyperslab[dimTime].Count = timeDimension.Size;

        hyperslab[dimLon] = new MutableRange();
        hyperslab[dimLon].Start = lonIndex;
        hyperslab[dimLon].Count = 1;

        hyperslab[dimLat] = new MutableRange();
        hyperslab[dimLat].Start = latIndex;
        hyperslab[dimLat].Count = 1;

        return variable.Read(hyperslab);
    }

}
