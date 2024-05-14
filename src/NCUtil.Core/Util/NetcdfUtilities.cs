using Microsoft.Research.Science.Data;
using Microsoft.Research.Science.Data.Imperative;
using NCUtil.Core.Logging;

namespace NCUtil.Core.Util;

public static class NetcdfUtilities
{
    public static DataSet OpenNetcdf(string path, ResourceOpenMode mode = ResourceOpenMode.Open)
    {
        Log.Debug("Opening netcdf file '{0}' in mode {1}...", path, mode);

        DataSet dataset = DataSet.Open(path, mode);
        Log.Diagnostic("Successfully opened netcdf file '{0}' in mode {1}", path, mode);
        return dataset;
    }
}
