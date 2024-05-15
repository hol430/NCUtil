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

        int variableLength = variable.Dimensions.Select(d => GetDimensionLength(id, GetDimensionID(id, d))).Product();
        int dataSize = variable.DataType.ToNcType().DataSize();
        int variableSize = variableLength * dataSize;

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
