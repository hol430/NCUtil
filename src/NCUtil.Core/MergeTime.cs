using NCUtil.Core.Configuration;
using NCUtil.Core.Logging;
using NCUtil.Core.Extensions;
using NCUtil.Core.Models;
using System.Reflection;
using Attribute = NCUtil.Core.Models.Attribute;
using Range = NCUtil.Core.Models.Range;
using NCUtil.Core.IO;
using NCUtil.Core.MPI;
using NCUtil.Core.Interop;

namespace NCUtil.Core;

public class MergeTime
{
    private readonly Options options;
    private readonly DateTime startTime;

    public MergeTime(Options options)
    {
        this.options = options;

        if (options.UseMpi)
        {
            if (Mpi.MPI_Initialized())
                Log.Warning("MPI appears to already be initialised. This is probably a programming error!");
            else
                Mpi.MPI_Init();
        }

        startTime = DateTime.Now;
        Log.ConfigureLogging((LogLevel)options.Verbosity, options.ShowProgress, options.ProgressInterval, options.UseMpi);

        if (options.UseMpi)
        {
            int size = Mpi.MPI_Comm_size(MpiBridge.MPI_COMM_WORLD);
            Log.Diagnostic("Successfully initialised MPI environment. World size is {0}", size);
        }
    }

    public void Run()
    {
        try
        {
            RunInternal();
        }
        catch (Exception error)
        {
            Log.Error(error.ToString());
            if (options.UseMpi)
                Mpi.MPI_Abort(MpiBridge.MPI_COMM_WORLD, 1);
        }
    }

    private void RunInternal()
    {
        DateTime startTime = DateTime.Now;

        Log.Information("Mergetime started");

        // Basic sanity checking.
        // TODO: refactor out the need for a restart file.
        if (options.WalltimeLimit != null && options.RestartFile == null)
            throw new Exception($"Walltime limit is set but no restart file is provided. This is probably a mistake - halting the job at walltime limit without writing a restart file leaves you with no way of resuming the job later.");
        if (options.WalltimeLimit == null && options.RestartFile != null)
            throw new Exception($"Restart file is set but no walltime limit is set. This is probably a mistake - the restart file will only be used if the walltimie limit is reached.");
        if (!options.InputFiles.Any())
            throw new Exception("No input files were given");

        string outFile = options.OutputFile;
        if (options.WorkingDirectory != null)
        {
            if (!Directory.Exists(options.WorkingDirectory))
                Directory.CreateDirectory(options.WorkingDirectory);

            outFile = Path.Join(options.WorkingDirectory, Path.GetFileName(outFile));
            Log.Diagnostic("Intermediate output file will be used: '{0}'", outFile);
        }

        // Read restart file.
        IEnumerable<string> mergedFiles = ReadRestartFile();
        Log.Diagnostic("{0} files have already been processed", mergedFiles.Count());

        if (IsMaster())
        {
            Log.Information("Performing one-time initialisation...");

            // Copy existing output file to working directory.
            if (options.RestartFile != null && options.WorkingDirectory != null && !File.Exists(outFile) && File.Exists(options.OutputFile))
            {
                Log.Diagnostic("Copying existing output file into working directory");
                File.Copy(options.OutputFile, outFile);
            }
            else
            {
                Log.Diagnostic("No need to copy existing file.");
            }

            // If not restarting, delete an existing output file.
            if (options.RestartFile == null && File.Exists(outFile))
            {
                Log.Diagnostic("Deleting existing output file: '{0}'", outFile);
                File.Delete(outFile);
            }
            else
                Log.Diagnostic("No need to delete output file. Either it doesn't exist or we are resuming a previous mergetime operation.");
        }

        (int ntime, IDictionary<string, int> offsets) = CountTimesteps();

        if (options.RestartFile == null || !File.Exists(outFile))
            InitialiseOutputFile(outFile, ntime);
        else
            Log.Information("Output file will not be initialised because it already exists.");

        if (options.UseMpi)
        {
            Log.Diagnostic("Waiting for master to initialise the output file...");
            Mpi.MPI_Barrier(MpiBridge.MPI_COMM_WORLD);
            Log.Diagnostic("Master node has successfully initialised the output file. Continuing...");
        }

        double start = 0.0;
        long totalSize = options.InputFiles.Select(i => new FileInfo(i).Length).Sum();
        IEnumerable<string> inputFiles = GetInputFiles(mergedFiles);
        using NetCDFFile ncOut = new NetCDFFile(outFile, NetCDFFileMode.Append, options.UseMpi);
        Log.Diagnostic("Processing {0} files", inputFiles.Count());
        foreach (string inputFile in inputFiles)
        {
            double step = (double)new FileInfo(inputFile).Length / totalSize;
            CopyData(inputFile, ncOut, offsets[inputFile], p => Log.Progress(start + step * p));
            start += step;
        }

        // Move intermediate output file into output directory.
        if (options.WorkingDirectory != null)
        {
            Log.Information("Moving intermediate output file {0} to output location: {1}", outFile, options.OutputFile);
            File.Move(outFile, options.OutputFile, true);
        }

        DateTime finishTime = DateTime.Now;
        TimeSpan duration = finishTime - startTime;
        Log.Information($"Mergetime completed in {duration.ToReadableString()}");
    }

    private (int ntime, IDictionary<string, int> offsets) CountTimesteps()
    {
        Log.Diagnostic("Opening input files to count total number of timesteps.");
        IDictionary<string, int> offsets = new Dictionary<string, int>();
        int ntime = 0;
        foreach (string inputFile in options.InputFiles)
        {
            offsets[inputFile] = ntime;
            ntime += GetNTime(inputFile);
        }
        Log.Diagnostic("Input files contain {0} timesteps", ntime);
        return (ntime, offsets);
    }

    /// <summary>
    /// Count the number of timesteps in the specified netcdf file.
    /// </summary>
    private int GetNTime(string file)
    {
        using NetCDFFile nc = new NetCDFFile(file, NetCDFFileMode.Read, options.UseMpi);
        return nc.GetNTime();
    }

    private void InitialiseOutputFile(string outFile, int ntime)
    {
        if (options.UseMpi && !IsMaster())
        {
            Log.Diagnostic("This node is not the master and will therefore not initialise the output file.");
            return;
        }

        Log.Information("Initialising output file");

        // Parse chunk sizes from user options. Doing this early so we can throw
        // early in case of parser error.
        ChunkSizes chunkSizes = new ChunkSizes(options.ChunkSizes);

        using NetCDFFile ncOut = new NetCDFFile(outFile, NetCDFFileMode.Write);
        using NetCDFFile ncIn = new NetCDFFile(options.InputFiles.First());
        Log.Diagnostic("Creating dimensions in output file");
        foreach (Dimension dim in ncIn.Dimensions)
        {
            int size = dim.IsTime() ? ntime : dim.Size;
            ncOut.CreateDimension(dim.Name, size);
        }

        // Create all variables in output file (but don't fill them with data).
        Log.Diagnostic("Creating variables in output file");
        foreach (Variable variable in ncIn.Variables)
        {
            // Create variable.

            // Set compression.
            // TODO: custom compression type.
            // Compression level of -1 means same as input file.
            // Compression level of 0 means no compression.
            // This should be refactored. It could be done better.
            ICompressionAlgorithm? compression = null;

            if (options.CompressionLevel > 0)
            {
                compression = new ZLibCompression(true, options.CompressionLevel);
                Log.Diagnostic("Variable {0}: Using user-defined compression: {1}",
                    variable.Name,
                    compression);
            }
            else if (options.CompressionLevel == -1 && variable.Compression != null)
            {
                Log.Diagnostic("Variable {0}: Enabling compression: {1} (same as input file)",
                    variable.Name,
                    variable.Compression);

                compression = variable.Compression;
            }

            Variable varOut = ncOut.CreateVariable(variable.Name, variable.Dimensions, variable.DataType, chunkSizes, options.AllowCompact, compression);
            foreach (Attribute attribute in variable.Attributes)
                varOut.CreateAttribute(attribute.Name, attribute.DataType, attribute.Value);
        }

        // Copy file-level metadata.
        ncIn.CopyMetadataTo(ncOut);

        Dimension time = ncIn.GetTimeDimension();

        // TODO: Custom dimension order.

        // Copy all non-time dimensions to the output file.
        Log.Diagnostic("Copying non-time coordinate variables to output file.");
        foreach (Dimension dimension in ncIn.Dimensions)
        {
            if (dimension.IsTime())
                continue;

            // Copy the contents of this variable from the input file to the
            // output file. Here we assume that the variable name matches the
            // dimension name.
            ncIn.Append(ncOut, dimension.Name, time.Name, options.MinChunkSize, 0, _ => {});
        }

        Log.Information("Output file has been successfully initialised.");
    }

    public int CopyData(string inputFile, NetCDFFile ncOut, int offset, Action<double> progressReporter)
    {
        // This is the only worker reading from this input file, so it can be
        // opened in serial mode.
        using NetCDFFile ncIn = new NetCDFFile(inputFile);

        IReadOnlyList<string> dimensions = ncIn.Dimensions.Select(d => d.Name).ToList();

        Variable varTime = ncIn.GetTimeVariable();
        Dimension dimTime = ncIn.GetTimeDimension();

        ncIn.Append(ncOut, varTime.Name, dimTime.Name, options.MinChunkSize, offset, _ => {});

        double start = 0;
        long totalWeight = ncIn.Variables.Where(v => !v.Dimensions.Contains(v.Name)).Sum(v => v.GetLength());
        foreach (Variable variable in ncIn.Variables)
        {
            if (dimensions.Contains(variable.Name))
                continue;

            double step = (double)variable.GetLength() / totalWeight;
            ncIn.Append(ncOut, variable.Name, dimTime.Name, options.MinChunkSize, offset, p => progressReporter(start + step * p));
            start += step;
        }

        return offset + dimTime.Size;
    }

    /// <summary>
    /// Return true if MPI is disabled or if MPI is enabled and this is the
    /// master node. In other words, check if this node should do things that
    /// should only be done once (by the master node).
    /// </summary>
    private bool IsMaster()
    {
        if (!options.UseMpi)
            return true;

        // In MPI mode, only the master node should initialise the output file.
        int rank = Mpi.MPI_Comm_rank(MpiBridge.MPI_COMM_WORLD);
        return rank == 0;
    }

    private IEnumerable<string> GetInputFiles(IEnumerable<string> skip)
    {
        IEnumerable<string> inputFiles = options.InputFiles.Except(skip);

        if (!options.UseMpi)
            return inputFiles;

        // Determine the world size (aka total number of workers).
        int worldSize = Mpi.MPI_Comm_size(MpiBridge.MPI_COMM_WORLD);

        // Split the input files into equally-sized chunks.
        int nfile = inputFiles.Count();
        List<string[]> chunks = inputFiles.Chunk(nfile / worldSize).ToList();

        // Get the rank of this worker.
        int rank = Mpi.MPI_Comm_rank(MpiBridge.MPI_COMM_WORLD);

        // Return the chunk for this worker (if any).
        if (chunks.Count > rank)
            return chunks[rank];

        // This can happen if the number of files exceeds the number of workers.
        return Enumerable.Empty<string>();
    }

    private IEnumerable<string> ReadRestartFile()
    {
        if (options.RestartFile == null || !File.Exists(options.RestartFile))
            return Enumerable.Empty<string>();
        return File.ReadAllLines(options.RestartFile);
    }
}
