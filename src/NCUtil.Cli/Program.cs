using CommandLine;
using NCUtil.Core;
using NCUtil.Core.Configuration;

int exitCode = 0;

try
{
    // Parse CLI args.
    ParserResult<Options> result = Parser.Default.ParseArguments<Options>(args);
    result.WithParsed(opts => new MergeTime(opts).Run());
    result.WithNotParsed(_ => exitCode = 1);
}
catch (Exception error)
{
    Console.Error.WriteLine(error.ToString());
    exitCode = 1;
}
return exitCode;
