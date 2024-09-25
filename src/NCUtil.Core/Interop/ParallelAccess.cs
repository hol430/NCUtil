namespace NCUtil.Core.Interop;

/// <summary>
/// The parallel access mode. When using collective access, certain functions
/// must be called by all MPI processes defined in the MPI communicator used in
/// nc_create_par() or nc_open_par(). When using independent access, these
/// functions may be called independently.
/// </summary>
public enum ParallelAccess : int
{
    /// <summary>
    /// Independent access.
    /// </summary>
    NC_INDEPENDENT = 0,

    /// <summary>
    /// Collective access.
    /// </summary>
    NC_COLLECTIVE = 1
}
