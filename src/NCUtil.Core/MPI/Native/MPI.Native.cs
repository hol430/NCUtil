using System.Runtime.InteropServices;

namespace NCUtil.Core.MPI.Native;

using MPI_Comm = IntPtr;
using MPI_Datatype = IntPtr;
using MPI_Group = IntPtr;
using MPI_Request = IntPtr;
using MPI_Op = IntPtr;
using MPI_Info = IntPtr;
using MPI_Errhandler = IntPtr;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct MPI_Status
{
    public int MPI_SOURCE;
    public int MPI_TAG;
    public int MPI_ERROR;
    internal int _cancelled;
    internal UIntPtr _ucount;
}

public static class MPI
{
    private const string library = "mpi";
    private const CallingConvention callConvention = CallingConvention.Cdecl;

    public const int MPI_ANY_SOURCE = -1;
    public const int MPI_ANY_TAG = -1;

    [DllImport(library, CallingConvention = callConvention)]
    public static extern unsafe int MPI_Init(ref int argc, byte *argv);

    [DllImport(library, CallingConvention = callConvention)]
    public static extern unsafe int MPI_Finalize();

    [DllImport(library, CallingConvention = callConvention)]
    public static extern unsafe int MPI_Initialized(out int flag);

    [DllImport(library, CallingConvention = callConvention)]
    public static extern unsafe int MPI_Finalized(out int flag);

    [DllImport(library, CallingConvention = callConvention)]
    public static extern unsafe int MPI_Abort(MPI_Comm comm, int errcode);

    [DllImport(library, CallingConvention = callConvention)]
    public static extern unsafe int MPI_Comm_size(MPI_Comm comm, out int size);

    [DllImport(library, CallingConvention = callConvention)]
    public static extern unsafe int MPI_Comm_rank(MPI_Comm comm, out int rank);

    [DllImport(library, CallingConvention = callConvention)]
    public static extern unsafe int MPI_Send(IntPtr buf, int count, MPI_Datatype datatype, int dest, int tag, MPI_Comm comm);

    [DllImport(library, CallingConvention = callConvention)]
    public static extern unsafe int MPI_Recv(IntPtr buf, int count, MPI_Datatype datatype, int source, int tag, MPI_Comm comm, out MPI_Status status);

    [DllImport(library, CallingConvention = callConvention)]
    public static extern unsafe int MPI_Test_cancelled(ref MPI_Status status, out int flag);

    [DllImport(library, CallingConvention = callConvention)]
    public static extern unsafe int MPI_Get_count(ref MPI_Status status, MPI_Datatype datatype, out int count);

    [DllImport(library, CallingConvention = callConvention)]
    public static extern int MPI_Barrier(MPI_Comm comm);
}
