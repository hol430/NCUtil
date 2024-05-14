using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using NCUtil.Core.Extensions;
using NCUtil.Core.Logging;
using NCUtil.Core.Models;
using NetCDFInterop;
using Attribute = NCUtil.Core.Models.Attribute;

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
