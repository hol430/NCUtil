namespace NCUtil.Core.MPI.Native;

using MPI_Comm = IntPtr;
using MPI_Datatype = IntPtr;
using MPI_Group = IntPtr;
using MPI_Request = IntPtr;
using MPI_Op = IntPtr;
using MPI_Info = IntPtr;
using MPI_Errhandler = IntPtr;
using System.Runtime.InteropServices;

internal static class MpiBridge
{
    private const string library = "mpibridge";

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Comm mpibridge_MPI_COMM_WORLD();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Comm mpibridge_MPI_COMM_SELF();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Comm mpibridge_MPI_COMM_NULL();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Datatype mpibridge_MPI_CHAR();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Datatype mpibridge_MPI_SIGNED_CHAR();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Datatype mpibridge_MPI_UNSIGNED_CHAR();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Datatype mpibridge_MPI_BYTE();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Datatype mpibridge_MPI_WCHAR();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Datatype mpibridge_MPI_SHORT();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Datatype mpibridge_MPI_UNSIGNED_SHORT();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Datatype mpibridge_MPI_INT();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Datatype mpibridge_MPI_UNSIGNED();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Datatype mpibridge_MPI_LONG();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Datatype mpibridge_MPI_UNSIGNED_LONG();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Datatype mpibridge_MPI_FLOAT();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Datatype mpibridge_MPI_DOUBLE();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Datatype mpibridge_MPI_LONG_DOUBLE();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Datatype mpibridge_MPI_LONG_LONG_INT();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Datatype mpibridge_MPI_UNSIGNED_LONG_LONG();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Datatype mpibridge_MPI_LONG_LONG();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Datatype mpibridge_MPI_PACKED();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Datatype mpibridge_MPI_DATATYPE_NULL();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Group mpibridge_MPI_GROUP_EMPTY();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Group mpibridge_MPI_GROUP_NULL();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Info mpibridge_MPI_INFO_NULL();

    // [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    // MPI_Copy_function * mpibridge_MPI_NULL_COPY_FN();

    // [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    // MPI_Delete_function * mpibridge_MPI_NULL_DELETE_FN();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Errhandler mpibridge_MPI_ERRORS_ARE_FATAL();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Errhandler mpibridge_MPI_ERRORS_RETURN();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Errhandler mpibridge_MPI_ERRHANDLER_NULL();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Op mpibridge_MPI_MAX();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Op mpibridge_MPI_MIN();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Op mpibridge_MPI_SUM();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Op mpibridge_MPI_PROD();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Op mpibridge_MPI_LAND();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Op mpibridge_MPI_BAND();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Op mpibridge_MPI_LOR();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Op mpibridge_MPI_BOR();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Op mpibridge_MPI_LXOR();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Op mpibridge_MPI_BXOR();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Op mpibridge_MPI_MINLOC();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Op mpibridge_MPI_MAXLOC();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Op mpibridge_MPI_OP_NULL();

    [DllImport(library, CallingConvention = CallingConvention.Cdecl)]
    public static extern MPI_Request mpibridge_MPI_REQUEST_NULL();
}
