using NCUtil.Core.Models;

namespace NCUtil.Core.IO;

public static class AppendTime
{
	/// <summary>
	/// Copy the specified variable from the input file to the output file,
	/// appending it along the time axis.
	/// </summary>
	/// <param name="inputFile">The input file.</param>
	/// <param name="outputFile">The output file.</param>
	/// <param name="variable">Name of the variable to be copied.</param>
	/// <param name="progress">Progress reporting function.</param>
	public static void AppendTimeVariable(NetCDFFile inputFile, NetCDFFile outputFile, string variable, Action<double> progress)
	{
		throw new NotImplementedException();
	}
}
