using System.Diagnostics;
using NCUtil.Core.Extensions;
using NCUtil.Core.Interop;
using NCUtil.Core.Logging;

using static NCUtil.Core.Interop.NetCDFManaged;

namespace NCUtil.Core.Models;

public class Attribute
{
    private readonly int ncid;
    private readonly int varid;

    public string Name { get; private init; }
    public object Value { get; private init; }
    public Type DataType { get; private init; }
    public int Length { get; private init; }

    internal Attribute(int ncid, int varid, string name, object value, Type type)
    {
        this.ncid = ncid;
        this.varid = varid;

        Name = name;
        Value = value;
        DataType = type;

        Create();
    }

    /// <summary>
    /// Create a managed attribute object for an attribute which already exists.
    /// </summary>
    /// <param name="ncid">ID of the NetCDF file.</param>
    /// <param name="varid">ID of the variable, or NCConst.NC_GLOBAL for a global attribute..</param>
    /// <param name="index">ID of the attribute in range [0, N-1], where N is the number of attributes on this variable.</param>
    internal Attribute(int ncid, int varid, int index)
    {
        this.ncid = ncid;
        this.varid = varid;

        int res = NetCDFNative.nc_inq_attname(ncid, varid, index, out string? name);
        CheckResult(res, "nc_inq_attname");
        Name = name!;

        Log.Debug("Variable {0} attribute {1} has name {2}", varid, index, name!);

        Log.Debug("Calling nc_inq_att()");
        res = NetCDFNative.nc_inq_att(ncid, varid, Name, out NCType nctype, out nint plength);
        CheckResult(res, "nc_inq_att()");

        Length = (int)plength;
        DataType = nctype.ToType();
        Log.Debug("Attribute {0} has type {1} and length {2}", Name, DataType.ToFriendlyName(), Length);

        Value = ReadAttribute();
        Debug.Assert(Value != null);
    }

    /// <summary>
    /// Create this variable.
    /// </summary>
    private void Create()
    {
        switch (DataType.ToNCType())
        {
            case NCType.NC_SHORT:
                SetAttributeValue<short>(NetCDFNative.nc_put_att_short);
                break;
            case NCType.NC_INT:
                SetAttributeValue<int>(NetCDFNative.nc_put_att_int);
                break;
            case NCType.NC_INT64:
                SetAttributeValue<long>(NetCDFNative.nc_put_att_longlong);
                break;

            case NCType.NC_USHORT:
                SetAttributeValue<ushort>(NetCDFNative.nc_put_att_ushort);
                break;
            case NCType.NC_UINT:
                SetAttributeValue<uint>(NetCDFNative.nc_put_att_uint);
                break;
            case NCType.NC_UINT64:
                SetAttributeValue<ulong>(NetCDFNative.nc_put_att_ulonglong);
                break;

            case NCType.NC_FLOAT:
                SetAttributeValue<float>(NetCDFNative.nc_put_att_float);
                break;
            case NCType.NC_DOUBLE:
                SetAttributeValue<double>(NetCDFNative.nc_put_att_double);
                break;

            case NCType.NC_BYTE:
                SetAttributeValue<sbyte>(NetCDFNative.nc_put_att_schar);
                break;
            case NCType.NC_UBYTE:
                SetAttributeValue<byte>(NetCDFNative.nc_put_att_ubyte);
                break;
            case NCType.NC_CHAR:
                SetAttributeValue<char>(SetCharAttributeValue);
                break;
            case NCType.NC_STRING:
                SetAttributeValue<string>(SetStringAttributeValue);
                break;
            default:
                throw new InvalidOperationException($"Unknown attribute type: {DataType.ToNCType().ToEnumString()}");
        }
    }

    /// <summary>
    /// Create this variable by invoking the specified nc_put_att_X func.
    /// </summary>
    /// <param name="nativeFunc">The native nc_put_att_X func.</param>
    private void SetAttributeValue<T>(Func<int, int, string, NCType, nint, T[], int> nativeFunc)
    {
        Log.Debug("Calling nc_put_att_{0}()", typeof(T).Name);

        // Convert the value into an array.
        T[] array;
        if (typeof(T).IsAssignableFrom(Value.GetType()))
            // This is a single-element attribute, so we store it as an array of
            // length 1.
            array = [(T)Value];
        else if (Value.GetType().IsArray)
            // The value is already an array so a cast is sufficient.
            array = (T[])Value;
        else if (Value is IEnumerable<T>)
            // The value is an IEnumerable with the correct element type. We can
            // just call its ToArray() function.
            array = ((IEnumerable<T>)Value).ToArray();
        else
            // Who knows what we're dealing with. The possibilities are endless.
            throw new InvalidOperationException($"Attempted to set attribute {Name} as {typeof(T).Name} attribute, but value is of type {Value.GetType().Name}");

        // Create the attribute.
        int res = nativeFunc(ncid, varid, Name, DataType.ToNCType(), array.Length, array);
        CheckResult(res, "Failed to set attribute {0}", Name);

        Log.Debug("Successfully set attribute {0}", Name);
    }

    /// <summary>
    /// Helper function for dealing with character attributes. Untested.
    /// </summary>
    private static int SetCharAttributeValue(int ncid, int varid, string name, NCType type, nint x, char[] value)
    {
        return NetCDFNative.nc_put_att_text(ncid, varid, name, value.Length, new string(value));
    }

    // TODO: replace with custom marshaler
    private static int SetStringAttributeValue(int ncid, int varid, string name, NCType type, nint x, string[] value)
    {
        return NetCDFNative.nc_put_att_string(ncid, varid, name, value);
    }

    private object ReadAttribute()
    {
        switch (DataType.ToNCType())
        {
            case NCType.NC_SHORT:
                return ReadAttributeValue<short>(NetCDFNative.nc_get_att_short);
            case NCType.NC_INT:
                return ReadAttributeValue<int>(NetCDFNative.nc_get_att_int);
            case NCType.NC_INT64:
                return ReadAttributeValue<long>(NetCDFNative.nc_get_att_longlong);

            case NCType.NC_USHORT:
                return ReadAttributeValue<ushort>(NetCDFNative.nc_get_att_ushort);
            case NCType.NC_UINT:
                return ReadAttributeValue<uint>(NetCDFNative.nc_get_att_uint);
            case NCType.NC_UINT64:
                return ReadAttributeValue<ulong>(NetCDFNative.nc_get_att_ulonglong);

            case NCType.NC_FLOAT:
                return ReadAttributeValue<float>(NetCDFNative.nc_get_att_float);
            case NCType.NC_DOUBLE:
                return ReadAttributeValue<double>(NetCDFNative.nc_get_att_double);

            case NCType.NC_BYTE:
                return ReadAttributeValue<sbyte>(NetCDFNative.nc_get_att_schar);
            case NCType.NC_UBYTE:
                return ReadAttributeValue<byte>(NetCDFNative.nc_get_att_uchar);
            case NCType.NC_CHAR:
                return ReadCharAttributeValue(ncid, varid, Name, Length);
            case NCType.NC_STRING:
                return ReadAttributeValue<string>(NetCDFNative.nc_get_att_string);
            default:
                throw new InvalidOperationException($"Unknown attribute type: {DataType.ToNCType().ToEnumString()}");
        }
    }

    private object ReadAttributeValue<T>(Func<int, int, string, T[], int> nativeFunc)
    {
        Log.Debug("Calling nc_get_att_{0}()...", typeof(T).ToFriendlyName());
        T[] data = new T[Length];
        int res = nativeFunc(ncid, varid, Name, data);
        CheckResult(res, "nc_get_att_{0}()", typeof(T).ToFriendlyName());
        Log.Debug("Successfully read {0}[] value of attribute {1}", typeof(T).ToFriendlyName(), Name);

        if (data.Length == 1 && data[0] != null)
            return data[0]!;

        return data;
    }

    private static string ReadCharAttributeValue(int ncid, int varid, string name, int length)
    {
        Log.Debug("Reading value of char attribute {0}", name);

        int res = NetCDFNative.nc_get_att_text(ncid, varid, name, out string? value, length);
        CheckResult(res, "Failed to read char attribute: {0}", name);

        Log.Debug("Successfully read value of char attribute {0}", name!);
        return value!;
    }
}
