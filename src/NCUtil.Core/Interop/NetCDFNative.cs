using System.Runtime.InteropServices;

namespace NCUtil.Core.Interop;

public static class NetCDFNative
{
    [DllImport("netcdf", CallingConvention=CallingConvention.Cdecl)]
    public static extern int nc_inq_ndims(int ncid, out int ndims);

    /// <summary>Retrieve a list of dimension ids associated with a group</summary>
    [DllImport("netcdf", CallingConvention = CallingConvention.Cdecl)]
    public static extern int nc_inq_dimids(int ncid, out int ndims, int[] dimids, int include_parents);

    [DllImport("netcdf", CallingConvention = CallingConvention.Cdecl)]
    public static extern int nc_inq_nvars(int ncid, out int nvars);

    /// <summary>Get a list of varids associated with a group given a group ID.</summary>
    [DllImport("netcdf", CallingConvention = CallingConvention.Cdecl)]
    public static extern int nc_inq_varids(int ncid, out int nvars, int[] varids);

    /// <summary>Find out compression settings of a var.</summary>
    [DllImport("netcdf", CallingConvention = CallingConvention.Cdecl)]
    public static extern int nc_inq_var_deflate(int ncid, int varid, out int shufflep, out int deflatep, out int deflate_levelp);

    /// <summary>Inquire about fletcher32 checksum for a var.</summary>
    [DllImport("netcdf", CallingConvention = CallingConvention.Cdecl)]
    public static extern int nc_inq_var_fletcher32(int ncid, int varid, out int fletcher32p);

    /// <summary>Inq chunking stuff for a var.</summary>
    [DllImport("netcdf", CallingConvention = CallingConvention.Cdecl)]
    public static extern int nc_inq_var_chunking(int ncid, int varid, out int storagep, out nint chunksizesp);

    /// <summary>Inq fill value setting for a var.</summary>
    [DllImport("netcdf", CallingConvention = CallingConvention.Cdecl)]
    public static extern int nc_inq_var_fill(int ncid, int varid, out int no_fill, out int fill_valuep);

    /// <summary>Learn about the endianness of a variable.</summary>
    [DllImport("netcdf", CallingConvention = CallingConvention.Cdecl)]
    public static extern int nc_inq_var_endian(int ncid, int varid, out int endianp);

    /// <summary>Find out szip settings of a var.</summary>
    [DllImport("netcdf", CallingConvention = CallingConvention.Cdecl)]
    public static extern int nc_inq_var_szip(int ncid, int varid, out int options_maskp, out int pixels_per_blockp);

    [DllImport("netcdf", CallingConvention = CallingConvention.Cdecl)]
    public static extern int nc_inq_varndims(int ncid, int varid, out int ndims);

    [DllImport("netcdf", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr nc_strerror(int ncerr1);
}
