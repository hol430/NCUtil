namespace NCUtil.Core.Models;

/// <summary>
/// Defines how a variable is packed inside a NetCDF file. These values may be
/// passed as integers directly into the NetCDF API.
/// </summary>
public enum PackType
{
    // NC_CHUNKED
    Chunked = 0,
    // NC_CONTIGUOUS
    Contiguous = 1,
    // NC_COMPACT
    Compact = 2
}
