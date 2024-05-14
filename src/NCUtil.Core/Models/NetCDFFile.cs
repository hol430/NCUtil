using System.Security.Cryptography;
using NCUtil.Core.Extensions;
using NCUtil.Core.Interop;
using NCUtil.Core.Logging;
using NetCDFInterop;

using static NCUtil.Core.Interop.NetCDFManaged;

namespace NCUtil.Core.Models;

public class NetCDFFile : IDisposable
{
    /// <summary>
    /// Name of the time dimension.
    /// </summary>
    public const string DimTime = "time";

    private readonly string path;
    private readonly int id;
    private readonly bool readOnly;

    public NetCDFFile(string path, NetCDFFileMode mode = NetCDFFileMode.Read)
    {
        if (mode == NetCDFFileMode.Write)
            id = CreateNetCDF(path);
        else
            id = OpenNetCDF(path, mode);
        readOnly = mode == NetCDFFileMode.Read;
        this.path = path;
    }

    public void Dispose()
    {
        CloseNetCDF(id, path);
    }

    public IReadOnlyList<Dimension> GetDimensions()
    {
        int[] dimids = GetDimensionIds(id);

        Dimension[] dimensions = new Dimension[dimids.Length];
        for (int i = 0; i < dimids.Length; i++)
        {
            string name = GetDimensionName(id, dimids[i]);
            int length = GetDimensionLength(id, dimids[i]);
            dimensions[i] = new Dimension(name, length);
        }
        return dimensions;
    }

    public IReadOnlyList<Variable> GetVariables()
    {
        int[] varids = GetVariableIDs(id);

        Variable[] variables = new Variable[varids.Length];
        for (int i = 0; i < varids.Length; i++)
        {
            GetVariable(id, varids[i], out string name, out NcType nctype, out int[] dimids, out int nattr);
            IEnumerable<string> dimNames = dimids.Select(dimid => GetDimensionName(id, dimid));
            Type type = nctype.ToType();
            IEnumerable<Attribute> attributes = Enumerable.Range(0, nattr).Select(j => GetAttribute(id, varids[i], j));
            ZLibOptions zlib = GetZLibOptions(id, varids[i]);
            GetChunkSizes(id, varids[i], out ChunkMode chunkMode, out int[] chunks);
            variables[i] = new Variable(name, dimNames, type, attributes, zlib, chunkMode, chunks);
        }
        return variables;
    }

    public int GetNTime()
    {
        int dimid = GetDimensionID(id, DimTime);
        return GetDimensionLength(id, dimid);
    }

    /// <summary>
    /// Create a new dimension in the file.
    /// </summary>
    public void AddDimension(string name, int length = 0)
    {
        if (readOnly)
            throw new InvalidOperationException($"Unable to create dimension {name}: file is read-only");

        CreateDimension(id, name, length);
    }

    public void AddVariable(string name, Type type, IEnumerable<string> dimensions, ZLibOptions? zlib = null)
    {
        if (readOnly)
            throw new InvalidOperationException($"Unable to create variable {name}: file is read-only");

        int varid = CreateVariable(id, name, type, dimensions);

        if (zlib != null && zlib.DeflateLevel > 0)
            EnableCompression(id, varid, zlib);
    }
}
