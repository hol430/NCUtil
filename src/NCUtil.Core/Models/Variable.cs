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
    public string Name { get; private init; }
    public IReadOnlyList<string> Dimensions { get; private init; }
    public Type DataType { get; private init; }
    public IReadOnlyList<Attribute> Attributes { get; private init; }
    public ZLibOptions Zlib { get; private init; }
    public IReadOnlyList<int> ChunkSizes { get; private init; }
    public ChunkMode Chunking { get; private init; }

    public Variable(int ncid, int varid, string name, IEnumerable<string> dimensions, NCType type, IEnumerable<Attribute> attributes, ZLibOptions zlib, ChunkMode chunking, IEnumerable<int> chunkSizes)
    {
        this.ncid = ncid;
        this.varid = varid;
        Name = name;
        Dimensions = dimensions.ToList();
        nctype = type;
        DataType = type.ToType();
        Attributes = attributes.ToList();
        Zlib = zlib;
        ChunkSizes = chunkSizes.ToList();
        Chunking = chunking;
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
                ReadVara(hyperslab, (string[])data, NetCDFManaged.nc_get_vara_string);
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

    public long GetLength()
    {
        int[] dimids = GetVariableDimensionIDs(ncid, varid);
        return dimids.Product(d => GetDimensionLength(ncid, d));
    }

    public override string ToString()
    {
        string dims = string.Join(", ", Dimensions);
        return $"{DataType.ToFriendlyName()} {Name} ({dims})";
    }
}
