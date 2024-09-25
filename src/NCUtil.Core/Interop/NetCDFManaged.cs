namespace NCUtil.Core.Interop;

internal static class NetCDFManaged
{
    public static void CheckResult(int result, string format, params object[] args)
    {
        if (result != 0)
        {
            string context = string.Format(format, args);
            string error = NetCDFNative.nc_strerror(result);
            throw new Exception($"{context}: {error}");
        }
    }
}
