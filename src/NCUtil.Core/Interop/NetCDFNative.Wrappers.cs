using System.Buffers;
using System.Security.Authentication.ExtendedProtection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using NCUtil.Core.Extensions;
using static NCUtil.Core.Interop.StringHandling;

namespace NCUtil.Core.Interop;

public static partial class NetCDFNative
{
    private static readonly ArrayPool<byte> pool = ArrayPool<byte>.Create();
    private const bool clearBuffer = true;

    /// <summary>Learn the path used to open/create the file.</summary>
    public static int nc_inq_path(int ncid, out string? path)
    {
        // Get the length.
        int res = nc_inq_path(ncid, out nint plen, null);
        if (res != 0)
        {
            path = null;
            return res;
        }

        // Allocate a buffer of the required length.
        byte[] buffer = pool.Rent((int)plen);
        res = nc_inq_path(ncid, out plen, buffer);
        if (res != 0)
        {
            pool.Return(buffer, clearBuffer);
            path = null;
            return res;
        }

        path = ReadBuffer(buffer, (int)plen);
        pool.Return(buffer, clearBuffer);
        return 0;
    }

    /// <summary>
    /// Find out the name of a dimension.
    /// </summary>
    public static int nc_inq_dimname(int ncid, int dimid, out string? name)
    {
        byte[] buffer = pool.Rent(NCConst.NC_MAX_NAME + 1);
        int res = nc_inq_dimname(ncid, dimid, buffer);
        if (res != 0)
        {
            pool.Return(buffer, clearBuffer);
            name = null;
            return res;
        }
        name = ReadBuffer(buffer);
        pool.Return(buffer, clearBuffer);
        return 0;
    }

    public static int nc_inq_var(int ncid, int varid, out string? name, out NCType type, out int ndims, int[] dimids, out int natts)
    {
        byte[] buffer = pool.Rent(NCConst.NC_MAX_NAME + 1);
        int res = nc_inq_var(ncid, varid, buffer, out type, out ndims, dimids, out natts);
        if (res != 0)
        {
            pool.Return(buffer, clearBuffer);
            name = null;
            return res;
        }

        name = ReadBuffer(buffer);
        pool.Return(buffer, clearBuffer);
        return 0;
    }

    public static int nc_inq_varname(int ncid, int varid, out string? name)
    {
        byte[] buffer = pool.Rent(NCConst.NC_MAX_NAME + 1);
        int res = nc_inq_varname(ncid, varid, buffer);
        if (res != 0)
        {
            pool.Return(buffer, clearBuffer);
            name = null;
            return res;
        }

        name = ReadBuffer(buffer);
        pool.Return(buffer, clearBuffer);
        return 0;
    }

    public static int nc_inq_attname(int ncid, int varid, int attnum, out string? name)
    {
        name = null;
        byte[] buffer = pool.Rent(NCConst.NC_MAX_NAME + 1);
        try
        {
            int res = nc_inq_attname(ncid, varid, attnum, buffer);
            if (res != 0)
                return res;

            name = ReadBuffer(buffer);
            return 0;
        }
        finally
        {
            pool.Return(buffer, clearBuffer);
        }
    }

    public static int nc_get_att_text(int ncid, int varid, string name, out string? value, int maxLength)
    {
        value = null;

        // In case netcdf adds terminating zero.
        byte[] buffer = pool.Rent(maxLength + 2);

        try
        {
            int res = nc_get_att_text(ncid, varid, name, buffer, maxLength);
            if (res != 0)
                return res;

            value = ReadBuffer(buffer);
            return 0;
        }
        finally
        {
            pool.Return(buffer, clearBuffer);
        }
    }

    public static int nc_get_att_string(int ncid, int varid, string name, string[] ip)
    {
        IntPtr[] parr = new IntPtr[ip.Length];

        int res = nc_get_att_string(ncid, varid, name, parr);
        if (res != 0)
            return res;

        for (int i = 0; i < ip.Length; i++)
            // String should be non-null when res != 0.
            ip[i] = ReadString(parr[i]) ?? string.Empty;

        return nc_free_string(new IntPtr(ip.Length), parr);
    }

    public static int nc_get_vara_string(int ncid, int varid, IntPtr[] start, IntPtr[] count, string[] data)
    {
        IntPtr[] parr = new IntPtr[data.Length];

        int res = nc_get_vara_string(ncid, varid, start, count, parr);
        if (res != 0)
            return res;

        for (int i = 0; i < data.Length; i++)
            data[i] = ReadString(parr[i]) ?? string.Empty;

        return nc_free_string(new IntPtr(data.Length), parr);
    }

    /// <summary>
    /// Store the specified string values as an attribute.
    /// </summary>
    /// <param name="ncid">ID of the NetCDF file.</param>
    /// <param name="varid">ID of the variable on which the attribute should be created. Use <see cref="NCConst.NC_GLOBAL"/> for a global attribute.</param>
    /// <param name="name">Name of the attribute.</param>
    /// <param name="tp">Values of the attribute. Use a single-element array for a scalar attribute.</param>
    unsafe public static int nc_put_att_string(int ncid, int varid, string name, string[] tp)
    {
        // TODO: replace with custom marshaler
        IntPtr[] bb = new IntPtr[tp.Length];
        (byte[] buffer, uint[] offsets) = WriteStrings(tp);
        fixed (byte* buf = buffer)
        {
            for (int i = 0; i < tp.Length; i++)
            {
                if (uint.MaxValue == offsets[i])
                    bb[i] = IntPtr.Zero;
                else
                    bb[i] = new IntPtr(buf + offsets[i]);
            }
            return nc_put_att_string(ncid, varid, name, new IntPtr(bb.Length), bb);
        }
    }

    private static string ReadBuffer(byte[] buffer)
    {
        return ReadBuffer(buffer, buffer.IndexOf(c => c == 0));
    }

    private static string ReadBuffer(byte[] buffer, int len)
    {
        return Encoding.UTF8.GetString(buffer, 0, len);
    }
}
