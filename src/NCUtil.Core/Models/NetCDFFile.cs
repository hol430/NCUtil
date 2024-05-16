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
        NetCDFManaged.GetVariable(id, varid, out string name, out NcType nctype, out int[] dimids, out int nattr);
        IEnumerable<string> dimNames = dimids.Select(dimid => GetDimensionName(id, dimid));
        Type type = nctype.ToType();
        IEnumerable<Attribute> attributes = Enumerable.Range(0, nattr).Select(j => GetAttribute(id, varid, j));
        ZLibOptions zlib = GetZLibOptions(id, varid);
        GetChunkSizes(id, varid, out ChunkMode chunkMode, out int[] chunks);
        return new Variable(name, dimNames, type, attributes, zlib, chunkMode, chunks);
    }

    /// <summary>
    /// Get all file-level attributes.
    /// </summary>
    public IEnumerable<Attribute> GetAttributes()
    {
        int nattr = GetNumAttributes(id);
        Attribute[] attributes = new Attribute[nattr];
        return Enumerable.Range(0, nattr)
                         .Select(i => GetAttribute(id, NcConst.NC_GLOBAL, i));
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

    public void AddVariable(Variable variable, ChunkSizes? chunking, bool allowCompact)
    {
        if (readOnly)
            throw new InvalidOperationException($"Unable to create variable {variable.Name}: file is read-only");

        Log.Diagnostic("Creating variable {0} in file {1} with type {2} and dimensions '{3}'",
            variable.Name,
            basename,
            variable.DataType.Name,
            string.Join(", ", variable.Dimensions));

        int varid = CreateVariable(id, variable.Name, variable.DataType, variable.Dimensions);

        long variableLength = GetVariableLength(varid);
        int dataSize = variable.DataType.ToNcType().DataSize();
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

            SetChunkSizes(variable.Name, sizes);
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
        if (variable.Zlib != null && variable.Zlib.DeflateLevel > 0)
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
        SetAttribute(id, NcConst.NC_GLOBAL, attribute);
    }

    public long GetVariableLength(string variable)
    {
        int varid = GetVariableID(id, variable);
        return GetVariableLength(varid);
    }

    /// <summary>
    /// Read from the specified variable.
    /// </summary>
    /// <param name="name">Name of the variable to be read.</param>
    /// <param name="hyperslab">The ranges to be read from the variable along
    /// each dimension. This array must have 1 element for each dimension of the
    /// variable, and must be in the same order as the dimensions.</param>
    public Array Read(string name, IRange[] hyperslab)
    {
        int varid = GetVariableID(id, name);
        return ReadVariable(id, varid, hyperslab);
    }

    public void Write(string name, IRange[] hyperslab, Array array)
    {
        int varid = GetVariableID(id, name);
        Write(varid, hyperslab, array);
    }

    private void Write(int varid, IRange[] hyperslab, Array array)
    {
        WriteVariable(id, varid, array, hyperslab);
    }

    private long GetVariableLength(int varid)
    {
        int[] dimids = GetVariableDimensionIDs(id, varid);
        return dimids.Select(d => GetDimensionLength(id, d)).Product();
    }

    /// <summary>
    /// Set the chunk sizes for the specified variable. This may only be called
    /// after the variable is defined but before the file is closed.
    /// </summary>
    /// <param name="variable">Name of the variable.</param>
    /// <param name="chunkSizes">Chunk size for each dimension - length must be same as number of dimensions for this variable.</param>
    private void SetChunkSizes(string variable, int[] chunkSizes)
    {
        if (readOnly)
            throw new InvalidOperationException($"Unable to set chunk sizes for variable {variable}: file is read-only");

        int varid = GetVariableID(id, variable);
        SetVariableChunking(id, varid, chunkSizes);
    }
}
