
#include <mpi.h>

MPI_Comm mpibridge_MPI_COMM_WORLD() { return MPI_COMM_WORLD; }
MPI_Comm mpibridge_MPI_COMM_SELF() { return MPI_COMM_SELF; }
MPI_Comm mpibridge_MPI_COMM_NULL() { return MPI_COMM_NULL; }
MPI_Datatype mpibridge_MPI_CHAR() { return MPI_CHAR; }
MPI_Datatype mpibridge_MPI_SIGNED_CHAR() { return MPI_SIGNED_CHAR; }
MPI_Datatype mpibridge_MPI_UNSIGNED_CHAR() { return MPI_UNSIGNED_CHAR; }
MPI_Datatype mpibridge_MPI_BYTE() { return MPI_BYTE; }
MPI_Datatype mpibridge_MPI_WCHAR() { return MPI_WCHAR; }
MPI_Datatype mpibridge_MPI_SHORT() { return MPI_SHORT; }
MPI_Datatype mpibridge_MPI_UNSIGNED_SHORT() { return MPI_UNSIGNED_SHORT; }
MPI_Datatype mpibridge_MPI_INT() { return MPI_INT; }
MPI_Datatype mpibridge_MPI_UNSIGNED() { return MPI_UNSIGNED; }
MPI_Datatype mpibridge_MPI_LONG() { return MPI_LONG; }
MPI_Datatype mpibridge_MPI_UNSIGNED_LONG() { return MPI_UNSIGNED_LONG; }
MPI_Datatype mpibridge_MPI_FLOAT() { return MPI_FLOAT; }
MPI_Datatype mpibridge_MPI_DOUBLE() { return MPI_DOUBLE; }
MPI_Datatype mpibridge_MPI_LONG_DOUBLE() { return MPI_LONG_DOUBLE; }
MPI_Datatype mpibridge_MPI_LONG_LONG_INT() { return MPI_LONG_LONG_INT; }
MPI_Datatype mpibridge_MPI_UNSIGNED_LONG_LONG() { return MPI_UNSIGNED_LONG_LONG; }
MPI_Datatype mpibridge_MPI_LONG_LONG() { return MPI_LONG_LONG; }
MPI_Datatype mpibridge_MPI_PACKED() { return MPI_PACKED; }
MPI_Datatype mpibridge_MPI_DATATYPE_NULL() { return MPI_DATATYPE_NULL; }
MPI_Group mpibridge_MPI_GROUP_EMPTY() { return MPI_GROUP_EMPTY; }
MPI_Group mpibridge_MPI_GROUP_NULL() { return MPI_GROUP_NULL; }
MPI_Info mpibridge_MPI_INFO_NULL() { return MPI_INFO_NULL; }
MPI_Copy_function * mpibridge_MPI_NULL_COPY_FN() { return MPI_COMM_NULL_COPY_FN; }
MPI_Delete_function * mpibridge_MPI_NULL_DELETE_FN() { return MPI_COMM_NULL_DELETE_FN; }
MPI_Errhandler mpibridge_MPI_ERRORS_ARE_FATAL() { return MPI_ERRORS_ARE_FATAL; }
MPI_Errhandler mpibridge_MPI_ERRORS_RETURN() { return MPI_ERRORS_RETURN; }
MPI_Errhandler mpibridge_MPI_ERRHANDLER_NULL() { return MPI_ERRHANDLER_NULL; }
MPI_Op mpibridge_MPI_MAX() { return MPI_MAX; }
MPI_Op mpibridge_MPI_MIN() { return MPI_MIN; }
MPI_Op mpibridge_MPI_SUM() { return MPI_SUM; }
MPI_Op mpibridge_MPI_PROD() { return MPI_PROD; }
MPI_Op mpibridge_MPI_LAND() { return MPI_LAND; }
MPI_Op mpibridge_MPI_BAND() { return MPI_BAND; }
MPI_Op mpibridge_MPI_LOR() { return MPI_LOR; }
MPI_Op mpibridge_MPI_BOR() { return MPI_BOR; }
MPI_Op mpibridge_MPI_LXOR() { return MPI_LXOR; }
MPI_Op mpibridge_MPI_BXOR() { return MPI_BXOR; }
MPI_Op mpibridge_MPI_MINLOC() { return MPI_MINLOC; }
MPI_Op mpibridge_MPI_MAXLOC() { return MPI_MAXLOC; }
MPI_Op mpibridge_MPI_OP_NULL() { return MPI_OP_NULL; }
MPI_Request mpibridge_MPI_REQUEST_NULL() { return MPI_REQUEST_NULL; }
