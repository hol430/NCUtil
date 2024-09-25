using NCUtil.Core.Interop;

namespace NCUtil.Core.MPI;

using MPI_Comm = IntPtr;
using MPI_Datatype = IntPtr;
using MPI_Group = IntPtr;
using MPI_Request = IntPtr;
using MPI_Op = IntPtr;
using MPI_Info = IntPtr;
using MPI_Errhandler = IntPtr;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text;

public static class Mpi
{
    /// <summary>
    /// Call MPI_Init(), passing no arguments. The MPI spec states that these
    /// arguments are neither interpreted nor modified anyway.
    /// </summary>
    public static unsafe void MPI_Init()
    {
        byte[] buffer = new byte[0];
        int argc = 0;

        fixed (byte* buf = buffer)
        {
            int result = Native.MPI.MPI_Init(ref argc, buf);
            CheckResult(result);
        }
    }

    public static unsafe void MPI_Init(ref int argc, ref string[]argv)
    {
        (byte[] buffer, uint[] offsets) = StringHandling.WriteStrings(argv, (byte)' ');

        fixed (byte* buf = buffer)
        {
            int result = Native.MPI.MPI_Init(ref argc, buf);
            CheckResult(result);
        }
    }

    public static void MPI_Finalize()
    {
        int result = Native.MPI.MPI_Finalize();
        CheckResult(result);
    }

    public static bool MPI_Initialized()
    {
        int result = Native.MPI.MPI_Initialized(out int flag);
        CheckResult(result);
        return flag == 1;
    }

    public static bool MPI_Finalized()
    {
        int result = Native.MPI.MPI_Finalized(out int flag);
        CheckResult(result);
        return result == 1;
    }

    public static void MPI_Abort(MPI_Comm comm, int errcode)
    {
        int result = Native.MPI.MPI_Abort(comm, errcode);
        CheckResult(result);
    }

    public static int MPI_Comm_size(MPI_Comm comm)
    {
        int result = Native.MPI.MPI_Comm_size(comm, out int size);
        CheckResult(result);
        return size;
    }

    public static int MPI_Comm_rank(MPI_Comm comm)
    {
        int result = Native.MPI.MPI_Comm_rank(comm, out int rank);
        CheckResult(result);
        return rank;
    }

    public static unsafe void MPI_Send(IntPtr buf, int count, MPI_Datatype datatype, int dest, int tag, MPI_Comm comm)
    {
        int result = Native.MPI.MPI_Send(buf, count, datatype, dest, tag, comm);
        CheckResult(result);
    }

    public static unsafe void MPI_Recv(IntPtr buf, int count, MPI_Datatype datatype, int source, int tag, MPI_Comm comm, out Native.MPI_Status status)
    {
        int result = Native.MPI.MPI_Recv(buf, count, datatype, source, tag, comm, out status);
        CheckResult(result);
    }

    public static unsafe bool MPI_Test_cancelled(ref Native.MPI_Status status)
    {
        int result = Native.MPI.MPI_Test_cancelled(ref status, out int flag);
        CheckResult(result);
        return flag == 1;
    }

    public static unsafe int MPI_Get_count(ref Native.MPI_Status status, MPI_Datatype datatype)
    {
        int result = Native.MPI.MPI_Get_count(ref status, datatype, out int count);
        CheckResult(result);
        return count;
    }

    public static void MPI_Barrier(MPI_Comm comm)
    {
        int result = Native.MPI.MPI_Barrier(comm);
        CheckResult(result);
    }

    // [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CheckResult(int result)
    {
        if (result != 0)
        {
            string func = new StackFrame(1, true).GetMethod()?.Name ?? "<caller unknown>";
            throw new Exception($"MPI returned non-zero result for function {func}");
        }
    }
}
