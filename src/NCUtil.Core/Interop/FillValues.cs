namespace NCUtil.Core.Interop;

/// <summary>
///	Default fill values, used unless _FillValue attribute is set.
/// These values are stuffed into newly allocated space as appropriate.
/// The hope is that one might use these to notice that a particular datum
/// has not been set.
/// </summary>
// #define NC_FILL_BYTE    ((signed char)-127)
// #define NC_FILL_CHAR    ((char)0)
// #define NC_FILL_SHORT   ((short)-32767)
// #define NC_FILL_INT     (-2147483647)
// #define NC_FILL_FLOAT   (9.9692099683868690e+36f) /* near 15 * 2^119 */
// #define NC_FILL_DOUBLE  (9.9692099683868690e+36)
// #define NC_FILL_UBYTE   (255)
// #define NC_FILL_USHORT  (65535)
// #define NC_FILL_UINT    (4294967295U)
// #define NC_FILL_INT64   ((long long)-9223372036854775806LL)
// #define NC_FILL_UINT64  ((unsigned long long)18446744073709551614ULL)
// #define NC_FILL_STRING  ((char *)"")
public static class FillValues
{
    public const sbyte NC_FILL_BYTE = -127;
    public const char NC_FILL_CHAR = (char)0;
    public const short NC_FILL_SHORT = -32767;
    public const int NC_FILL_INT = -2147483647;
    public const float NC_FILL_FLOAT = 9.96921E+36f;    /* near 15 * 2^119 */
    public const double NC_FILL_DOUBLE = 9.969209968386869E+36;
    public const byte NC_FILL_UBYTE = 255;
    public const ushort NC_FILL_USHORT = 65535;
    public const uint NC_FILL_UINT = 4294967295U;
    public const long NC_FILL_INT64 = -9223372036854775806L;
    public const ulong NC_FILL_UINT64 = 18446744073709551614U;
    public const string NC_FILL_STRING = "";
}
