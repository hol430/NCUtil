using NCUtil.Core.Interop;
using NCUtil.Core.Logging;
using static NCUtil.Core.Interop.NetCDFManaged;

namespace NCUtil.Core.Models;

public class Dimension
{
    private int ncid;
    private int dimid;

    public string Name { get; private init; }
    public int Size { get; private init; }

    /// <summary>
    /// Create a new dimension in a NetCDF file.
    /// </summary>
    /// <param name="ncid">ID of the NetCDF file.</param>
    /// <param name="name">Name of the dimension to be created.</param>
    /// <param name="length">Length of the dimension to be created. 0 means unlimited. Negative values are illegal.</param>
    internal Dimension(int ncid, string name, int length)
    {
        this.ncid = ncid;
        Name = name;
        Size = length;

        if (length < 0)
            throw new InvalidOperationException($"Attempted to create dimension with negative length: {length}");

        if (length == 0)
        {
            Log.Debug("Dimension {0} will be of unlimited length", name);

            // Technically redundant, I'm leaving this here for clarity.
            length = NCConst.NC_UNLIMITED;
        }

        Log.Debug("Calling nc_def_dim() to create dimension {0} with length {1}", name, length);

        // Create the dimension.
        // Note: this.dimid as out parameter.
        int res = NetCDFNative.nc_def_dim(ncid, name, (nint)length, out dimid);
        CheckResult(res, "Failed to create dimension with name {0}", name);

        Log.Debug("Successfully created dimension {0} with length {1}", name, length);
    }

    /// <summary>
    /// Create a managed dimension object for a dimension which already exists.
    /// </summary>
    /// <param name="ncid">The ID of the NetCDF file.</param>
    /// <param name="dimid">The ID of the dimension.</param>
    internal Dimension(int ncid, int dimid)
    {
        this.ncid = ncid;
        this.dimid = dimid;

        Size = GetLength();
        Name = GetName();
    }

    /// <summary>
    /// Create a managed dimension object for a dimension which already exists.
    /// </summary>
    /// <param name="ncid">The ID of the NetCDF file.</param>
    /// <param name="name">The name of the dimension.</param>
    internal Dimension(int ncid, string name)
    {
        this.ncid = ncid;
        Name = name;

        dimid = GetID(ncid, name);
        Size = GetLength();
    }

    /// <summary>
    /// Read the length of a dimension from a NetCDF file.
    /// </summary>
    /// <param name="ncid">ID of the NetCDF file.</param>
    /// <param name="dimid">ID of the dimension.</param>
    public static int GetLength(int ncid, int dimid)
    {
        Log.Debug("Calling nc_inq_dimlen() for dimension {0}", dimid);

        int res = NetCDFNative.nc_inq_dimlen(ncid, dimid, out nint length);
        CheckResult(res, "Failed to get length of dimension {0}: {1}", dimid);

        Log.Debug("Call to nc_inq_dimlen() was successful for dimension {0} and returned {1}", dimid, (int)length);
        return (int)length;
    }

    /// <summary>
    /// Read the name of a dimension from a NetCDF file.
    /// </summary>
    /// <param name="ncid">ID of the NetCDF file.</param>
    /// <param name="dimid">ID of the dimension.</param>
    public static string GetName(int ncid, int dimid)
    {
        Log.Debug("Calling nc_inq_dimname() for dimension {0}", dimid);

        int res = NetCDFNative.nc_inq_dimname(ncid, dimid, out string? name);
        CheckResult(res, "Failed to get name of dimension with ID {0}", dimid);

        // Name guaranteed to be non-null if result is zero.
        Log.Debug("Call to nc_inq_dimname() was successful for dimension {0}: {1}", dimid, name!);
        return name!;
    }

    /// <summary>
    /// Get the ID of the dimension with the specified name. Throw if the
    /// dimension does not exist.
    /// </summary>
    /// <param name="ncid">NetCDF file ID.</param>
    /// <param name="name">Name of the dimension.</param>
    public static int GetID(int ncid, string name)
    {
        Log.Debug("Calling nc_inq_dimid() for dimension {0}", name);

        int res = NetCDFNative.nc_inq_dimid(ncid, name, out int dimid);
        CheckResult(res, "Failed to get ID of dimension with name {0}", name);

        Log.Debug("Call to nc_inq_dimid() was successful; dimension {0} has ID {1}", name, dimid);
        return dimid;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{Name}: {Size}";
    }

    /// <summary>
    /// Read the length of this dimension from this NetCDF file.
    /// </summary>
    private int GetLength() => GetLength(ncid, dimid);

    /// <summary>
    /// Read the name of this dimension from this NetCDF file.
    /// </summary>
    private string GetName() => GetName(ncid, dimid);
}
