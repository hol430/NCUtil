namespace NCUtil.Core.Interop;

/// <summary>
/// The file creation mode flag. The following flags are available:
/// NC_CLOBBER (overwrite existing file),
/// NC_NOCLOBBER (do not overwrite existing file),
/// NC_SHARE (limit write caching - netcdf classic files only),
/// NC_64BIT_OFFSET (create 64-bit offset file),
/// NC_64BIT_DATA (alias NC_CDF5) (create CDF-5 file),
/// NC_NETCDF4 (create netCDF-4/HDF5 file),
/// NC_CLASSIC_MODEL (enforce netCDF classic mode on netCDF-4/HDF5 files),
/// NC_DISKLESS (store data in memory), and NC_PERSIST (force the NC_DISKLESS data from memory to a file),
/// NC_MMAP (use MMAP for NC_DISKLESS instead of NC_INMEMORY – deprecated).
/// </summary>
public enum CreateMode : int
{
    /// <summary>
    /// Overwrite existing file. Mode flag for nc_create().
    /// </summary>
    NC_CLOBBER = 0x0000,

    /// <summary>
    /// Don't destroy existing file. Mode flag for nc_create().
    /// </summary>
    NC_NOCLOBBER = 0x0004,

    /// <summary>
    /// Use diskless file. Mode flag for nc_open() or nc_create().
    /// </summary>
    NC_DISKLESS = 0x0008,

    /// <summary>
    /// deprecated Use diskless file with mmap. Mode flag for nc_open() or nc_create().
    /// </summary>
    NC_MMAP = 0x0010,

    /// <summary>
    /// CDF-5 format: classic model but 64 bit dimensions and sizes.
    /// </summary>
    NC_64BIT_DATA = 0x0020,

    /// <summary>
    /// Enforce classic model on netCDF-4. Mode flag for nc_create().
    /// </summary>
    NC_CLASSIC_MODEL = 0x0100,

    /// <summary>
    /// Use large (64-bit) file offsets. Mode flag for nc_create().
    /// </summary>
    NC_64BIT_OFFSET = 0x0200,

    /// <summary>
    /// Share updates, limit caching. Use this in mode flags for both nc_create() and nc_open().
    /// </summary>
    NC_SHARE = 0x0800,

    /// <summary>
    /// se netCDF-4/HDF5 format. Mode flag for nc_create().
    /// </summary>
    NC_NETCDF4 = 0x1000,

    /// <summary>
    /// Save diskless contents to disk. Mode flag for nc_open() or nc_create().
    /// </summary>
    NC_PERSIST = 0x4000
}
