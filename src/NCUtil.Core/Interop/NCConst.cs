namespace NCUtil.Core.Interop;

public static class NCConst
{
    /// <summary>
    /// Size argument to nc_def_dim() for an unlimited dimension.
    /// </summary>
    public const int NC_UNLIMITED = 0;

    /// <summary>
    /// ID used to read or write a global attribute.
    /// </summary>
    public const int NC_GLOBAL = -1;

    /// <summary>
    /// Maximum length of the name of a variable or dimension in a class NetCDF
    /// file. The docs say to use this for functions like nc_inq_dimname() even
    /// in hdf5 files, even though the docs say this should not be used for hdf5
    /// files.
    /// </summary>
    public const int NC_MAX_NAME = 256;

    /** In HDF5 files you can set the endianness of variables with nc_def_var_endian(). This define is used there. */
    public const int NC_ENDIAN_NATIVE = 0;
    public const int NC_ENDIAN_LITTLE = 1;
    public const int NC_ENDIAN_BIG = 2;

    /** In HDF5 files you can set storage for each variable to be either contiguous or chunked, with nc_def_var_chunking().  This define is
        * used there. */
    public const int NC_CHUNKED = 0;
    public const int NC_CONTIGUOUS = 1;

    /* In HDF5 files you can set check-summing for each variable. Currently the only checksum available is Fletcher-32, which can be set
    with the function nc_def_var_fletcher32.  These defines are used there.
    */
    public const int NC_NOCHECKSUM = 0;
    public const int NC_FLETCHER32 = 1;

    /* Control the HDF5 shuffle filter. In HDF5 files you can specify that a shuffle filter should be used on each chunk of a variable to
        * improve compression for that variable. This per-variable shuffle property can be set with the function nc_def_var_deflate().
        */
    public const int NC_NOSHUFFLE = 0;
    public const int NC_SHUFFLE = 1;

    /* Control the compression
        */
    public const int NC_NODEFLATE = 0;
    public const int NC_DEFLATE = 1;

    /// <summary>Minimum deflate level.</summary>
    public const int NC_MIN_DEFLATE_LEVEL = 0;
    /// <summary>Maximum deflate level.</summary>
    public const int NC_MAX_DEFLATE_LEVEL = 9;

    /*  Format specifier for nc_set_default_format() and returned by nc_inq_format. This returns the format as provided by
        *  the API. See nc_inq_format_extended to see the true file format. Starting with version 3.6, there are different format netCDF files.
        *  4.0 introduces the third one. \see netcdf_format
    */
    public const int NC_FORMAT_CLASSIC = 1;
    /* After adding CDF5 support, the NC_FORMAT_64BIT flag is somewhat confusing. So, it is renamed.
        Note that the name in the contributed code NC_FORMAT_64BIT was renamed to NC_FORMAT_CDF2
    */
    public const int NC_FORMAT_64BIT_OFFSET = 2;
    /// <summary>deprecated Saved for compatibility.  Use NC_FORMAT_64BIT_OFFSET or NC_FORMAT_64BIT_DATA, from netCDF 4.4.0 onwards</summary>
    public const int NC_FORMAT_64BIT = (NC_FORMAT_64BIT_OFFSET);
    public const int NC_FORMAT_NETCDF4 = 3;
    public const int NC_FORMAT_NETCDF4_CLASSIC = 4;
    public const int NC_FORMAT_64BIT_DATA = 5;

    /* Alias */
    public const int NC_FORMAT_CDF5 = NC_FORMAT_64BIT_DATA;
}
