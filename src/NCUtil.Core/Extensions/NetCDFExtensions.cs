using NCUtil.Core.Models;
using NetCDFInterop;

namespace NCUtil.Core.Extensions;

public static class NetCDFExtensions
{
    public static CreateMode ToCreateMode(this NetCDFFileMode mode)
    {
        switch (mode)
        {
            case NetCDFFileMode.Read:
                return CreateMode.NC_NOWRITE;
            case NetCDFFileMode.Write:
            case NetCDFFileMode.Append:
                return CreateMode.NC_WRITE;
            default:
                throw new ArgumentException($"Unknown netcdf file mode: {mode}");
            
        }
    }

    public static bool IsTime(this Dimension dimension)
    {
        return dimension.Name == NetCDFFile.DimTime;
    }
}