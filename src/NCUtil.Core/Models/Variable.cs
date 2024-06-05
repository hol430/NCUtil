using NCUtil.Core.Extensions;
using NCUtil.Core.Interop;
using NCUtil.Core.Logging;

using static NCUtil.Core.Interop.NetCDFManaged;

namespace NCUtil.Core.Models;

public class Variable
{
    private readonly int ncid;
    private readonly int varid;
    private readonly NCType nctype;
    private readonly List<Attribute> attributes;
    public string Name { get; private init; }
    public IReadOnlyList<string> Dimensions { get; private init; }
    public Type DataType { get; private init; }
    public IReadOnlyList<Attribute> Attributes => attributes;
    public ICompressionAlgorithm? Compression { get; private init; }
    public IReadOnlyList<int>? ChunkSizes { get; private init; }
    public PackType Chunking { get; private init; }

    /// <summary>
    /// Create a managed variable object for a variable which already exists.
    /// </summary>
    /// <param name="ncid">ID of the NetCDF file.</param>
    /// <param name="varid">ID of the variable.</param>
    internal Variable(int ncid, int varid)
    {
        this.ncid = ncid;
        this.varid = varid;

        Log.Debug("Calling nc_inq_varname()");

        int ndims = GetNumDimensions();
        int[] dimids = new int[ndims];

        int res = NetCDFNative.nc_inq_var(ncid, varid, out string? name, out nctype, out int ndim2, dimids, out int natt);
        CheckResult(res, "nc_inq_var(), varid = {0}", varid);

        if (ndims != ndim2)
            throw new Exception($"Number of dimensions for variable {name} has changed from {ndims} to {ndim2}");

        Name = name!;
        Dimensions = dimids.Select(d => Dimension.GetName(ncid, d)).ToList();
        DataType = nctype.ToType();

        attributes = new List<Attribute>();
        for (int i = 0; i < natt; i++)
            attributes.Add(new Attribute(ncid, varid, i));

        Compression = GetCompressionOptions();
        GetChunkSizes(out PackType packing, out int[] sizes);
        Chunking = packing;
        ChunkSizes = sizes;

        Log.Debug("Call to nc_inq_var() was successful");
    }

    /// <summary>
    /// Create a new variable in a NetCDF file.
    /// </summary>
    /// <param name="ncid">ID of the NetCDF file.</param>
    /// <param name="name">Name of the variable to be created.</param>
    /// <param name="dimensions">Names of the dimensions of the variable to be created (in desired order).</param>
    /// <param name="type">Type of the variable to be created.</param>
    /// <param name="packing">Packing mode of the variable to be created.</param>
    /// <param name="chunkSizes">(Optional) chunk sizes of the variable to be created. Only required if the variable is to be chunked (ie packing == PackType.Chunked).</param>
    /// <param name="compression">(Optional) compression algorithm to be used. Null means no compression.</param>
    public Variable(int ncid, string name, IEnumerable<string> dimensions, NCType type, PackType packing, IEnumerable<int>? chunkSizes = null, ICompressionAlgorithm? compression = null)
    {
        this.ncid = ncid;
        Name = name;
        Dimensions = dimensions.ToList();
        nctype = type;
        DataType = type.ToType();
        Compression = compression;
        ChunkSizes = chunkSizes?.ToList();
        Chunking = packing;

        // Create the variable.
        varid = Create();

        // Define the packing (and chunking, if enabled).
        SetChunking(packing, chunkSizes);

        // Compression.
        if (compression is ZLibCompression zlib)
            EnableZLibCompression(zlib);
        else if (compression != null)
            throw new NotImplementedException($"TBI: unsupported compression type: {compression.GetType().Name}");

        attributes = new List<Attribute>();
    }

    /// <summary>
    /// Read the specified hyperslab from a variable and return the result as a
    /// multi-dimensional array matching the shape of the variable.
    /// </summary>
    /// <param name="hyperslab">The hyperslab to read.</param>
    public Array Read(params IRange[] hyperslab)
    {
        Array array = Array.CreateInstance(DataType, hyperslab.Product(r => r.Count));
        Read(array, hyperslab);
        return array;
    }

    /// <summary>
    /// Read the specified hyperslab from a variable and return the result as a
    /// multi-dimensional array matching the shape of the variable.
    /// </summary>
    /// <param name="hyperslab">The hyperslab to read.</param>
    public void Read(Array array, params IRange[] hyperslab)
    {
        Read1D(hyperslab, array);
        // int[] shape = hyperslab.Select(h => h.Count).ToArray();
        // array = array.ToMultiDimensionalArray(shape);
    }

    /// <summary>
    /// Call the appropriate nc_get_vara_X() function to read the specified
    /// hyperslab from the specified variable, and return the result as a
    /// 1-dimensional array with the last dimension varying most rapidly, and
    /// the first dimension varying most slowly.
    /// </summary>
    /// <param name="ncid">ID of the NetCDF file.</param>
    /// <param name="varid">ID of the variable to be read.</param>
    /// <param name="hyperslab">The hyperslab to read.</param>
    /// <param name="data">The output data array (in/out parameter). This must be initialised by the caller. This must be of the correct type and length.</param>
    private void Read1D(IRange[] hyperslab, Array data)
    {
        switch (nctype)
        {
            case NCType.NC_SHORT:
                ReadVara(hyperslab, (short[])data, NetCDFNative.nc_get_vara_short);
                break;
            case NCType.NC_INT:
                ReadVara(hyperslab, (int[])data, NetCDFNative.nc_get_vara_int);
                break;
            case NCType.NC_INT64:
                ReadVara(hyperslab, (long[])data, NetCDFNative.nc_get_vara_longlong);
                break;

            case NCType.NC_USHORT:
                ReadVara(hyperslab, (ushort[])data, NetCDFNative.nc_get_vara_ushort);
                break;
            case NCType.NC_UINT:
                ReadVara(hyperslab, (uint[])data, NetCDFNative.nc_get_vara_uint);
                break;
            case NCType.NC_UINT64:
                ReadVara(hyperslab, (ulong[])data, NetCDFNative.nc_get_vara_ulonglong);
                break;

            case NCType.NC_FLOAT:
                ReadVara(hyperslab, (float[])data, NetCDFNative.nc_get_vara_float);
                break;
            case NCType.NC_DOUBLE:
                ReadVara(hyperslab, (double[])data, NetCDFNative.nc_get_vara_double);
                break;

            case NCType.NC_BYTE:
                ReadVara(hyperslab, (sbyte[])data, NetCDFNative.nc_get_vara_schar);
                break;
            case NCType.NC_UBYTE:
                ReadVara(hyperslab, (byte[])data, NetCDFNative.nc_get_vara_ubyte);
                break;
            case NCType.NC_CHAR:
                ReadVara(hyperslab, (byte[])data, NetCDFNative.nc_get_vara_text);
                break;
            case NCType.NC_STRING:
                ReadVara(hyperslab, (string[])data, NetCDFNative.nc_get_vara_string);
                break;
            default:
                throw new NotImplementedException($"Unable to read from variable {Name}: unsupported type: {nctype}");
        }
    }

    private void ReadVara<T>(IRange[] hyperslab, T[] data, Func<int, int, nint[], nint[], T[], int> reader)
    {
        if (Dimensions.Count != hyperslab.Length)
            throw new InvalidOperationException($"Unable to read from variable {Name}: only {hyperslab.Length} dimensions were specified, but variable has {Dimensions.Count} dimensions");

        nint[] start = hyperslab.Select(h => (nint)h.Start).ToArray();
        nint[] count = hyperslab.Select(h => (nint)h.Count).ToArray();

        long n = hyperslab.Product(h => h.Count);

        if (data.Length < n)
            throw new InvalidOperationException($"Unable to read {n} elements into array of length {data.Length}");

        Log.Debug("Reading {0} elements from variable {1}", n, Name);

        int res = reader(ncid, varid, start, count, data);
        CheckResult(res, "Failed to read from variable {0}", Name);

        Log.Debug("Successfully read from variable {0}", Name);
    }

    private void WriteVara<T>(IRange[] hyperslab, T[] data, Func<int, int, nint[], nint[], T[], int> writer)
    {
        if (Dimensions.Count != hyperslab.Length)
            throw new InvalidOperationException($"Unable to write to variable {Name}: only {hyperslab.Length} dimensions were specified, but variable has {Dimensions.Count} dimensions");

        nint[] start = hyperslab.Select(h => (nint)h.Start).ToArray();
        nint[] count = hyperslab.Select(h => (nint)h.Count).ToArray();

        long n = hyperslab.Product(h => h.Count);

        // If array length is greater than the product of the hyperslab lengths,
        // only the first N elements will be written. We assume this is
        // intentional and do not error out if this happens. This assumption is
        // useful as an optimisation to avoid re-allocating an array on final
        // iterations when copying data.
        if (data.Length < n)
            throw new InvalidOperationException($"Unable to write {n} elements from array of length {data.Length}");

        Log.Debug("Writing {0} elements to variable {1}", n, Name);

        int res = writer(ncid, varid, start, count, data);
        CheckResult(res, "Failed to write to variable {0}", Name);

        Log.Debug("Successfully wrote to variable {0}", Name);
    }

    public void Write(Array data, params IRange[] hyperslab)
    {
        if (data.Rank > 1)
            data = data.ToFlatArray();

        switch (nctype)
        {
            case NCType.NC_SHORT:
                WriteVara(hyperslab, (short[])data, NetCDFNative.nc_put_vara_short);
                break;
            case NCType.NC_INT:
                WriteVara(hyperslab, (int[])data, NetCDFNative.nc_put_vara_int);
                break;
            case NCType.NC_INT64:
                // native longlong is equivalent to managed long
                WriteVara(hyperslab, (long[])data, NetCDFNative.nc_put_vara_longlong);
                break;

            case NCType.NC_USHORT:
                WriteVara(hyperslab, (ushort[])data, NetCDFNative.nc_put_vara_ushort);
                break;
            case NCType.NC_UINT:
                WriteVara(hyperslab, (uint[])data, NetCDFNative.nc_put_vara_uint);
                break;
            case NCType.NC_UINT64:
                WriteVara(hyperslab, (ulong[])data, NetCDFNative.nc_put_vara_ulonglong);
                break;

            case NCType.NC_FLOAT:
                WriteVara(hyperslab, (float[])data, NetCDFNative.nc_put_vara_float);
                break;
            case NCType.NC_DOUBLE:
                WriteVara(hyperslab, (double[])data, NetCDFNative.nc_put_vara_double);
                break;

            case NCType.NC_BYTE:
            case NCType.NC_UBYTE:
                WriteVara(hyperslab, (byte[])data, NetCDFNative.nc_put_vara_ubyte);
                break;
            case NCType.NC_CHAR:
                // WriteVara(hyperslab, (char[])data, NetCDFNative.nc_put_vara_char);
                // break;
            case NCType.NC_STRING:
                WriteVara(hyperslab, (string[])data, NetCDFNative.nc_put_vara_string);
                break;
            default:
                throw new NotImplementedException($"Unable to read from variable {Name}: unsupported type: {DataType}");
        }
    }

    /// <summary>
    /// Get the total length of this variable along all dimensions.
    /// </summary>
    public long GetLength()
    {
        int[] dimids = GetDimensionIDs();
        return dimids.Product(d => Dimension.GetLength(ncid, d));
    }

    public void CreateAttribute(string name, Type type, object value)
    {
        Attribute attribute = new Attribute(ncid, varid, name, value, type);
        attributes.Add(attribute);
    }

    public override string ToString()
    {
        string dims = string.Join(", ", Dimensions);
        return $"{DataType.ToFriendlyName()} {Name} ({dims})";
    }

    /// <summary>
    /// Create a variable and return the created variable's ID.
    /// </summary>
    private int Create()
    {
        NCType nctype = DataType.ToNCType();
        int[] dimids = Dimensions.Select(d => Dimension.GetID(ncid, d)).ToArray();

        Log.Debug("nc_def_var(): Creating variable {0} with type {1} and dimensions {2}", Name, DataType.ToFriendlyName(), string.Join(", ", Dimensions));

        int res = NetCDFNative.nc_def_var(ncid, Name, nctype, dimids.Length, dimids, out int varid);
        CheckResult(res, "Failed to create variable {0} with type {1} and dimensions {2}", Name, DataType.ToFriendlyName(), string.Join(", ", Dimensions));

        Log.Debug("Successfully created variable {0} with type {1} and dimensions {2}", Name, DataType.ToFriendlyName(), string.Join(", ", Dimensions));

        return varid;
    }

    /// <summary>
    /// Set the packing type and chunk sizes for this variable. This must be
    /// called after the variable is created but before the file is closed.
    /// </summary>
    /// <param name="mode">The packing mode.</param>
    /// </summary>
    private void SetChunking(PackType mode, IEnumerable<int>? chunkSizes)
    {
        if (chunkSizes == null && mode == PackType.Chunked)
            throw new InvalidOperationException($"Unable to set packing to chunked for variable {Name}: no chunk sizes are defined");

        if (chunkSizes != null && mode != PackType.Chunked)
            // Technically, this is not actually a fatal error, but it almost
            // certainly indicates a programming error.
            throw new InvalidOperationException($"Unable to set packing to {mode.ToEnumString()} for variable {Name}: chunk sizes were defined but packing is not set to chunked");

        nint[] ptrs = chunkSizes?.Select(c => (nint)c).ToArray() ?? Array.Empty<nint>();
        Log.Debug("Calling nc_def_var_chunking() for variable {0}", varid);

        int res = NetCDFNative.nc_def_var_chunking(ncid, varid, (int)mode, ptrs);
        CheckResult(res, "Failed to set chunk sizes for variable {0}", varid);

        Log.Debug("Successfully set chunk sizes for variable {0}", varid);
    }

    /// <summary>
    /// Enable zlib compression for the specified variable. This should only be
    /// called after creating the variable but before closing the file.
    /// </summary>
    /// <param name="options">Compression options.</param>
    private void EnableZLibCompression(ZLibCompression options)
    {
        int shuf = options.Shuffle ? 1 : 0;
        int defl = options.DeflateLevel > 0 ? 1 : 0;

        if (options.DeflateLevel < NCConst.NC_MIN_DEFLATE_LEVEL || options.DeflateLevel > NCConst.NC_MAX_DEFLATE_LEVEL)
            throw new InvalidOperationException($"Invalid deflation level: {options.DeflateLevel}. This must be in range [{NCConst.NC_MIN_DEFLATE_LEVEL}, {NCConst.NC_MAX_DEFLATE_LEVEL}]");

        if (options.DeflateLevel == 0)
            Log.Warning("Enabling zlib compression with deflation level 0. Are you sure you want to do this?");

        Log.Debug("Enabling zlib compression for variable {0}: shuffle = {1}, level = {2}", Name, options.Shuffle, options.DeflateLevel);

        int res = NetCDFNative.nc_def_var_deflate(ncid, varid, shuf, defl, options.DeflateLevel);
        CheckResult(res, "Failed to enable zlib compression for variable {0}", Name);

        Log.Debug("Successfully enabled zlib compression for variable {0}", Name);
    }

    /// <summary>
    /// Get the zlib configuration for the specified variable. If zlib
    /// compression is disabled, this will return a deflate level of 0.
    /// </summary>
    private ICompressionAlgorithm? GetCompressionOptions()
    {
        Log.Debug("Checking compression settings for variable {0}", varid);

        int res = NetCDFNative.nc_inq_var_deflate(ncid, varid, out int shuf, out int def, out int deflateLevel);
        CheckResult(res, "Failed to check zlib compression for variable {0}", varid);

        Log.Debug("Successfully read compression settings for variable {0}", varid);

        // def will be 1 if the deflate filter is turned on. Otherwise it will
        // be zero.
        if (def == 0)
            return null;

        return new ZLibCompression(shuf == 1, deflateLevel);
    }

    /// <summary>
    /// Get the packing mode and chunk sizes (if any) of this variable.
    /// </summary>
    private unsafe void GetChunkSizes(out PackType mode, out int[] chunks)
    {
        Log.Debug("Checking chunk sizes for variable {0}", varid);

        int ndim = GetNumDimensions();
        nint* ptr = stackalloc nint[ndim];

        int res = NetCDFNative.nc_inq_var_chunking(ncid, varid, out int storagep, ptr);
        CheckResult(res, "Failed to read chunk sizes for variable {0}", varid);

        chunks = new int[ndim];
        for (int i = 0; i < ndim; i++)
            chunks[i] = (int)ptr[i];

        mode = (PackType)storagep;
        Log.Debug("Variable {0} has chunk sizes: {1}", varid, string.Join(", ", chunks));
    }

    /// <summary>
    /// Get the IDs of the dimensions of this variable.
    /// </summary>
    private int[] GetDimensionIDs()
    {
        int ndim = GetNumDimensions();
        int[] dimids = new int[ndim];

        Log.Debug("Calling nc_inq_vardimid() for variable {0}", varid);

        int res = NetCDFNative.nc_inq_vardimid(ncid, varid, dimids);
        CheckResult(res, "Failed to get dimension IDs for variable {0}", varid);

        Log.Debug("Call to nc_inq_vardimid() was successful");
        return dimids;
    }

    /// <summary>
    /// Get the rank (number of dimensions) of this variable.
    /// </summary>
    private int GetNumDimensions()
    {
        Log.Debug("Calling nc_inq_varndims() for variable {0}", varid);

        int res = NetCDFNative.nc_inq_varndims(ncid, varid, out int ndim);
        CheckResult(res, "nc_inq_varndims()");

        Log.Debug("nc_inq_varndims(): variable {0} has {1} dimensions", varid, ndim);
        return ndim;
    }
}
