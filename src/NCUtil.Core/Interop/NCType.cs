namespace NCUtil.Core.Interop;

/// <summary>The netcdf external data types</summary>
public enum NCType : int
{
    /// <summary>
    /// Signed 1 byte integer.
    /// In C# this is sbyte but the NetCDF variable type is schar (e.g.nc_put_var_schar).
    /// </summary>
    NC_BYTE = 1,

    /// <summary>
    /// ISO/ASCII character.
    /// </summary>
    NC_CHAR = 2,

    /// <summary>
    /// Signed 2 byte integer.
    /// </summary>
    NC_SHORT = 3,

    /// <summary>
    /// Signed 4 byte integer.
    /// </summary>
    NC_INT = 4,

    /// <summary>
    /// Single precision floating point number.
    /// </summary>
    NC_FLOAT = 5,

    /// <summary>
    /// Double precision floating point number.
    /// </summary>
    NC_DOUBLE = 6,

    /// <summary>
    /// Unsigned 1 byte integer.
    /// In C# this is byte but the NetCDF variable type is ubyte (e.g.nc_put_var_ubyte).
    /// </summary>
    NC_UBYTE = 7,

    /// <summary>
    /// Unsigned 2-byte int.
    /// </summary>
    NC_USHORT = 8,

    /// <summary>
    /// Unsigned 4-byte int.
    /// </summary>
    NC_UINT = 9,

    /// <summary>
    /// Signed 8-byte int.
    /// </summary>
    NC_INT64 = 10,

    /// <summary>
    /// Unsigned 8-byte int.
    /// </summary>
    NC_UINT64 = 11,

    /// <summary>
    /// String.
    /// </summary>
    NC_STRING = 12,

    // The following are use internally in support of user-defined types. They
    // are also the class returned by nc_inq_user_type.

    /// <summary>
    /// vlen (variable-length) types.
    /// </summary>
    NC_VLEN = 13,

    /// <summary>
    /// Opaque types.
    /// </summary>
    NC_OPAQUE = 14,

    /// <summary>
    /// Enum types.
    /// </summary>
    NC_ENUM = 15,

    /// <summary>
    /// Compound types.
    /// </summary>
    NC_COMPOUND = 16
}
