using Microsoft.Research.Science.Data;
using Microsoft.Research.Science.Data.NetCDF4;
using Microsoft.Research.Science.Data.Imperative;
using NetCDFInterop;
using NCUtil.Core.Logging;
using System.Reflection;

namespace NCUtil.Core.Extensions;

public static class DataSetExtensions
{
    private static readonly IDictionary<Type, NcType> typeLookup = new Dictionary<Type, NcType>()
    {
        { typeof(short), NcType.NC_SHORT },
        { typeof(int), NcType.NC_INT },
        { typeof(long), NcType.NC_INT64 },

        { typeof(ushort), NcType.NC_USHORT },
        { typeof(uint), NcType.NC_UINT },
        { typeof(ulong), NcType.NC_UINT64 },

        { typeof(float), NcType.NC_FLOAT },
        { typeof(double), NcType.NC_DOUBLE },

        { typeof(sbyte), NcType.NC_BYTE },
        { typeof(byte), NcType.NC_UBYTE },
        { typeof(char), NcType.NC_CHAR },
        { typeof(string), NcType.NC_STRING },
    };

    private static readonly IDictionary<NcType, int> dataSizes = new Dictionary<NcType, int>()
    {
        { NcType.NC_SHORT, sizeof(short) },
        { NcType.NC_INT, sizeof(int) },
        { NcType.NC_INT64, sizeof(long) },

        { NcType.NC_USHORT, sizeof(ushort) },
        { NcType.NC_UINT, sizeof(uint) },
        { NcType.NC_UINT64, sizeof(ulong) },

        { NcType.NC_FLOAT, sizeof(float) },
        { NcType.NC_DOUBLE, sizeof(double) },

        { NcType.NC_BYTE, sizeof(sbyte) },
        { NcType.NC_UBYTE, sizeof(byte) },
        { NcType.NC_CHAR, sizeof(char) },
        // { NcType.NC_STRING, sizeof(char) },
    };

    /// <summary>
    /// Name of the time dimension.
    /// </summary>
    private const string dimTime = "time";

    /// <summary>
    /// Name of the ncid variable in sdslite source...fixme: blegh
    /// </summary>
    private const string ncidName = "ncid";

    public static bool IsTime(this Dimension dimension)
    {
        return dimension.Name == dimTime;
    }

    public static Dimension GetDimension(this DataSet dataset, string name)
    {
        try
        {
            return dataset.Dimensions[name];
        }
        catch (Exception error)
        {
            throw new Exception($"Failed to get dimension {name}", error);
        }
    }

    public static Dimension GetTimeDimension(this DataSet dataset)
    {
        return dataset.GetDimension(dimTime);
    }

    public static int CountTimesteps(this DataSet dataset)
    {
        Dimension time = dataset.GetTimeDimension();
        return time.Length;
    }

    public static int GetNcId(this DataSet dataset)
    {
        if ( !(dataset is NetCDFDataSet netcdf) )
            throw new Exception($"Dataset is not a netcdf file");
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo? field = typeof(NetCDFDataSet).GetField(ncidName, flags);
        if (field == null)
            throw new Exception("Unable to get netcdf file id");
        return (int)field.GetValue(netcdf)!;
    }

    public static void CreateDimension(this DataSet dataset, string name, int size)
    {
        if (dataset.Dimensions.FindIndex(name) >= 0)
            throw new InvalidOperationException($"Dimension {name} cannot be created because it already exists");
        if (!(dataset is NetCDFDataSet netcdf))
            throw new InvalidOperationException($"Dataset is not a netcdf file");

        Log.Debug("Creating dimension {0} with size {1}", name, size);
        int res = NetCDF.nc_def_dim(netcdf.GetNcId(), name, size, out _);
        if (res != 0)
        {
            string error = NetCDF.nc_strerror(res);
            throw new Exception($"Failed to create dimension {name} with size {size}: {error}");
        }
        Log.Diagnostic("Successfully created dimension {0} with size {1}", name, size);
    }

    public static NcType ToNcType(this Type type)
    {
        if (typeLookup.ContainsKey(type))
            return typeLookup[type];

        throw new Exception($"Type {type.FullName} has no netcdf equivalent");
    }

    public static Type ToType(this NcType type)
    {
        // All values of the NcType enum have an entry in this dictionary, so
        // this should never throw unless ucar add a new NetCDF type in the future.
        // TODO: how does this behave for user-defined types?
        return typeLookup.First(pair => pair.Value == type).Key;
    }

    public static void CreateVariable(this DataSet dataset, string name, Type type, IReadOnlyList<string> dimensions)
    {
        
        if (!(dataset is NetCDFDataSet netcdf))
            throw new InvalidOperationException($"Dataset is not a netcdf file");

        int[] dimids = dimensions.Select(d => netcdf.Dimensions.FindIndex(d)).ToArray();
        if (dimids.Any(d => d < 0))
            throw new Exception($"The following dimensions do not exist: {string.Join(", ", dimensions.Zip(dimids).Where((x, y) => y < 0).Select((x, y) => x))}");

        NcType ncType = type.ToNcType();

        Log.Debug("Creating variable {0} with dimensions: {1}", name, string.Join(", ", dimensions));
        int res = NetCDF.nc_def_var(netcdf.GetNcId(), name, ncType, dimids, out _);
        if (res != 0)
        {
            string error = NetCDF.nc_strerror(res);
            throw new Exception($"Failed to create variable {name}: {error}");
        }
        Log.Diagnostic("Successfully created variable {0}", name, string.Join(", ", dimensions));
    }

    public static int DataSize(this NcType type)
    {
        if (dataSizes.ContainsKey(type))
            return dataSizes[type];
        throw new Exception($"Unknown data size for type {type}");
    }
}
