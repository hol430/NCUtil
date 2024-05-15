using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using NCUtil.Core.Extensions;
using NCUtil.Core.Logging;
using NCUtil.Core.Models;
using NetCDFInterop;
using Attribute = NCUtil.Core.Models.Attribute;
using Range = NCUtil.Core.Models.Range;

namespace NCUtil.Core.Interop;

internal static class NetCDFManaged
{
    private static readonly Decoder utfDecoder;
    private static readonly Encoder utfEncoder;

    static NetCDFManaged()
    {
        Encoding encoding = new UTF8Encoding(false);
        utfDecoder = encoding.GetDecoder();
        utfEncoder = encoding.GetEncoder();
    }

    /// <summary>
    /// Open the specified NetCDF file and return its file ID.
    /// </summary>
    /// <param name="file">Path to the NetCDF file.</param>
    /// <param name="mode">File open mode.</param>
    /// <returns></returns>
    public static int OpenNetCDF(string file, NetCDFFileMode mode)
    {
        if (mode == NetCDFFileMode.Append && !File.Exists(file))
            throw new FileNotFoundException($"Unable to open netcdf file: file does not exist: {file}");

        int result = NetCDF.nc_open(file, mode.ToCreateMode(), out int id);
        CheckResult(result, "Failed to open netcdf file");

        Log.Diagnostic($"Successfully opened netcdf file in mode {0}: '{1}'", mode, file);
        return id;
    }

    public static int CreateNetCDF(string file)
    {
        Log.Debug("Creating NetCDF file: '{0}'...", file);

        int res = NetCDF.nc_create(file, CreateMode.NC_NETCDF4 | CreateMode.NC_CLOBBER, out int id);
        CheckResult(res, "Failed to create file {0}", file);

        Log.Debug("Successfully created NetCDF file: '{0}'", file);
        return id;
    }

    /// <summary>
    /// Close the specified NetCDF file.
    /// </summary>
    /// <param name="ncid">ID of the file to be closed.</param>
    /// <param name="path">Optional path of the netcdf file, used only for logging purposes.</param>
    public static void CloseNetCDF(int ncid, string? path = null)
    {
        path ??= ncid.ToString();

        Log.Debug("Closing netcdf file: {0}...", path);

        int result = NetCDF.nc_close(ncid);
        CheckResult(result, "Failed to close netcdf file: {0}", path);

        Log.Diagnostic("Successfully closed netcdf file: {0}", path);
    }

    public static int GetNumDimensions(int ncid)
    {
        Log.Debug("Calling nc_inq_ndims()...");

        int res = NetCDFNative.nc_inq_ndims(ncid, out int ndim);
        CheckResult(res, "Failed to get dimensions");

        Log.Debug("Call to nc_inq_ndims() was successful");

        return ndim;
    }

    public static int[] GetDimensionIds(int ncid)
    {
        int ndim = GetNumDimensions(ncid);

        int[] dimids = new int[ndim];
        int res = NetCDFNative.nc_inq_dimids(ncid, out int ndim2, dimids, 0);
        CheckResult(res, "Failed to get dimension IDs");

        // Not sure what could cause this. Parallel IO? File corruption?
        // Let's leave this here as a sanity check anyway.
        if (ndim != ndim2)
            throw new InvalidOperationException($"nc_inq_dimids() returned different dimension length ({ndim2}) than nc_inq_ndims() ({ndim})");

        return dimids;
    }

    public static int GetDimensionLength(int ncid, int dimid)
    {
        Log.Debug("Calling nc_inq_dimlen() for dimension {0}", dimid);

        int res = NetCDF.nc_inq_dimlen(ncid, dimid, out nint length);
        CheckResult(res, "Failed to get length of dimension {0}: {1}", dimid);

        Log.Debug("Call to nc_inq_dimlen() was successful for dimension {0} and returned {1}", dimid, (int)length);
        return (int)length;
    }

    public static string GetDimensionName(int ncid, int dimid)
    {
        Log.Debug("Calling nc_inq_dimname() for dimension {0}", dimid);

        int res = NetCDF.nc_inq_dimname(ncid, dimid, out string name);
        CheckResult(res, "Failed to get name of dimension with ID {0}", dimid);

        Log.Debug("Call to nc_inq_dimname() was successful for dimension {0}: {1}", dimid, name);
        return name;
    }

    public static int GetDimensionID(int ncid, string name)
    {
        Log.Debug("Calling nc_inq_dimid() for dimension {0}", name);

        int res = NetCDF.nc_inq_dimid(ncid, name, out int dimid);
        CheckResult(res, "Failed to get ID of dimension with name {0}", name);

        Log.Debug("Call to nc_inq_dimid() was successful; dimension {0} has ID {1}", name, dimid);
        return dimid;
    }

    public static int GetVariableID(int ncid, string name)
    {
        Log.Debug("Calling nc_inq_varid() for variable {0}", name);

        int res = NetCDFNative.nc_inq_varid(ncid, name, out int varid);
        CheckResult(res, "Failed to get ID of variable {0}", name);

        Log.Debug("Call to nc_inq_varid() was successful; variable {0} has ID {1}", name, varid);
        return varid;
    }

    public static int[] GetVariableDimensionIDs(int ncid, int varid)
    {
        int ndim = GetVariableNumDimensions(ncid, varid);
        int[] dimids = new int[ndim];

        Log.Debug("Calling nc_inq_vardimid() for variable {0}", varid);

        int res = NetCDF.nc_inq_vardimid(ncid, varid, dimids);
        CheckResult(res, "Failed to get dimension IDs for variable {0}", varid);

        Log.Debug("Call to nc_inq_vardimid() was successful");
        return dimids;
    }

    public static void CreateDimension(int ncid, string name, int length)
    {
        if (length < 0)
            throw new InvalidOperationException($"Attempted to create dimension with negative length: {length}");

        if (length == 0)
            length = NcConst.NC_UNLIMITED;

        Log.Debug("Calling nc_def_dim() to create dimension {0} with length {1}", name, length);

        int res = NetCDF.nc_def_dim(ncid, name, (nint)length, out _);
        CheckResult(res, "Failed to create dimension with name {0}", name);

        Log.Debug("Successfully created dimension {0} with length {1}", name, length);
    }

    public static int GetNumVars(int ncid)
    {
        Log.Debug("Calling nc_inq_nvars()");

        int res = NetCDFNative.nc_inq_nvars(ncid, out int nvars);
        CheckResult(res, "nc_inq_nvars(): failed to get number of variables");

        Log.Debug("nc_inq_nvars(): file contains {0} variables", nvars);
        return nvars;
    }

    public static int[] GetVariableIDs(int ncid)
    {
        int nvar = GetNumVars(ncid);
        int[] varids = new int[nvar];

        Log.Debug("Calling nc_inq_varids()");

        int res = NetCDFNative.nc_inq_varids(ncid, out int nvar2, varids);
        CheckResult(res, "nc_inq_varids()");

        if (nvar != nvar2)
            throw new Exception($"Number of variables appears to have changed from {nvar} to {nvar2}");

        return varids;
    }

    public static int GetVariableNumDimensions(int ncid, int varid)
    {
        Log.Debug("Calling nc_inq_varndims() for variable {0}", varid);

        int res = NetCDF.nc_inq_varndims(ncid, varid, out int ndim);
        CheckResult(res, "nc_inq_varndims()");

        Log.Debug("nc_inq_varndims(): variable {0} has {1} dimensions", varid, ndim);
        return ndim;
    }

    public static void GetVariable(int ncid, int varid, out string name, out NcType type, out int[] dimids, out int nattr)
    {
        Log.Debug("Calling nc_inq_varname()");

        int ndims = GetVariableNumDimensions(ncid, varid);
        dimids = new int[ndims];

        int res = NetCDF.nc_inq_var(ncid, varid, out name, out type, out int ndim2, dimids, out nattr);
        CheckResult(res, "nc_inq_var(), varid = {0}", varid);

        if (ndims != ndim2)
            throw new Exception($"Number of dimensions for variable {name} has changed from {ndims} to {ndim2}");

        Log.Debug("Call to nc_inq_var() was successful");
    }

    public static NcType GetVariableType(int ncid, int varid)
    {
        GetVariable(ncid, varid, out _, out NcType type, out _, out _);
        return type;
    }

    private static short[] ReadVaraShort(int ncid, int varid, Range[] hyperslab)
    {
        nint[] start = hyperslab.Select(h => (nint)h.Start).ToArray();
        nint[] count = hyperslab.Select(h => (nint)h.Count).ToArray();
        long n = hyperslab.Product(h => h.Count);
        short[] data = new short[n];

        Log.Debug("Reading {0} elements from variable {1}", n, varid);

        int res = NetCDF.nc_get_vara_short(ncid, varid, start, count, data);
        CheckResult(res, "Failed to read from variable {0}", varid);

        Log.Debug("Successfully read from variable {0}", varid);
        return data;
    }

    private static int[] ReadVaraInt(int ncid, int varid, Range[] hyperslab)
    {
        nint[] start = hyperslab.Select(h => (nint)h.Start).ToArray();
        nint[] count = hyperslab.Select(h => (nint)h.Count).ToArray();
        long n = hyperslab.Product(h => h.Count);
        int[] data = new int[n];

        Log.Debug("Reading {0} elements from variable {1}", n, varid);

        int res = NetCDF.nc_get_vara_int(ncid, varid, start, count, data);
        CheckResult(res, "Failed to read from variable {0}", varid);

        Log.Debug("Successfully read from variable {0}", varid);
        return data;
    }

    private static long[] ReadVaraInt64(int ncid, int varid, Range[] hyperslab)
    {
        nint[] start = hyperslab.Select(h => (nint)h.Start).ToArray();
        nint[] count = hyperslab.Select(h => (nint)h.Count).ToArray();
        long n = hyperslab.Product(h => h.Count);
        long[] data = new long[n];

        Log.Debug("Reading {0} elements from variable {1}", n, varid);

        int res = NetCDF.nc_get_vara_longlong(ncid, varid, start, count, data);
        CheckResult(res, "Failed to read from variable {0}", varid);

        Log.Debug("Successfully read from variable {0}", varid);
        return data;
    }

    private static ushort[] ReadVaraUshort(int ncid, int varid, Range[] hyperslab)
    {
        nint[] start = hyperslab.Select(h => (nint)h.Start).ToArray();
        nint[] count = hyperslab.Select(h => (nint)h.Count).ToArray();
        long n = hyperslab.Product(h => h.Count);
        ushort[] data = new ushort[n];

        Log.Debug("Reading {0} elements from variable {1}", n, varid);

        int res = NetCDF.nc_get_vara_ushort(ncid, varid, start, count, data);
        CheckResult(res, "Failed to read from variable {0}", varid);

        Log.Debug("Successfully read from variable {0}", varid);
        return data;
    }

    private static uint[] ReadVaraUint(int ncid, int varid, Range[] hyperslab)
    {
        nint[] start = hyperslab.Select(h => (nint)h.Start).ToArray();
        nint[] count = hyperslab.Select(h => (nint)h.Count).ToArray();
        long n = hyperslab.Product(h => h.Count);
        uint[] data = new uint[n];

        Log.Debug("Reading {0} elements from variable {1}", n, varid);

        int res = NetCDF.nc_get_vara_uint(ncid, varid, start, count, data);
        CheckResult(res, "Failed to read from variable {0}", varid);

        Log.Debug("Successfully read from variable {0}", varid);
        return data;
    }

    private static ulong[] ReadVaraUint64(int ncid, int varid, Range[] hyperslab)
    {
        nint[] start = hyperslab.Select(h => (nint)h.Start).ToArray();
        nint[] count = hyperslab.Select(h => (nint)h.Count).ToArray();
        long n = hyperslab.Product(h => h.Count);
        ulong[] data = new ulong[n];

        Log.Debug("Reading {0} elements from variable {1}", n, varid);

        int res = NetCDF.nc_get_vara_ulonglong(ncid, varid, start, count, data);
        CheckResult(res, "Failed to read from variable {0}", varid);

        Log.Debug("Successfully read from variable {0}", varid);
        return data;
    }

    private static float[] ReadVaraFloat(int ncid, int varid, Range[] hyperslab)
    {
        nint[] start = hyperslab.Select(h => (nint)h.Start).ToArray();
        nint[] count = hyperslab.Select(h => (nint)h.Count).ToArray();
        long n = hyperslab.Product(h => h.Count);
        float[] data = new float[n];

        Log.Debug("Reading {0} elements from variable {1}", n, varid);

        int res = NetCDF.nc_get_vara_float(ncid, varid, start, count, data);
        CheckResult(res, "Failed to read from variable {0}", varid);

        Log.Debug("Successfully read from variable {0}", varid);
        return data;
    }

    private static double[] ReadVaraDouble(int ncid, int varid, Range[] hyperslab)
    {
        nint[] start = hyperslab.Select(h => (nint)h.Start).ToArray();
        nint[] count = hyperslab.Select(h => (nint)h.Count).ToArray();
        long n = hyperslab.Product(h => h.Count);
        double[] data = new double[n];

        Log.Debug("Reading {0} elements from variable {1}", n, varid);

        int res = NetCDF.nc_get_vara_double(ncid, varid, start, count, data);
        CheckResult(res, "Failed to read from variable {0}", varid);

        Log.Debug("Successfully read from variable {0}", varid);
        return data;
    }

    private static sbyte[] ReadVaraByte(int ncid, int varid, Range[] hyperslab)
    {
        nint[] start = hyperslab.Select(h => (nint)h.Start).ToArray();
        nint[] count = hyperslab.Select(h => (nint)h.Count).ToArray();
        long n = hyperslab.Product(h => h.Count);
        sbyte[] data = new sbyte[n];

        Log.Debug("Reading {0} elements from variable {1}", n, varid);

        int res = NetCDF.nc_get_vara_schar(ncid, varid, start, count, data);
        CheckResult(res, "Failed to read from variable {0}", varid);

        Log.Debug("Successfully read from variable {0}", varid);
        return data;
    }

    private static byte[] ReadVaraUbyte(int ncid, int varid, Range[] hyperslab)
    {
        nint[] start = hyperslab.Select(h => (nint)h.Start).ToArray();
        nint[] count = hyperslab.Select(h => (nint)h.Count).ToArray();
        long n = hyperslab.Product(h => h.Count);
        byte[] data = new byte[n];

        Log.Debug("Reading {0} elements from variable {1}", n, varid);

        int res = NetCDF.nc_get_vara_ubyte(ncid, varid, start, count, data);
        CheckResult(res, "Failed to read from variable {0}", varid);

        Log.Debug("Successfully read from variable {0}", varid);
        return data;
    }

    private static byte[] ReadVaraChar(int ncid, int varid, Range[] hyperslab)
    {
        nint[] start = hyperslab.Select(h => (nint)h.Start).ToArray();
        nint[] count = hyperslab.Select(h => (nint)h.Count).ToArray();
        long n = hyperslab.Product(h => h.Count);
        byte[] data = new byte[n];

        Log.Debug("Reading {0} elements from variable {1}", n, varid);

        int res = NetCDF.nc_get_vara_text(ncid, varid, start, count, data);
        CheckResult(res, "Failed to read from variable {0}", varid);

        Log.Debug("Successfully read from variable {0}", varid);
        return data;
    }

    private static string[] ReadVaraString(int ncid, int varid, Range[] hyperslab)
    {
        nint[] start = hyperslab.Select(h => (nint)h.Start).ToArray();
        nint[] count = hyperslab.Select(h => (nint)h.Count).ToArray();
        long n = hyperslab.Product(h => h.Count);
        string[] data = new string[n];

        Log.Debug("Reading {0} elements from variable {1}", n, varid);

        int res = NetCDF.nc_get_vara_string(ncid, varid, start, count, data);
        CheckResult(res, "Failed to read from variable {0}", varid);

        Log.Debug("Successfully read from variable {0}", varid);
        return data;
    }

    public static Array ReadVariable(int ncid, int varid, Range[] hyperslab)
    {
        GetVariable(ncid, varid, out string name, out NcType type, out int[] dimids, out _);
        int ndim = dimids.Length;
        if (ndim != hyperslab.Length)
            throw new InvalidOperationException($"Unable to read from variable {name}: only {hyperslab.Length} hyperslabs were specified, but variable has {ndim} dimensions");

        switch (type)
        {
            case NcType.NC_SHORT:
                return ReadVaraShort(ncid, varid, hyperslab);
            case NcType.NC_INT:
                return ReadVaraInt(ncid, varid, hyperslab);
            case NcType.NC_INT64:
                return ReadVaraInt64(ncid, varid, hyperslab);

            case NcType.NC_USHORT:
                return ReadVaraUshort(ncid, varid, hyperslab);
            case NcType.NC_UINT:
                return ReadVaraUint(ncid, varid, hyperslab);
            case NcType.NC_UINT64:
                return ReadVaraUint64(ncid, varid, hyperslab);

            case NcType.NC_FLOAT:
                return ReadVaraFloat(ncid, varid, hyperslab);
            case NcType.NC_DOUBLE:
                return ReadVaraDouble(ncid, varid, hyperslab);

            case NcType.NC_BYTE:
                return ReadVaraByte(ncid, varid, hyperslab);
            case NcType.NC_UBYTE:
                return ReadVaraUbyte(ncid, varid, hyperslab);
            case NcType.NC_CHAR:
                return ReadVaraChar(ncid, varid, hyperslab);
            case NcType.NC_STRING:
                return ReadVaraChar(ncid, varid, hyperslab);
            default:
                throw new NotImplementedException($"Unable to read from variable {name}: unsupported type: {type}");
        }
    }

    /// <summary>
    /// Get the zlib configuration for the specified variable. If zlib
    /// compression is disabled, this will return a deflate level of 0.
    /// </summary>
    public static ZLibOptions GetZLibOptions(int ncid, int varid)
    {
        Log.Debug("Checking compression settings for variable {0}", varid);

        int res = NetCDFNative.nc_inq_var_deflate(ncid, varid, out int shuf, out int def, out int deflateLevel);
        CheckResult(res, "Failed to check zlib compression for variable {0}", varid);

        Log.Debug("Successfully read compression settings for variable {0}", varid);

        return new ZLibOptions(shuf == 1, deflateLevel);
    }

    public static void GetChunkSizes(int ncid, int varid, out ChunkMode mode, out int[] chunks)
    {
        Log.Debug("Checking chunk sizes for variable {0}", varid);

        int res = NetCDFNative.nc_inq_var_chunking(ncid, varid, out int storagep, out nint chunksizesp);
        CheckResult(res, "Failed to read chunk sizes for variable {0}", varid);

        int ndim = GetVariableNumDimensions(ncid, varid);

        chunks = new int[ndim];
        for (int i = 0; i < ndim; i++)
            chunks[i] = (int)(chunksizesp + i);

        mode = (ChunkMode)storagep;

        Log.Debug("Variable {0} has chunk sizes: {1}", varid, chunksizesp);
    }

    public static string GetAttributeName(int ncid, int varid, int number)
    {
        Log.Debug("Calling nc_inq_attname()");

        int res = NetCDF.nc_inq_attname(ncid, varid, number, out string name);
        CheckResult(res, "nc_inq_attname");

        Log.Debug("Variable {0} attribute {1} has name {2}", varid, number, name);
        return name;
    }

    public static object GetAttributeValue<T>(int ncid, int varid, string name, int length, Func<int, int, string, T[], int> nativeFunc)
    {
        Log.Debug("Calling nc_get_att_{0}()...", typeof(T).Name);
        T[] data = new T[length];
        int res = nativeFunc(ncid, varid, name, data);
        CheckResult(res, "nc_get_att_{0}()", typeof(T).Name);
        Log.Debug("Successfully read {0}[] value of attribute {1}", typeof(T).Name, name);

        if (data.Length == 1 && data[0] != null)
            return data[0]!;

        return data;
    }

    public static object GetAttributeValue(int ncid, int varid, string name, NcType type, int length)
    {
        switch (type)
        {
            case NcType.NC_SHORT:
                return GetAttributeValue<short>(ncid, varid, name, length, NetCDF.nc_get_att_short);
            case NcType.NC_INT:
                return GetAttributeValue<int>(ncid, varid, name, length, NetCDF.nc_get_att_int);
            case NcType.NC_INT64:
                return GetAttributeValue<long>(ncid, varid, name, length, NetCDF.nc_get_att_longlong);

            case NcType.NC_USHORT:
                return GetAttributeValue<ushort>(ncid, varid, name, length, NetCDF.nc_get_att_ushort);
            case NcType.NC_UINT:
                return GetAttributeValue<uint>(ncid, varid, name, length, NetCDF.nc_get_att_uint);
            case NcType.NC_UINT64:
                return GetAttributeValue<ulong>(ncid, varid, name, length, NetCDF.nc_get_att_ulonglong);

            case NcType.NC_FLOAT:
                return GetAttributeValue<float>(ncid, varid, name, length, NetCDF.nc_get_att_float);
            case NcType.NC_DOUBLE:
                return GetAttributeValue<double>(ncid, varid, name, length, NetCDF.nc_get_att_double);

            case NcType.NC_BYTE:
                return GetAttributeValue<sbyte>(ncid, varid, name, length, NetCDF.nc_get_att_schar);
            case NcType.NC_UBYTE:
                return GetAttributeValue<byte>(ncid, varid, name, length, NetCDF.nc_get_att_uchar);
            case NcType.NC_CHAR:
                return ReadCharAttributeValue(ncid, varid, name, length);
            case NcType.NC_STRING:
                return GetAttributeValue<string>(ncid, varid, name, length, NetCDF.nc_get_att_string);
            default:
                throw new InvalidOperationException($"Unknown attribute type: {type}");
        }
    }

    private static string ReadCharAttributeValue(int ncid, int varid, string name, int length)
    {
        Log.Debug("Reading value of char attribute {0}", name);

        int res = NetCDF.nc_get_att_text(ncid, varid, name, out string value, length);
        CheckResult(res, "Failed to read char attribute: {0}", name);

        Log.Debug("Successfully read value of char attribute {0}", name);
        return value;
    }

    public static Attribute GetAttribute(int ncid, int varid, int number)
    {
        string name = GetAttributeName(ncid, varid, number);

        Log.Debug("Calling nc_inq_att()");
        int res = NetCDF.nc_inq_att(ncid, varid, name, out NcType nctype, out nint plength);
        CheckResult(res, "nc_inq_att()");

        int length = (int)plength;
        Type type = nctype.ToType();
        Log.Debug("Attribute {0} has type {1} and length {2}", name, type.Name, length);

        object value = GetAttributeValue(ncid, varid, name, nctype, length);

        return new Attribute(name, value, type);
    }

    private static void SetAttributeValue<T>(int ncid, int varid, string name, object value, Func<int, int, string, T[], int> nativeFunc)
    {
        Log.Debug("Calling nc_put_att_{0}()", typeof(T).Name);

        T[] array;
        if (typeof(T).IsAssignableFrom(value.GetType()))
            array = new T[1] { (T)value };
        else if (value.GetType().IsArray)
            array = (T[])value;
        else if (value is IEnumerable<T>)
            array = ((IEnumerable<T>)value).ToArray();
        else
            throw new InvalidOperationException($"Attempted to set attribute {name} as {typeof(T).Name} attribute, but value is of type {value.GetType().Name}");

        int res = nativeFunc(ncid, varid, name, array);
        CheckResult(res, "Failed to set attribute {0}", name);

        Log.Debug("Successfully set attribute {0}", name);
    }

    private static void SetChunking(int ncid, int varid, ChunkMode mode, int[]? chunkSizes)
    {
        nint[]? ptrs = chunkSizes?.Select(c => (nint)c).ToArray();
        Log.Debug("Calling nc_def_var_chunking() for variable {0}", varid);

        int res = NetCDF.nc_def_var_chunking(ncid, varid, (int)mode, ptrs);
        CheckResult(res, "Failed to set chunk sizes for variable {0}", varid);

        Log.Debug("Successfully set chunk sizes for variable {0}", varid);
    }

    public static void SetVariableChunking(int ncid, int varid, int[] chunkSizes)
    {
        SetChunking(ncid, varid, ChunkMode.Chunked, chunkSizes);
    }

    public static void MakeContiguous(int ncid, int varid)
    {
        SetChunking(ncid, varid, ChunkMode.Contiguous, null);
    }

    public static void MakeCompact(int ncid, int varid)
    {
        SetChunking(ncid, varid, ChunkMode.Compact, null);
    }

    private static int SetCharAttributeValue(int ncid, int varid, string name, char[] value)
    {
        return NetCDF.nc_put_att_text(ncid, varid, name, new string(value));
    }

    public static void SetAttribute(int ncid, int varid, Attribute attribute)
    {
        switch (attribute.DataType.ToNcType())
        {
            case NcType.NC_SHORT:
                SetAttributeValue<short>(ncid, varid, attribute.Name, attribute.Value, NetCDF.nc_put_att_short);
                break;
            case NcType.NC_INT:
                SetAttributeValue<int>(ncid, varid, attribute.Name, attribute.Value, NetCDF.nc_put_att_int);
                break;
            case NcType.NC_INT64:
                SetAttributeValue<long>(ncid, varid, attribute.Name, attribute.Value, NetCDF.nc_put_att_longlong);
                break;

            case NcType.NC_USHORT:
                SetAttributeValue<ushort>(ncid, varid, attribute.Name, attribute.Value, NetCDF.nc_put_att_ushort);
                break;
            case NcType.NC_UINT:
                SetAttributeValue<uint>(ncid, varid, attribute.Name, attribute.Value, NetCDF.nc_put_att_uint);
                break;
            case NcType.NC_UINT64:
                SetAttributeValue<ulong>(ncid, varid, attribute.Name, attribute.Value, NetCDF.nc_put_att_ulonglong);
                break;

            case NcType.NC_FLOAT:
                SetAttributeValue<float>(ncid, varid, attribute.Name, attribute.Value, NetCDF.nc_put_att_float);
                break;
            case NcType.NC_DOUBLE:
                SetAttributeValue<double>(ncid, varid, attribute.Name, attribute.Value, NetCDF.nc_put_att_double);
                break;

            case NcType.NC_BYTE:
                SetAttributeValue<sbyte>(ncid, varid, attribute.Name, attribute.Value, NetCDF.nc_put_att_schar);
                break;
            case NcType.NC_UBYTE:
                SetAttributeValue<byte>(ncid, varid, attribute.Name, attribute.Value, NetCDF.nc_put_att_ubyte);
                break;
            case NcType.NC_CHAR:
                SetAttributeValue<char>(ncid, varid, attribute.Name, attribute.Value, SetCharAttributeValue);
                break;
            case NcType.NC_STRING:
                SetAttributeValue<string>(ncid, varid, attribute.Name, attribute.Value, NetCDF.nc_put_att_string);
                break;
            default:
                throw new InvalidOperationException($"Unknown attribute type: {attribute.DataType.Name}");
        }
    }

    /// <summary>
    /// Get the number of file-level attributes.
    /// </summary>
    /// <param name="ncid">NetCDF file ID.</param>
    public static int GetNumAttributes(int ncid)
    {
        Log.Debug("Calling nc_inq_natts() for file {0}...", ncid);

        int res = NetCDFNative.nc_inq_natts(ncid, out int natts);
        CheckResult(res, "Failed to get number of file-level attributes");

        Log.Debug("Call to nc_inq_natts() was successful; file {0} has {1} global attributes", ncid, natts);
        return natts;
    }

    public static int CreateVariable(int ncid, string name, Type type, IEnumerable<string> dimensions)
    {
        NcType nctype = type.ToNcType();
        int[] dimids = dimensions.Select(d => GetDimensionID(ncid, d)).ToArray();

        Log.Debug("nc_def_var(): Creating variable {0} with type {1} and dimensions {2}", name, type.Name, string.Join(", ", dimensions));

        int res = NetCDF.nc_def_var(ncid, name, nctype, dimids, out int varid);
        CheckResult(res, "Failed to create variable {0} with type {1} and dimensions {2}", name, type.Name, string.Join(", ", dimensions));

        Log.Debug("Successfully created variable {0} with type {1} and dimensions {2}", name, type.Name, string.Join(", ", dimensions));

        return varid;
    }

    /// <summary>
    /// Enable zlib compression for the specified variable. This should only be
    /// called after creating the variable but before closing the file.
    /// </summary>
    public static void EnableCompression(int ncid, int varid, ZLibOptions zlib, string? name = null)
    {
        int shuf = zlib.Shuffle ? 1 : 0;
        int defl = zlib.DeflateLevel > 0 ? 1 : 0;

        if (zlib.DeflateLevel == 0)
            Log.Warning("Enabling zlib compression with deflation level 0. Are you sure you want to do this?");

        Log.Debug("Enabling zlib compression for variable {0}: shuffle = {1}, level = {2}", name ?? varid.ToString(), zlib.Shuffle, zlib.DeflateLevel);

        int res = NetCDF.nc_def_var_deflate(ncid, varid, shuf, defl, zlib.DeflateLevel);
        CheckResult(res, "Failed to enable zlib compression for variable {0}", name ?? varid.ToString());

        Log.Debug("Successfully enabled zlib compression for variable {0}", name ?? varid.ToString());
    }

    public static string StrError(int errorCode)
    {
        IntPtr ptr = NetCDFNative.nc_strerror(errorCode);
        return ReadString(ptr)!;
    }

    /// <summary>
    /// Read a string from a pointer to a null-terminated sequence of utf-8
    /// bytes.
    /// </summary>
    unsafe private static string? ReadString(IntPtr p)
    {
        if (p == IntPtr.Zero)
            return null;

        byte* b = (byte*)p;
        byte* z = b;
        while (*z != (byte)0)
            z += 1;

        int count = (int)(z - b);
        if (count == 0)
            return string.Empty;

        var chars = new char[utfDecoder.GetCharCount(b, count, true)];
        fixed (char* c = chars)
            utfDecoder.GetChars(b, count, c, chars.Length, true);
        return new string(chars);
    }

    private static void CheckResult(int result, string format, params object[] args)
    {
        if (result != 0)
        {
            string context = string.Format(format, args);
            string error = StrError(result);
            throw new Exception($"{context}: {error}");
        }
    }
}
