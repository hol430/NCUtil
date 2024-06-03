namespace NCUtil.Core.Interop;

/// <summary>
/// The open mode flags.
/// </summary>
public enum OpenMode : int
{
    /// <summary>
    /// Set read-only access for nc_open().
    /// </summary>
    NC_NOWRITE = 0x0000,

    /// <summary>
    /// Set read-write access for nc_open().
    /// </summary>
    NC_WRITE = 0x0001,

    /// <summary>
    /// Use diskless file. Mode flag for nc_open() or nc_create().
    /// </summary>
    NC_DISKLESS = 0x0008,

    /// <summary>
    /// Share updates, limit caching. Use this in mode flags for both nc_create() and nc_open().
    /// </summary>
    NC_SHARE = 0x0800,

    /// <summary>
    /// Read from memory. Mode flag for nc_open() or nc_create().
    /// </summary>
    NC_INMEMORY = 0x8000
}
