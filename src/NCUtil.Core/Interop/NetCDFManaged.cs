using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using NCUtil.Core.Extensions;
using NCUtil.Core.Logging;
using NCUtil.Core.Models;
using Attribute = NCUtil.Core.Models.Attribute;
using Range = NCUtil.Core.Models.Range;

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
