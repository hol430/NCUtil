using System.Security.Cryptography;
using NCUtil.Core.Extensions;
using NCUtil.Core.Interop;
using NCUtil.Core.Logging;

using static NCUtil.Core.Interop.NetCDFManaged;

namespace NCUtil.Core.Models;

public class NetCDFFile : IDisposable
{
    /// <summary>
    /// Maximum size of a compact variable is 64 KiB.
    /// </summary>
    private const int maxCompactSize = 64 * 1024;

    private readonly string path;
    private readonly string basename;
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
        basename = Path.GetFileName(path);
    }

    public void Dispose()
    {
        CloseNetCDF(id, path);
    }

    public IReadOnlyList<Dimension> GetDimensions()
    {
        int[] dimids = GetDimensionIds(id);
        return dimids.Select(GetDimension).ToArray();
    }

    public Dimension GetDimension(string name)
    {
        int dimid = GetDimensionID(id, name);
        int length = GetDimensionLength(id, dimid);
        return new Dimension(name, length);
    }

    public Dimension GetDimension(int dimid)
    {
        string name = GetDimensionName(id, dimid);
        int length = GetDimensionLength(id, dimid);
        return new Dimension(name, length);
    }

    public IReadOnlyList<Variable> GetVariables()
    {
        int[] varids = GetVariableIDs(id);

        Variable[] variables = new Variable[varids.Length];
        for (int i = 0; i < varids.Length; i++)
            variables[i] = GetVariable(varids[i]);
        return variables;
    }

    public Variable GetVariable(string name)
    {
        int varid = GetVariableID(id, name);
        return GetVariable(varid);
    }

    public Variable GetVariable(int varid)
    {
        NetCDFManaged.GetVariable(id, varid, out string name, out NCType type, out int[] dimids, out int nattr);
        IEnumerable<string> dimNames = dimids.Select(dimid => GetDimensionName(id, dimid));
        IEnumerable<Attribute> attributes = Enumerable.Range(0, nattr).Select(j => GetAttribute(id, varid, j));
        ZLibOptions zlib = GetZLibOptions(id, varid);
        GetChunkSizes(id, varid, out ChunkMode chunkMode, out int[] chunks);
        return new Variable(id, varid, name, dimNames, type, attributes, zlib, chunkMode, chunks);
    }

    /// <summary>
    /// Get all file-level attributes.
    /// </summary>
    public IEnumerable<Attribute> GetAttributes()
    {
        int nattr = GetNumAttributes(id);
        Attribute[] attributes = new Attribute[nattr];
        return Enumerable.Range(0, nattr)
                         .Select(i => GetAttribute(id, NCConst.NC_GLOBAL, i));
    }

    public int GetNTime()
    {
        return this.GetTimeDimension().Size;
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

    public void AddVariable(Variable variable, ChunkSizes? chunking, bool allowCompact, int compressionLevel)
    {
        if (readOnly)
            throw new InvalidOperationException($"Unable to create variable {variable.Name}: file is read-only");

        Log.Diagnostic("Creating variable {0} in file {1} with type {2} and dimensions '{3}'",
            variable.Name,
            basename,
            variable.DataType.Name,
            string.Join(", ", variable.Dimensions));

        int varid = CreateVariable(id, variable.Name, variable.DataType, variable.Dimensions);

        long variableLength = variable.GetLength();
        int dataSize = variable.DataType.ToNCType().DataSize();
        long variableSize = variableLength * dataSize;

        // Set chunk sizes and strategy.
        // Variables are contiguous by default.
        if (chunking != null && variable.Dimensions.Count > 0 && chunking.ContainsAll(variable.Dimensions))
        {
            // All dimensions of this variable have a user-specified chunk
            // size.
            int[] sizes = chunking.GetChunkSizes(variable.Dimensions);

            Log.Diagnostic("Enabling chunking on variable {0} in file {1} with chunk sizes: {2}",
                variable.Name,
                basename,
                string.Join(", ", variable.Dimensions.Zip(sizes).Select(x => $"{x.First}:{x.Second}")));

            foreach ((int size, string dimension) in sizes.Zip(variable.Dimensions))
            {
                int dimid = GetDimensionID(id, dimension);
                int dimensionLength = GetDimensionLength(id, dimid);
                if (dimensionLength < size)
                    throw new InvalidOperationException($"Chunk size on dimension {dimension} ({size}) is less than dimension length ({dimensionLength})");
            }

            SetVariableChunking(id, varid, sizes);
        }
        else if (allowCompact && variableSize < maxCompactSize)
        {
            Log.Diagnostic("Enabling compact packing on variable {0} in file {1}", variable.Name, basename);
            MakeCompact(id, varid);
        }
        else
        {
            Log.Diagnostic("Variable {0} will be left as contiguous", variable.Name, basename);
        }

        // Set compression.
        // TODO: custom compression type.
        // Compression level of -1 means same as input file.
        // Compression level of 0 means no compression.
        // This should be refactored. It could be done better.
        if (compressionLevel > 0)
        {
            ZLibOptions zlib = new ZLibOptions(true, compressionLevel);
            EnableCompression(id, varid, zlib);
        }
        else if (compressionLevel == -1 && variable.Zlib != null && variable.Zlib.DeflateLevel > 0)
        {
            Log.Diagnostic("Enabling zlib compression on variable {0} in file {1} with deflation level {2}",
                variable.Name,
                basename,
                variable.Zlib.DeflateLevel);

            EnableCompression(id, varid, variable.Zlib);
        }

        // TODO: Custom dimension order.

        // Set metadata.
        foreach (var attribute in variable.Attributes)
        {
            Log.Diagnostic("Setting attribute {0} on variable {1} in file {2}",
                attribute.Name,
                variable.Name,
                basename);

            SetAttribute(id, varid, attribute);
        }
    }

    /// <summary>
    /// Write a file-level attribute.
    /// </summary>
    /// <param name="attribute">The attribute to be written.</param>
    public void SetGlobalAttribute(Attribute attribute)
    {
        SetAttribute(id, NCConst.NC_GLOBAL, attribute);
    }
}
