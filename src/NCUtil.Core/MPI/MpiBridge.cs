namespace NCUtil.Core.MPI;

using MPI_Comm = IntPtr;
using MPI_Datatype = IntPtr;
using MPI_Group = IntPtr;
using MPI_Request = IntPtr;
using MPI_Op = IntPtr;
using MPI_Info = IntPtr;
using MPI_Errhandler = IntPtr;

internal static class MpiBridge
{
    public static readonly MPI_Comm MPI_COMM_WORLD = Native.MpiBridge.mpibridge_MPI_COMM_WORLD();
    public static readonly MPI_Comm MPI_COMM_SELF = Native.MpiBridge.mpibridge_MPI_COMM_SELF();
    public static readonly MPI_Comm MPI_COMM_NULL = Native.MpiBridge.mpibridge_MPI_COMM_NULL();
    public static readonly MPI_Datatype MPI_CHAR = Native.MpiBridge.mpibridge_MPI_CHAR();
    public static readonly MPI_Datatype MPI_SIGNED_CHAR = Native.MpiBridge.mpibridge_MPI_SIGNED_CHAR();
    public static readonly MPI_Datatype MPI_UNSIGNED_CHAR = Native.MpiBridge.mpibridge_MPI_UNSIGNED_CHAR();
    public static readonly MPI_Datatype MPI_BYTE = Native.MpiBridge.mpibridge_MPI_BYTE();
    public static readonly MPI_Datatype MPI_WCHAR = Native.MpiBridge.mpibridge_MPI_WCHAR();
    public static readonly MPI_Datatype MPI_SHORT = Native.MpiBridge.mpibridge_MPI_SHORT();
    public static readonly MPI_Datatype MPI_UNSIGNED_SHORT = Native.MpiBridge.mpibridge_MPI_UNSIGNED_SHORT();
    public static readonly MPI_Datatype MPI_INT = Native.MpiBridge.mpibridge_MPI_INT();
    public static readonly MPI_Datatype MPI_UNSIGNED = Native.MpiBridge.mpibridge_MPI_UNSIGNED();
    public static readonly MPI_Datatype MPI_LONG = Native.MpiBridge.mpibridge_MPI_LONG();
    public static readonly MPI_Datatype MPI_UNSIGNED_LONG = Native.MpiBridge.mpibridge_MPI_UNSIGNED_LONG();
    public static readonly MPI_Datatype MPI_FLOAT = Native.MpiBridge.mpibridge_MPI_FLOAT();
    public static readonly MPI_Datatype MPI_DOUBLE = Native.MpiBridge.mpibridge_MPI_DOUBLE();
    public static readonly MPI_Datatype MPI_LONG_DOUBLE = Native.MpiBridge.mpibridge_MPI_LONG_DOUBLE();
    public static readonly MPI_Datatype MPI_LONG_LONG_INT = Native.MpiBridge.mpibridge_MPI_LONG_LONG_INT();
    public static readonly MPI_Datatype MPI_UNSIGNED_LONG_LONG = Native.MpiBridge.mpibridge_MPI_UNSIGNED_LONG_LONG();
    public static readonly MPI_Datatype MPI_LONG_LONG = Native.MpiBridge.mpibridge_MPI_LONG_LONG();
    public static readonly MPI_Datatype MPI_PACKED = Native.MpiBridge.mpibridge_MPI_PACKED();
    public static readonly MPI_Datatype MPI_DATATYPE_NULL = Native.MpiBridge.mpibridge_MPI_DATATYPE_NULL();
    public static readonly MPI_Group MPI_GROUP_EMPTY = Native.MpiBridge.mpibridge_MPI_GROUP_EMPTY();
    public static readonly MPI_Group MPI_GROUP_NULL = Native.MpiBridge.mpibridge_MPI_GROUP_NULL();
    public static readonly MPI_Info MPI_INFO_NULL = Native.MpiBridge.mpibridge_MPI_INFO_NULL();
    // public static readonly MPI_Copy_function e_MPI_NULL_COPY_FN = Native.MpiBridge.mpibridge_e_MPI_NULL_COPY_FN();
    // public static readonly MPI_Delete_function e_MPI_NULL_DELETE_FN = Native.MpiBridge.mpibridge_e_MPI_NULL_DELETE_FN();
    public static readonly MPI_Errhandler MPI_ERRORS_ARE_FATAL = Native.MpiBridge.mpibridge_MPI_ERRORS_ARE_FATAL();
    public static readonly MPI_Errhandler MPI_ERRORS_RETURN = Native.MpiBridge.mpibridge_MPI_ERRORS_RETURN();
    public static readonly MPI_Errhandler MPI_ERRHANDLER_NULL = Native.MpiBridge.mpibridge_MPI_ERRHANDLER_NULL();
    public static readonly MPI_Op MPI_MAX = Native.MpiBridge.mpibridge_MPI_MAX();
    public static readonly MPI_Op MPI_MIN = Native.MpiBridge.mpibridge_MPI_MIN();
    public static readonly MPI_Op MPI_SUM = Native.MpiBridge.mpibridge_MPI_SUM();
    public static readonly MPI_Op MPI_PROD = Native.MpiBridge.mpibridge_MPI_PROD();
    public static readonly MPI_Op MPI_LAND = Native.MpiBridge.mpibridge_MPI_LAND();
    public static readonly MPI_Op MPI_BAND = Native.MpiBridge.mpibridge_MPI_BAND();
    public static readonly MPI_Op MPI_LOR = Native.MpiBridge.mpibridge_MPI_LOR();
    public static readonly MPI_Op MPI_BOR = Native.MpiBridge.mpibridge_MPI_BOR();
    public static readonly MPI_Op MPI_LXOR = Native.MpiBridge.mpibridge_MPI_LXOR();
    public static readonly MPI_Op MPI_BXOR = Native.MpiBridge.mpibridge_MPI_BXOR();
    public static readonly MPI_Op MPI_MINLOC = Native.MpiBridge.mpibridge_MPI_MINLOC();
    public static readonly MPI_Op MPI_MAXLOC = Native.MpiBridge.mpibridge_MPI_MAXLOC();
    public static readonly MPI_Op MPI_OP_NULL = Native.MpiBridge.mpibridge_MPI_OP_NULL();
    public static readonly MPI_Request MPI_REQUEST_NULL = Native.MpiBridge.mpibridge_MPI_REQUEST_NULL();
}
