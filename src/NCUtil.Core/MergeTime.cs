using NCUtil.Core.Configuration;
using NCUtil.Core.Logging;
using NCUtil.Core.Extensions;
using NCUtil.Core.Models;
using System.Reflection;
using Attribute = NCUtil.Core.Models.Attribute;
using Range = NCUtil.Core.Models.Range;
using NCUtil.Core.IO;

namespace NCUtil.Core;

public class MergeTime
{
    private readonly Options options;
    private readonly DateTime startTime;

    public MergeTime(Options options)
    {
        this.options = options;
        startTime = DateTime.Now;
        Log.ConfigureLogging((LogLevel)options.Verbosity, options.ShowProgress, options.ProgressInterval);
    }

    public void Run()
    {
        Log.Information("Running mergetime");

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

        // Copy existing output file to working directory.
        if (options.RestartFile != null && options.WorkingDirectory != null && !File.Exists(outFile) && File.Exists(options.OutputFile))
        {
            Log.Diagnostic("Copying existing output file into working directory");
            File.Copy(options.OutputFile, outFile);
        }

        // If not restarting, delete an existing output file.
        if (options.RestartFile == null && File.Exists(outFile))
        {
            Log.Diagnostic("Deleting existing output file: '{0}'", outFile);
            File.Delete(outFile);
        }

        if (!File.Exists(outFile))
            InitialiseOutputFile(outFile);

        double start = 0.0;
        long totalSize = options.InputFiles.Select(i => new FileInfo(i).Length).Sum();
        int offset = 0;
        foreach (string inputFile in options.InputFiles)
        {
            double step = (double)new FileInfo(inputFile).Length / totalSize;
            offset += CopyData(inputFile, outFile, offset, p => Log.Progress(start + step * p));
            start += step;
        }

        // Move intermediate output file into output directory.
        if (options.WorkingDirectory != null)
        {
            Log.Information("Moving intermediate output file {0} to output location: {1}", outFile, options.OutputFile);
            File.Move(outFile, options.OutputFile, true);
        }
    }

    /// <summary>
    /// Count the number of timesteps in the specified netcdf file.
    /// </summary>
    private static int GetNTime(string file)
    {
        using NetCDFFile nc = new NetCDFFile(file, NetCDFFileMode.Read);
        return nc.GetNTime();
    }

    private void InitialiseOutputFile(string outFile)
    {
        // Parse chunk sizes from user options. Doing this early so we can throw
        // early in case of parser error.
        ChunkSizes chunkSizes = new ChunkSizes(options.ChunkSizes);

        using NetCDFFile ncOut = new NetCDFFile(outFile, NetCDFFileMode.Write);
        Log.Information("Initialising output file");

        Log.Diagnostic("Opening input files to count total number of timesteps.");
        int ntime = options.InputFiles.Sum(GetNTime);
        Log.Diagnostic("Input files contain {0} timesteps", ntime);

        using NetCDFFile ncIn = new NetCDFFile(options.InputFiles.First());
        Log.Diagnostic("Creating dimensions in output file");
        foreach (Dimension dim in ncIn.GetDimensions())
        {
            int size = dim.IsTime() ? ntime : dim.Size;
            ncOut.AddDimension(dim.Name, size);
        }

        // Create all variables in output file (but don't fill them with data).
        Log.Diagnostic("Creating variables in output file");
        foreach (Variable variable in ncIn.GetVariables())
        {
            // Create variable.
            ncOut.AddVariable(variable, chunkSizes, options.AllowCompact, options.CompressionLevel);
        }

        // Copy file-level metadata.
        ncIn.CopyMetadataTo(ncOut);

        Dimension time = ncIn.GetTimeDimension();

        // Copy all non-time dimensions to the output file.
        Log.Diagnostic("Copying non-time coordinate variables to output file.");
        foreach (Dimension dimension in ncIn.GetDimensions())
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

    public int CopyData(string inputFile, string outputFile, int offset, Action<double> progressReporter)
    {
        using NetCDFFile ncIn = new NetCDFFile(inputFile);
        using NetCDFFile ncOut = new NetCDFFile(outputFile, NetCDFFileMode.Append);

        IReadOnlyList<string> dimensions = ncIn.GetDimensions().Select(d => d.Name).ToList();
        IReadOnlyList<Variable> variables = ncIn.GetVariables();

        Variable varTime = ncIn.GetTimeVariable();
        Dimension dimTime = ncIn.GetTimeDimension();

        ncIn.Append(ncOut, varTime.Name, dimTime.Name, options.MinChunkSize, offset, _ => {});

        double start = 0;
        long totalWeight = variables.Where(v => !v.Dimensions.Contains(v.Name)).Sum(v => v.GetLength());
        foreach (Variable variable in variables)
        {
            if (dimensions.Contains(variable.Name))
                continue;

            double step = (double)variable.GetLength() / totalWeight;
            ncIn.Append(ncOut, variable.Name, dimTime.Name, options.MinChunkSize, offset, p => progressReporter(start + step * p));
            start += step;
        }

        return offset + dimTime.Size;
    }

    private IEnumerable<string> ReadRestartFile()
    {
        if (options.RestartFile == null || !File.Exists(options.RestartFile))
            return Enumerable.Empty<string>();
        return File.ReadAllLines(options.RestartFile);
    }
}
