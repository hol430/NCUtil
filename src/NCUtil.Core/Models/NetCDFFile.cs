using System.Buffers;
using System.Security.Cryptography;
using System.Text;
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

    // These are mutable if the file is opened in write/append mode.
    private readonly List<Dimension> dimensions;
    private readonly List<Variable> variables;
    private readonly List<Attribute> attributes;

    public IReadOnlyList<Dimension> Dimensions => dimensions;
    public IReadOnlyList<Variable> Variables => variables;
    public IReadOnlyList<Attribute> Attributes => attributes;

    public NetCDFFile(string path, NetCDFFileMode mode = NetCDFFileMode.Read)
    {
        id = Open(path, mode);
        readOnly = mode == NetCDFFileMode.Read;
        this.path = path;
        basename = Path.GetFileName(path);

        // Read file metadata.

        // Technically we could do this on-demand, but the disk I/O behind these
        // operations occurs as soon as we invoke nc_open(), so the only time
        // saved would be the negligible overhead incurred by native interop.
        dimensions = ReadDimensions().ToList();
        variables = ReadVariables().ToList();
        attributes = ReadAttributes().ToList();
    }

    public void Dispose()
    {
        Close();
    }

    public Dimension GetDimension(string name)
    {
        foreach (Dimension dimension in Dimensions)
            if (dimension.Name == name)
                return dimension;
        throw new InvalidOperationException($"File {basename} does not contain dimension '{name}'");
    }

    public Variable GetVariable(string name)
    {
        foreach (Variable variable in Variables)
            if (variable.Name == name)
                return variable;
        throw new InvalidOperationException($"File {basename} does not contain variable '{name}'");
    }

    public int GetNTime()
    {
        return this.GetTimeDimension().Size;
    }

    /// <summary>
    /// Create a new dimension in the file.
    /// </summary>
    /// <param name="name">Name of the dimension.</param>
    /// <param name="length">Length of the dimension. Zero means unlimited dimension.</param>
    public Dimension CreateDimension(string name, int length = 0)
    {
        if (readOnly)
            throw new InvalidOperationException($"Unable to create dimension {name}: file is read-only");

        Dimension dimension = new Dimension(id, name, length);
        dimensions.Add(dimension);
        return dimension;
    }

    /// <summary>
    /// Create a new variable in a NetCDF file.
    /// </summary>
    /// <param name="name">Name of the variable to be created.</param>
    /// <param name="dimensions">Names of the dimensions of the variable to be created (in desired order).</param>
    /// <param name="type">Type of the variable to be created.</param>
    /// <param name="chunking">(Optional) Preferred chunk sizes. If not null, the variable will be chunked. If null, the variable will be packed as compact if it's small enough, or contiguous otherwise.</param>
    /// <param name="allowCompact">If true, and the variable is small enough, and no chunk sizes are provided, the variable will be created as packed. If false, the variable will be chunked (iff chunk sizes are specified) or contiguous.</param>
    /// <param name="compression">(Optional) compression algorithm to be used. Null means no compression.</param>
    public Variable CreateVariable(string name, IEnumerable<string> dimensions, Type type, ChunkSizes? chunking, bool allowCompact, ICompressionAlgorithm? compression = null)
    {
        if (readOnly)
            throw new InvalidOperationException($"Unable to create variable {name}: file is read-only");

        Log.Diagnostic("Creating variable {0} in file {1} with type {2} and dimensions '{3}'",
            name,
            basename,
            type.ToFriendlyName(),
            string.Join(", ", dimensions));

        // Choose packing strategy.
        (PackType packing, int[]? chunkSizes) = DeterminePacking(name, type, dimensions, allowCompact, chunking);

        // Create the variable.
        Variable variable = new Variable(id, name, dimensions, type.ToNCType(), packing, chunkSizes, compression);
        variables.Add(variable);

        return variable;
    }

    /// <summary>
    /// Write a file-level attribute.
    /// </summary>
    /// <param name="attribute">The attribute to be written.</param>
    public void CreateAttribute(string name, object value, Type type)
    {
        if (readOnly)
            throw new InvalidOperationException($"Unable to create attribute {name}: file is read-only");
        Attribute attribute = new Attribute(id, NCConst.NC_GLOBAL, name, value, type);
        attributes.Add(attribute);
    }

    /// <summary>
    /// Choose a packing strategy for a variable to be created.
    /// </summary>
    /// <param name="type">The name of the variable to be created.</param>
    /// <param name="type">The type of the variable to be created.</param>
    /// <param name="dimensions">Dimensions of the variable to be created.</param>
    /// <param name="allowCompact">True to allow the variable to be created compact. Note that compact packing will only be used if the no chunk sizes are specified, and the variable is small enough to be packed.</param>
    /// <param name="preferredChunkSizes">Preferred chunk sizes. The varaible will always be chunked if these are non-null.</param>
    private (PackType, int[]?) DeterminePacking(string name, Type type, IEnumerable<string> dimensions, bool allowCompact, ChunkSizes? preferredChunkSizes)
    {
        int[] dimids = dimensions.Select(d => Dimension.GetID(id, d)).ToArray();
        long variableLength = dimids.Product(d => Dimension.GetLength(id, d));
        int dataSize = type.ToNCType().DataSize();
        long variableSize = variableLength * dataSize;

        if (preferredChunkSizes != null && dimensions.Any() && preferredChunkSizes.ContainsAll(dimensions))
        {
            // All dimensions of this variable have a user-specified chunk size.
            int[] chunkSizes = preferredChunkSizes.GetChunkSizes(dimensions);

            Log.Debug("Variable {0} in file {1} will be chunked with chunk sizes: {2}",
                name,
                basename,
                string.Join(", ", dimensions.Zip(chunkSizes).Select(x => $"{x.First}:{x.Second}")));

            foreach ((int size, string dimension) in chunkSizes.Zip(dimensions))
            {
                int dimid = Dimension.GetID(id, dimension);
                int dimensionLength = Dimension.GetLength(id, dimid);
                if (dimensionLength < size)
                    throw new InvalidOperationException($"Chunk size on dimension {dimension} ({size}) is less than dimension length ({dimensionLength})");
            }

            return (PackType.Chunked, chunkSizes);
        }
        else if (allowCompact && variableSize < maxCompactSize)
        {
            Log.Debug("Variable {0} in file {1} will be created as compact (total size = {2}) ", name, basename, variableSize);
            return (PackType.Compact, null);
        }
        else
        {
            Log.Diagnostic("Variable {0} in file {1} will be created contiguous", name, basename);
            return (PackType.Contiguous, null);
        }
    }

    /// <summary>
    /// Get the number of dimensions in this file.
    /// </summary>
    public int GetNumDimensions()
    {
        Log.Debug("Calling nc_inq_ndims()...");

        int res = NetCDFNative.nc_inq_ndims(id, out int ndim);
        CheckResult(res, "Failed to get dimensions");

        Log.Debug("Call to nc_inq_ndims() was successful");

        return ndim;
    }

    /// <summary>
    /// Get the IDs of all dimensions in this file.
    /// </summary>
    private int[] GetDimensionIDs()
    {
        int ndim = GetNumDimensions();

        int[] dimids = new int[ndim];
        int res = NetCDFNative.nc_inq_dimids(id, out int ndim2, dimids, 0);
        CheckResult(res, "Failed to get dimension IDs");

        // Not sure what could cause this. Parallel IO? File corruption?
        // Let's leave this here as a sanity check anyway.
        if (ndim != ndim2)
            throw new InvalidOperationException($"nc_inq_dimids() returned different dimension length ({ndim2}) than nc_inq_ndims() ({ndim})");

        return dimids;
    }

    /// <summary>
    /// Get the number of variables in this file.
    /// </summary>
    private int GetNumVars()
    {
        Log.Debug("Calling nc_inq_nvars()");

        int res = NetCDFNative.nc_inq_nvars(id, out int nvars);
        CheckResult(res, "nc_inq_nvars(): failed to get number of variables");

        Log.Debug("nc_inq_nvars(): file {0} contains {1} variables", basename, nvars);
        return nvars;
    }

    /// <summary>
    /// Get the IDs of all variables in this file.
    /// </summary>
    private int[] GetVariableIDs()
    {
        int nvar = GetNumVars();
        int[] varids = new int[nvar];

        Log.Debug("Calling nc_inq_varids()");

        int res = NetCDFNative.nc_inq_varids(id, out int nvar2, varids);
        CheckResult(res, "nc_inq_varids()");

        if (nvar != nvar2)
            throw new Exception($"Number of variables appears to have changed from {nvar} to {nvar2}");

        return varids;
    }

    /// <summary>
    /// Get the number of file-level attributes.
    /// </summary>
    /// <param name="ncid">NetCDF file ID.</param>
    private int GetNumAttributes()
    {
        Log.Debug("Calling nc_inq_natts() for file {0}...", path);

        int res = NetCDFNative.nc_inq_natts(id, out int natts);
        CheckResult(res, "Failed to get number of file-level attributes");

        Log.Debug("Call to nc_inq_natts() was successful; file {0} has {1} global attributes", path, natts);
        return natts;
    }

    /// <summary>
    /// Get the ID of the variable with the specified name.
    /// </summary>
    /// <param name="name">Name of the variable.</param>
    private int GetVariableID(string name)
    {
        Log.Debug("Calling nc_inq_varid() for variable {0} in file {1}", name, basename);

        int res = NetCDFNative.nc_inq_varid(id, name, out int varid);
        CheckResult(res, "Failed to get ID of variable {0}", name);

        Log.Debug("Call to nc_inq_varid() was successful; variable {0} in file {1} has ID {2}", name, basename, varid);
        return varid;
    }

    /// <summary>
    /// Read all dimensions from the input file.
    /// </summary>
    private IEnumerable<Dimension> ReadDimensions()
    {
        int[] dimids = GetDimensionIDs();
        return dimids.Select(d => new Dimension(id, d));
    }

    /// <summary>
    /// Read all variables from the input file.
    /// </summary>
    private IEnumerable<Variable> ReadVariables()
    {
        int[] varids = GetVariableIDs();
        return varids.Select(v => new Variable(id, v));
    }

    /// <summary>
    /// Read all file-level attributes from the input file.
    /// </summary>
    private IEnumerable<Attribute> ReadAttributes()
    {
        int nattr = GetNumAttributes();
        return Enumerable.Range(0, nattr)
                         .Select(i => new Attribute(id, NCConst.NC_GLOBAL, i));
    }

    /// <summary>
    /// Open the specified NetCDF file and return its file ID.
    /// </summary>
    /// <param name="file">Path to the NetCDF file.</param>
    /// <param name="mode">File open mode.</param>
    private int Open(string file, NetCDFFileMode mode)
    {
        if (mode == NetCDFFileMode.Append && !File.Exists(file))
            throw new FileNotFoundException($"Unable to open netcdf file: file does not exist: {file}");
        else if (mode == NetCDFFileMode.Write)
            return Create(file);

        int result = NetCDFNative.nc_open(file, mode.ToOpenMode(), out int id);
        CheckResult(result, "Failed to open netcdf file");

        Log.Diagnostic("Successfully opened netcdf file in mode {0}: '{1}'", mode.ToEnumString(), file);
        return id;
    }

    private int Create(string file)
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
    private void Close()
    {
        Log.Debug("Closing netcdf file: {0}...", path);

        int result = NetCDFNative.nc_close(id);
        CheckResult(result, "Failed to close netcdf file: {0}", path);

        Log.Diagnostic("Successfully closed netcdf file: {0}", path);
    }
}
