namespace NCUtil.Core.Configuration;

using CommandLine;
using NCUtil.Core.Logging;

[Verb("mergetime", HelpText = "Merge two or more .nc files along the time axis")]
public class Options
{
    public const int ExitCodeWalltimeLimit = 42;

    private readonly int verbosity;
    private readonly bool showProgress;
    private readonly int progressInterval;
    private readonly int minChunkSize;
    private readonly string? units;
    private readonly string? workingDirectory;
    private readonly TimeSpan? walltimeLimit;
    private readonly string? restartFile;
    private readonly IEnumerable<string> chunkSizes;
    private readonly int compressionLevel;
    private readonly bool allowCompact;
    private readonly string outputFile;
    private readonly IEnumerable<string> inputFiles;

    [Option('v', "verbosity", Default = 2, HelpText = "Logging verbosity (0-4)")]
    public int Verbosity => verbosity;

    [Option('p', "show-progress", Default = false, HelpText = "Enable progress reporting")]
    public bool ShowProgress => showProgress;

    [Option("progress-interval", Default = 1, HelpText = "Minimum interval between progress reports in seconds. This has no effect if progress reporting is disabled (default: 1)")]
    public int ProgressInterval => progressInterval;

    [Option("min-chunk-size", Default = 1, HelpText = "Number of chunks to read at a time (along each dimension) when copying data. If 1, the chunk size of the input data will be used. This does not affect the chunking of the variables in the output file. Higher values result in higher throughput at the cost of higher memory usage")]
    public int MinChunkSize => minChunkSize;

    [Option('u', "units", Default = null, HelpText = "Output units (optional) (default: same as input units)")]
    public string? Units => units;

    [Option('w', "work-dir", Default = null, HelpText = "Working directory. Input files will be copied to this location before being read. Output file will be written in this location before being moved to the ultimate output path specified by the -o/--out-file parameter.")]
    public string? WorkingDirectory => workingDirectory;

    [Option('W', "walltime-limit", Default = null, HelpText = "Walltime limit. If this limit is reached, the job will terminate (with exit code 42) and may be resumed later by re-running the command with the same restart file. If the job is running in PBS, it will be resubmitted via the `qrerun` command. This requires that the job is restartable (ie submitted with -ry).")]
    public TimeSpan? WalltimeLimit => walltimeLimit;

    // TODO: implement this without a restart file
    [Option('r', "restart-file", Default = null, HelpText = "Path to the restart file to be used. This allows execution to resume from a previous run of ncmergetime. This requires that the same arguments are used in all invocations of the command.")]
    public string? RestartFile => restartFile;

    [Option('c', "chunk-sizes", Separator = ',', Default = null, HelpText = "Chunk sizes for each dimension. This is optional, and if omitted, the values in the input files will be kept. This should be specified in the same format as for nco. E.g. lat/1,lon/1,time/365")]
    public IEnumerable<string> ChunkSizes => chunkSizes;

    [Option('C', "compression-level", Default = -1, HelpText = "Compression level 0-9. 0 means no compression. 9 means highest compression ratio but slowest performance. Default behaviour is to use the same compression level as in the input files.")]
    public int CompressionLevel => compressionLevel;

    [Option("allow-compact", Default = false, HelpText = "Allow compact packing of variables small enough to allow it and which don't have explicit chunk sizes specified for all of their dimensions")]
    public bool AllowCompact => allowCompact;

    [Option('o', "out-file", Required = true, HelpText = "Path to the output file.")]
    public string OutputFile => outputFile;

    [Value(0)]
    public IEnumerable<string> InputFiles => inputFiles;

    public Options(int verbosity, bool showProgress, int progressInterval
        , int minChunkSize, string? units, string? workingDirectory
        , TimeSpan? walltimeLimit, string? restartFile
        , IEnumerable<string> chunkSizes, int compressionLevel
        , bool allowCompact, string outputFile, IEnumerable<string> inputFiles)
    {
        this.verbosity = verbosity;
        this.showProgress = showProgress;
        this.progressInterval = progressInterval;
        this.minChunkSize = minChunkSize;
        this.units = units;
        this.workingDirectory = workingDirectory;
        this.walltimeLimit = walltimeLimit;
        this.restartFile = restartFile;
        this.chunkSizes = chunkSizes;
        this.compressionLevel = compressionLevel;
        this.allowCompact = allowCompact;
        this.outputFile = outputFile;
        this.inputFiles = inputFiles;
    }
}
