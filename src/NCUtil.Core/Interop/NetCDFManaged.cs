using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using NCUtil.Core.Extensions;
using NCUtil.Core.Logging;
using NCUtil.Core.Models;
using Attribute = NCUtil.Core.Models.Attribute;
using Range = NCUtil.Core.Models.Range;

namespace NCUtil.Core.Interop;

internal static class NetCDFManaged
{
    private const int nameBufferSize = 256;
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
        else if (mode == NetCDFFileMode.Write)
            return CreateNetCDF(file);

        int result = NetCDFNative.nc_open(file, mode.ToOpenMode(), out int id);
        CheckResult(result, "Failed to open netcdf file");

        Log.Diagnostic($"Successfully opened netcdf file in mode {0}: '{1}'", mode, file);
        return id;
    }

    public static int CreateNetCDF(string file)
    {
        Log.Debug("Creating NetCDF file: '{0}'...", file);

        int res = NetCDFNative.nc_create(file, CreateMode.NC_NETCDF4 | CreateMode.NC_CLOBBER, out int id);
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

        int result = NetCDFNative.nc_close(ncid);
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

        int res = NetCDFNative.nc_inq_dimlen(ncid, dimid, out nint length);
        CheckResult(res, "Failed to get length of dimension {0}: {1}", dimid);

        Log.Debug("Call to nc_inq_dimlen() was successful for dimension {0} and returned {1}", dimid, (int)length);
        return (int)length;
    }

    public static string GetDimensionName(int ncid, int dimid)
    {
        Log.Debug("Calling nc_inq_dimname() for dimension {0}", dimid);

        StringBuilder buffer = new StringBuilder(nameBufferSize + 1);
        int res = NetCDFNative.nc_inq_dimname(ncid, dimid, buffer);
        CheckResult(res, "Failed to get name of dimension with ID {0}", dimid);

        string name = buffer.ToString();
        Log.Debug("Call to nc_inq_dimname() was successful for dimension {0}: {1}", dimid, name);
        return name;
    }

    public static int GetDimensionID(int ncid, string name)
    {
        Log.Debug("Calling nc_inq_dimid() for dimension {0}", name);

        int res = NetCDFNative.nc_inq_dimid(ncid, name, out int dimid);
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

        int res = NetCDFNative.nc_inq_vardimid(ncid, varid, dimids);
        CheckResult(res, "Failed to get dimension IDs for variable {0}", varid);

        Log.Debug("Call to nc_inq_vardimid() was successful");
        return dimids;
    }

    public static void CreateDimension(int ncid, string name, int length)
    {
        if (length < 0)
            throw new InvalidOperationException($"Attempted to create dimension with negative length: {length}");

        if (length == 0)
            length = NCConst.NC_UNLIMITED;

        Log.Debug("Calling nc_def_dim() to create dimension {0} with length {1}", name, length);

        int res = NetCDFNative.nc_def_dim(ncid, name, (nint)length, out _);
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

        int res = NetCDFNative.nc_inq_varndims(ncid, varid, out int ndim);
        CheckResult(res, "nc_inq_varndims()");

        Log.Debug("nc_inq_varndims(): variable {0} has {1} dimensions", varid, ndim);
        return ndim;
    }

    public static void GetVariable(int ncid, int varid, out string name, out NCType type, out int[] dimids, out int nattr)
    {
        Log.Debug("Calling nc_inq_varname()");

        int ndims = GetVariableNumDimensions(ncid, varid);
        dimids = new int[ndims];

        StringBuilder namebuf = new StringBuilder(nameBufferSize);

        int res = NetCDFNative.nc_inq_var(ncid, varid, namebuf, out type, out int ndim2, dimids, out nattr);
        CheckResult(res, "nc_inq_var(), varid = {0}", varid);

        name = namebuf.ToString();

        if (ndims != ndim2)
            throw new Exception($"Number of dimensions for variable {name} has changed from {ndims} to {ndim2}");

        Log.Debug("Call to nc_inq_var() was successful");
    }

    public static NCType GetVariableType(int ncid, int varid)
    {
        GetVariable(ncid, varid, out _, out NCType type, out _, out _);
        return type;
    }

    internal static void WriteVaraShort(int ncid, int varid, IRange[] hyperslab, Array data)
    {
        // NCType.NC_SHORT
        nint[] start = hyperslab.Select(h => (nint)h.Start).ToArray();
        nint[] count = hyperslab.Select(h => (nint)h.Count).ToArray();

        long n = hyperslab.Product(r => r.Count);
        Log.Debug("Writing {0} elements to variable {1}", n, varid);

        int res = NetCDFNative.nc_put_vara_short(ncid, varid, start, count, (short[])data);
        CheckResult(res, "Failed to write to variable {0}", varid);

        Log.Debug("Successfully wrote {0} elements to variable {1}", n, varid);
    }

    internal static void WriteVaraInt(int ncid, int varid, IRange[] hyperslab, Array data)
    {
        // NCType.NC_INT
        nint[] start = hyperslab.Select(h => (nint)h.Start).ToArray();
        nint[] count = hyperslab.Select(h => (nint)h.Count).ToArray();

        long n = hyperslab.Product(r => r.Count);
        Log.Debug("Writing {0} elements to variable {1}", n, varid);

        int res = NetCDFNative.nc_put_vara_int(ncid, varid, start, count, (int[])data);
        CheckResult(res, "Failed to write to variable {0}", varid);

        Log.Debug("Successfully wrote {0} elements to variable {1}", n, varid);
    }

    internal static void WriteVaraInt64(int ncid, int varid, IRange[] hyperslab, Array data)
    {
        // NCType.NC_INT64
        nint[] start = hyperslab.Select(h => (nint)h.Start).ToArray();
        nint[] count = hyperslab.Select(h => (nint)h.Count).ToArray();

        long n = hyperslab.Product(r => r.Count);
        Log.Debug("Writing {0} elements to variable {1}", n, varid);

        int res = NetCDFNative.nc_put_vara_longlong(ncid, varid, start, count, (long[])data);
        CheckResult(res, "Failed to write to variable {0}", varid);

        Log.Debug("Successfully wrote {0} elements to variable {1}", n, varid);
    }

    internal static void WriteVaraUshort(int ncid, int varid, IRange[] hyperslab, Array data)
    {
        // NCType.NC_USHORT
        nint[] start = hyperslab.Select(h => (nint)h.Start).ToArray();
        nint[] count = hyperslab.Select(h => (nint)h.Count).ToArray();

        long n = hyperslab.Product(r => r.Count);
        Log.Debug("Writing {0} elements to variable {1}", n, varid);

        int res = NetCDFNative.nc_put_vara_ushort(ncid, varid, start, count, (ushort[])data);
        CheckResult(res, "Failed to write to variable {0}", varid);

        Log.Debug("Successfully wrote {0} elements to variable {1}", n, varid);
    }

    internal static void WriteVaraUint(int ncid, int varid, IRange[] hyperslab, Array data)
    {
        // NCType.NC_UINT
        nint[] start = hyperslab.Select(h => (nint)h.Start).ToArray();
        nint[] count = hyperslab.Select(h => (nint)h.Count).ToArray();

        long n = hyperslab.Product(r => r.Count);
        Log.Debug("Writing {0} elements to variable {1}", n, varid);

        int res = NetCDFNative.nc_put_vara_uint(ncid, varid, start, count, (uint[])data);
        CheckResult(res, "Failed to write to variable {0}", varid);

        Log.Debug("Successfully wrote {0} elements to variable {1}", n, varid);
    }

    internal static void WriteVaraUint64(int ncid, int varid, IRange[] hyperslab, Array data)
    {
        // NCType.NC_UINT64
        nint[] start = hyperslab.Select(h => (nint)h.Start).ToArray();
        nint[] count = hyperslab.Select(h => (nint)h.Count).ToArray();

        long n = hyperslab.Product(r => r.Count);
        Log.Debug("Writing {0} elements to variable {1}", n, varid);

        int res = NetCDFNative.nc_put_vara_ulonglong(ncid, varid, start, count, (ulong[])data);
        CheckResult(res, "Failed to write to variable {0}", varid);

        Log.Debug("Successfully wrote {0} elements to variable {1}", n, varid);
    }

    internal static void WriteVaraFloat(int ncid, int varid, IRange[] hyperslab, Array data)
    {
        // NCType.NC_FLOAT
        nint[] start = hyperslab.Select(h => (nint)h.Start).ToArray();
        nint[] count = hyperslab.Select(h => (nint)h.Count).ToArray();

        long n = hyperslab.Product(r => r.Count);
        Log.Debug("Writing {0} elements to variable {1}", n, varid);

        int res = NetCDFNative.nc_put_vara_float(ncid, varid, start, count, (float[])data);
        CheckResult(res, "Failed to write to variable {0}", varid);

        Log.Debug("Successfully wrote {0} elements to variable {1}", n, varid);
    }

    internal static void WriteVaraDouble(int ncid, int varid, IRange[] hyperslab, Array data)
    {
        // NCType.NC_DOUBLE
        nint[] start = hyperslab.Select(h => (nint)h.Start).ToArray();
        nint[] count = hyperslab.Select(h => (nint)h.Count).ToArray();

        long n = hyperslab.Product(r => r.Count);
        Log.Debug("Writing {0} elements to variable {1}", n, varid);

        int res = NetCDFNative.nc_put_vara_double(ncid, varid, start, count, (double[])data);
        CheckResult(res, "Failed to write to variable {0}", varid);

        Log.Debug("Successfully wrote {0} elements to variable {1}", n, varid);
    }

    internal static void WriteVaraByte(int ncid, int varid, IRange[] hyperslab, Array data)
    {
        // NCType.NC_BYTE
        nint[] start = hyperslab.Select(h => (nint)h.Start).ToArray();
        nint[] count = hyperslab.Select(h => (nint)h.Count).ToArray();

        long n = hyperslab.Product(r => r.Count);
        Log.Debug("Writing {0} elements to variable {1}", n, varid);

        int res = NetCDFNative.nc_put_vara_text(ncid, varid, start, count, (byte[])data);
        CheckResult(res, "Failed to write to variable {0}", varid);

        Log.Debug("Successfully wrote {0} elements to variable {1}", n, varid);
    }

    internal static void WriteVaraUbyte(int ncid, int varid, IRange[] hyperslab, Array data)
    {
        // NCType.NC_UBYTE
        nint[] start = hyperslab.Select(h => (nint)h.Start).ToArray();
        nint[] count = hyperslab.Select(h => (nint)h.Count).ToArray();

        long n = hyperslab.Product(r => r.Count);
        Log.Debug("Writing {0} elements to variable {1}", n, varid);

        int res = NetCDFNative.nc_put_vara_ubyte(ncid, varid, start, count, (byte[])data);
        CheckResult(res, "Failed to write to variable {0}", varid);

        Log.Debug("Successfully wrote {0} elements to variable {1}", n, varid);
    }

    internal static void WriteVaraChar(int ncid, int varid, IRange[] hyperslab, Array data)
    {
        // NCType.NC_CHAR
        nint[] start = hyperslab.Select(h => (nint)h.Start).ToArray();
        nint[] count = hyperslab.Select(h => (nint)h.Count).ToArray();

        long n = hyperslab.Product(r => r.Count);
        Log.Debug("Writing {0} elements to variable {1}", n, varid);

        int res = NetCDFNative.nc_put_vara_text(ncid, varid, start, count, (byte[])data);
        CheckResult(res, "Failed to write to variable {0}", varid);

        Log.Debug("Successfully wrote {0} elements to variable {1}", n, varid);
    }

    internal static void WriteVaraString(int ncid, int varid, IRange[] hyperslab, Array data)
    {
        // NCType.NC_STRING
        nint[] start = hyperslab.Select(h => (nint)h.Start).ToArray();
        nint[] count = hyperslab.Select(h => (nint)h.Count).ToArray();

        long n = hyperslab.Product(r => r.Count);
        Log.Debug("Writing {0} elements to variable {1}", n, varid);

        int res = NetCDFNative.nc_put_vara_string(ncid, varid, start, count, (string[])data);
        CheckResult(res, "Failed to write to variable {0}", varid);

        Log.Debug("Successfully wrote {0} elements to variable {1}", n, varid);
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

    public static unsafe void GetChunkSizes(int ncid, int varid, out ChunkMode mode, out int[] chunks)
    {
        Log.Debug("Checking chunk sizes for variable {0}", varid);

        int ndim = GetVariableNumDimensions(ncid, varid);
        nint* ptr = stackalloc nint[ndim];

        int res = NetCDFNative.nc_inq_var_chunking(ncid, varid, out int storagep, ptr);
        CheckResult(res, "Failed to read chunk sizes for variable {0}", varid);

        chunks = new int[ndim];
        for (int i = 0; i < ndim; i++)
            chunks[i] = (int)ptr[i];

        mode = (ChunkMode)storagep;
        Log.Debug("Variable {0} has chunk sizes: {1}", varid, string.Join(", ", chunks));
    }

    public static string GetAttributeName(int ncid, int varid, int number)
    {
        StringBuilder namebuf = new StringBuilder(nameBufferSize);

        Log.Debug("Calling nc_inq_attname()");

        int res = NetCDFNative.nc_inq_attname(ncid, varid, number, namebuf);
        CheckResult(res, "nc_inq_attname");

        string name = namebuf.ToString();
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

    private static string HandleNullableString(string? ns)
    {
        return ns ?? string.Empty;
    }

    public static int nc_get_att_string(int ncid, int varid, string name, string[] ip)
    {
        IntPtr[] parr = new IntPtr[ip.Length];

        int res = NetCDFNative.nc_get_att_string(ncid, varid, name, parr);
        CheckResult(res, "Failed to read string attribute {0} of variable {1}", name, varid);

        for (int i = 0; i < ip.Length; i++)
            ip[i] = HandleNullableString(ReadString(parr[i]));

        return NetCDFNative.nc_free_string(new IntPtr(ip.Length), parr);
    }

    public static object GetAttributeValue(int ncid, int varid, string name, NCType type, int length)
    {
        switch (type)
        {
            case NCType.NC_SHORT:
                return GetAttributeValue<short>(ncid, varid, name, length, NetCDFNative.nc_get_att_short);
            case NCType.NC_INT:
                return GetAttributeValue<int>(ncid, varid, name, length, NetCDFNative.nc_get_att_int);
            case NCType.NC_INT64:
                return GetAttributeValue<long>(ncid, varid, name, length, NetCDFNative.nc_get_att_longlong);

            case NCType.NC_USHORT:
                return GetAttributeValue<ushort>(ncid, varid, name, length, NetCDFNative.nc_get_att_ushort);
            case NCType.NC_UINT:
                return GetAttributeValue<uint>(ncid, varid, name, length, NetCDFNative.nc_get_att_uint);
            case NCType.NC_UINT64:
                return GetAttributeValue<ulong>(ncid, varid, name, length, NetCDFNative.nc_get_att_ulonglong);

            case NCType.NC_FLOAT:
                return GetAttributeValue<float>(ncid, varid, name, length, NetCDFNative.nc_get_att_float);
            case NCType.NC_DOUBLE:
                return GetAttributeValue<double>(ncid, varid, name, length, NetCDFNative.nc_get_att_double);

            case NCType.NC_BYTE:
                return GetAttributeValue<sbyte>(ncid, varid, name, length, NetCDFNative.nc_get_att_schar);
            case NCType.NC_UBYTE:
                return GetAttributeValue<byte>(ncid, varid, name, length, NetCDFNative.nc_get_att_uchar);
            case NCType.NC_CHAR:
                return ReadCharAttributeValue(ncid, varid, name, length);
            case NCType.NC_STRING:
                return GetAttributeValue<string>(ncid, varid, name, length, nc_get_att_string);
            default:
                throw new InvalidOperationException($"Unknown attribute type: {type}");
        }
    }

    public static int nc_get_att_text(int ncid, int varid, string name, out string value, int maxLength)
    {
        // In case netcdf adds terminating zero.
        byte[] buffer = new byte[maxLength + 2];

        int res = NetCDFNative.nc_get_att_text(ncid, varid, name, buffer, maxLength);
        // TODO: error checking
        char[] chars = new char[utfDecoder.GetCharCount(buffer, 0, maxLength)];
        utfDecoder.GetChars(buffer, 0, maxLength, chars, 0);
        value = new string(chars);
        return res;
    }

    private static string ReadCharAttributeValue(int ncid, int varid, string name, int length)
    {
        Log.Debug("Reading value of char attribute {0}", name);

        int res = nc_get_att_text(ncid, varid, name, out string value, length);
        CheckResult(res, "Failed to read char attribute: {0}", name);

        Log.Debug("Successfully read value of char attribute {0}", name);
        return value;
    }

    public static Attribute GetAttribute(int ncid, int varid, int number)
    {
        string name = GetAttributeName(ncid, varid, number);

        Log.Debug("Calling nc_inq_att()");
        int res = NetCDFNative.nc_inq_att(ncid, varid, name, out NCType NCType, out nint plength);
        CheckResult(res, "nc_inq_att()");

        int length = (int)plength;
        Type type = NCType.ToType();
        Log.Debug("Attribute {0} has type {1} and length {2}", name, type.Name, length);

        object value = GetAttributeValue(ncid, varid, name, NCType, length);

        return new Attribute(name, value, type);
    }

    private static void SetAttributeValue<T>(int ncid, int varid, string name, NCType type, object value, Func<int, int, string, NCType, nint, T[], int> nativeFunc)
    {
        Log.Debug("Calling nc_put_att_{0}()", typeof(T).Name);

        T[] array;
        if (typeof(T).IsAssignableFrom(value.GetType()))
            array = [(T)value];
        else if (value.GetType().IsArray)
            array = (T[])value;
        else if (value is IEnumerable<T>)
            array = ((IEnumerable<T>)value).ToArray();
        else
            throw new InvalidOperationException($"Attempted to set attribute {name} as {typeof(T).Name} attribute, but value is of type {value.GetType().Name}");

        int res = nativeFunc(ncid, varid, name, type, array.Length, array);
        CheckResult(res, "Failed to set attribute {0}", name);

        Log.Debug("Successfully set attribute {0}", name);
    }

    private static void SetChunking(int ncid, int varid, ChunkMode mode, int[]? chunkSizes)
    {
        nint[] ptrs = chunkSizes?.Select(c => (nint)c).ToArray() ?? Array.Empty<nint>();
        Log.Debug("Calling nc_def_var_chunking() for variable {0}", varid);

        int res = NetCDFNative.nc_def_var_chunking(ncid, varid, (int)mode, ptrs);
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

    private static int SetCharAttributeValue(int ncid, int varid, string name, NCType type, nint x, char[] value)
    {
        return NetCDFNative.nc_put_att_text(ncid, varid, name, value.Length, new string(value));
    }

    // TODO: replace with custom marshaler
    private static int SetStringAttributeValue(int ncid, int varid, string name, NCType type, nint x, string[] value)
    {
        return nc_put_att_string(ncid, varid, name, value);
    }

    // TODO: replace with custom marshaler
    unsafe public static int nc_put_att_string(int ncid, int varid, string name, string[] tp)
    {
        IntPtr[] bb = new IntPtr[tp.Length];
        (byte[] buffer, uint[] offsets) = WriteStrings(tp);
        fixed (byte* buf = buffer)
        {
            for (int i = 0; i < tp.Length; i++)
            {
                if (uint.MaxValue == offsets[i])
                    bb[i] = IntPtr.Zero;
                else
                    bb[i] = new IntPtr(buf + offsets[i]);
            }
            return NetCDFNative.nc_put_att_string(ncid, varid, name, new IntPtr(bb.Length), bb);
        }
    }

    public static void SetAttribute(int ncid, int varid, Attribute attribute)
    {
        NCType nctype = attribute.DataType.ToNCType();
        switch (attribute.DataType.ToNCType())
        {
            case NCType.NC_SHORT:
                SetAttributeValue<short>(ncid, varid, attribute.Name, nctype, attribute.Value, NetCDFNative.nc_put_att_short);
                break;
            case NCType.NC_INT:
                SetAttributeValue<int>(ncid, varid, attribute.Name, nctype, attribute.Value, NetCDFNative.nc_put_att_int);
                break;
            case NCType.NC_INT64:
                SetAttributeValue<long>(ncid, varid, attribute.Name, nctype, attribute.Value, NetCDFNative.nc_put_att_longlong);
                break;

            case NCType.NC_USHORT:
                SetAttributeValue<ushort>(ncid, varid, attribute.Name, nctype, attribute.Value, NetCDFNative.nc_put_att_ushort);
                break;
            case NCType.NC_UINT:
                SetAttributeValue<uint>(ncid, varid, attribute.Name, nctype, attribute.Value, NetCDFNative.nc_put_att_uint);
                break;
            case NCType.NC_UINT64:
                SetAttributeValue<ulong>(ncid, varid, attribute.Name, nctype, attribute.Value, NetCDFNative.nc_put_att_ulonglong);
                break;

            case NCType.NC_FLOAT:
                SetAttributeValue<float>(ncid, varid, attribute.Name, nctype, attribute.Value, NetCDFNative.nc_put_att_float);
                break;
            case NCType.NC_DOUBLE:
                SetAttributeValue<double>(ncid, varid, attribute.Name, nctype, attribute.Value, NetCDFNative.nc_put_att_double);
                break;

            case NCType.NC_BYTE:
                SetAttributeValue<sbyte>(ncid, varid, attribute.Name, nctype, attribute.Value, NetCDFNative.nc_put_att_schar);
                break;
            case NCType.NC_UBYTE:
                SetAttributeValue<byte>(ncid, varid, attribute.Name, nctype, attribute.Value, NetCDFNative.nc_put_att_ubyte);
                break;
            case NCType.NC_CHAR:
                SetAttributeValue<char>(ncid, varid, attribute.Name, nctype, attribute.Value, SetCharAttributeValue);
                break;
            case NCType.NC_STRING:
                SetAttributeValue<string>(ncid, varid, attribute.Name, nctype, attribute.Value, SetStringAttributeValue);
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
        NCType nctype = type.ToNCType();
        int[] dimids = dimensions.Select(d => GetDimensionID(ncid, d)).ToArray();

        Log.Debug("nc_def_var(): Creating variable {0} with type {1} and dimensions {2}", name, type.Name, string.Join(", ", dimensions));

        int res = NetCDFNative.nc_def_var(ncid, name, nctype, dimids.Length, dimids, out int varid);
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

        int res = NetCDFNative.nc_def_var_deflate(ncid, varid, shuf, defl, zlib.DeflateLevel);
        CheckResult(res, "Failed to enable zlib compression for variable {0}", name ?? varid.ToString());

        Log.Debug("Successfully enabled zlib compression for variable {0}", name ?? varid.ToString());
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

    public static int nc_get_vara_string(int ncid, int varid, IntPtr[] start, IntPtr[] count, string[] data)
    {
        IntPtr[] parr = new IntPtr[data.Length];

        int res = NetCDFNative.nc_get_vara_string(ncid, varid, start, count, parr);
        if (res != 0)
            return res;

        for (int i = 0; i < data.Length; i++)
            data[i] = ReadString(parr[i]) ?? string.Empty;

        return NetCDFNative.nc_free_string(new IntPtr(data.Length), parr);
    }

    /// <summary>
    /// Writes strings to a buffer as zero-terminated UTF8-encoded strings.
    /// </summary>
    /// <param name="data">An array of strings to write to a buffer.</param>
    /// <returns>A pair of a buffer with zero-terminated UTF8-encoded strings and an array of offsets to the buffer.
    /// An offset of uint.MaxValue represents null in the data.
    /// </returns>
    unsafe private static (byte[], uint[]) WriteStrings(string[] data)
    {
        // Total length of the buffer.
        uint buflen = 0;

        // Length of each buffer.
        int[] bytecounts = new int[data.Length];

        // Compute buffer offsets.
        for (int i = 0; i < data.Length; i++)
        {
            fixed (char* p = data[i])
                bytecounts[i] = utfEncoder.GetByteCount(p, data[i].Length, true);

            // Guard against overflow.
            if (bytecounts[i] > uint.MaxValue - buflen - 1)
                throw new InternalBufferOverflowException("string buffer cannot exceed 4Gbyte in a single NetCDF operation");

            // Extra byte for the null terminator.
            buflen += (uint)bytecounts[i] + 1;
        }

        // Buffer containing the utf8-encoded strings separated by null terminators.
        byte[] buf = new byte[buflen];

        // The offset of each of the strings within the buffer.
        uint[] offsets = new uint[data.Length];

        // Allocate the buffer and write bytes
        fixed (byte* pbuf = buf)
        {
            int charsUsed;
            int bytesUsed;
            bool isCompleted;
            uint offset = 0;
            for (int i = 0; i < data.Length; i++)
            {
                offsets[i] = offset;
                int bc = bytecounts[i];
                fixed (char* p = data[i])
                    utfEncoder.Convert(p, data[i].Length, pbuf + offset, bc, true, out charsUsed, out bytesUsed, out isCompleted);
                System.Diagnostics.Debug.Assert(charsUsed == data[i].Length && bytesUsed == bc && isCompleted);
                offset += (uint)bc;
                *(pbuf + offset) = (byte)0;
                offset += 1;
            }
        }
        return (buf, offsets);
    }

    // public static unsafe int nc_put_vara_string(int ncid, int varid, IntPtr[] start, IntPtr[] count, string[] dp)
    // {
    //     int r;
    //     var len = dp.Length;
    //     IntPtr[] bb = new IntPtr[len];
    //     (byte[] buffer, uint[] offsets) = WriteStrings(dp);
    //     fixed (byte* buf = buffer)
    //     {
    //         for (int i = 0; i < len; i++)
    //         {
    //             if (offsets[i] == uint.MaxValue)
    //                 bb[i] = IntPtr.Zero;
    //             else
    //                 bb[i] = new IntPtr(buf + offsets[i]);
    //         }
    //         r = NetCDFNative.nc_put_vara_string(ncid, varid, start, count, bb);
    //     }
    //     return r;
    // }

    public static void CheckResult(int result, string format, params object[] args)
    {
        if (result != 0)
        {
            string context = string.Format(format, args);
            string error = NetCDFNative.nc_strerror(result);
            throw new Exception($"{context}: {error}");
        }
    }
}
