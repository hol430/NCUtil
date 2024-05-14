namespace NCUtil.Core.Models;

public enum NetCDFFileMode
{
    /// <summary>
    /// Open the file in read-only mode. Throw if it doesn't exist.
    /// </summary>
    Read,

    /// <summary>
    /// Open the file in read-write mode. Clobber it if it already exists.
    /// </summary>
    Write,

    /// <summary>
    /// Append to an existing file. Throw if it doesn't exist.
    /// </summary>
    Append
}
