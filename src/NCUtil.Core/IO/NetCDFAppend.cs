using NCUtil.Core.Extensions;
using NCUtil.Core.Logging;
using NCUtil.Core.Models;

namespace NCUtil.Core.IO;

/// <summary>
/// Functions for appending data to a netcdf file along a specified axis.
/// </summary>
public static class NetCDFAppend
{
    /// <summary>
    /// Append the contents of the variable in the input file to the variable in
    /// the output file, appending along the specified dimension.
    /// </summary>
    /// <param name="ncIn">Input file.</param>
    /// <param name="ncOut">Output file.</param>
    /// <param name="variableName">Name of the variable to be copied.</param>
    /// <param name="dimensionName">The dimension along which data will be appended.</param>
    /// <param name="minChunkSize">Minimum multiple of input file chunk sizes in which data will be read.
    /// <param name="offset">The offset along the specified dimension.</param>
    /// <param name="progressReporter">Progress reporting function.</param>
    public static void Append(this NetCDFFile ncIn, NetCDFFile ncOut, string variableName, string dimensionName, int minChunkSize, int offset, Action<double> progressReporter)
    {
        Log.Diagnostic("Appending variable {0} along {1} axis to output file", variableName, dimensionName);

        Variable varIn = ncIn.GetVariable(variableName);
        Variable varOut = ncOut.GetVariable(variableName);

        if (varIn.Dimensions.Count != varOut.Dimensions.Count)
            throw new InvalidOperationException($"Unable to copy variable {variableName}: number of dimensions in input file ({varIn.Dimensions.Count}) doesn't match the number of dimensions in the output file ({varOut.Dimensions.Count})");

        if (varIn.Dimensions.Count == 1)
            Append1D(ncIn, ncOut, variableName, dimensionName, minChunkSize, offset, progressReporter);
        else if (varIn.Dimensions.Count == 2)
            Append2D(ncIn, ncOut, variableName, dimensionName, minChunkSize, offset, progressReporter);
        else if (varIn.Dimensions.Count == 3)
            Append3D(ncIn, ncOut, variableName, dimensionName, minChunkSize, offset, progressReporter);
        else if (varIn.Dimensions.Count != 0)
            throw new NotImplementedException($"Appending along more than 4 dimensions is not yet implemented (but could theoretically be done).");
    }

    private static void Append1D(NetCDFFile ncIn, NetCDFFile ncOut, string variableName, string dimensionName, int minChunkSize, int offset, Action<double> progressReporter)
    {
        Variable varIn = ncIn.GetVariable(variableName);
        Variable varOut = ncOut.GetVariable(variableName);

        Dimension dimension = ncIn.GetDimension(varIn.Dimensions[0]);

        string unitsIn = varIn.GetUnits();
        string unitsOut = varOut.GetUnits();

        if (unitsIn != unitsOut)
            throw new NotImplementedException("TBI: units conversion");

        // The chunk sizes to be used for reading data.
        int chunkSize = GetChunkSize(varIn.ChunkSizes[0], minChunkSize);

        // Check if we are appending or copying.
        bool hasOffset = dimension.Name == dimensionName;
        if (!hasOffset)
            offset = 0;

        // Create an array to hold each chunk of outputs.
        Array chunk = Array.CreateInstance(varIn.DataType, chunkSize);

        // The number of elements that have been read.
        MutableRange rangeIn = new MutableRange();
        MutableRange rangeOut = new MutableRange();

        for (int i = 0; i < dimension.Size; i += chunkSize)
        {
            // Don't allow reading more values along this dimension than exist
            // along this dimension.
            rangeIn.Count = Math.Min(rangeIn.Start + chunkSize, dimension.Size);

            // Read a chunk of data from input file.
            varIn.Read(chunk, rangeIn);

            // TODO: implement units conversion.
            // TODO: different dimension order.

            // The output hyperslab may have an offset if this 1D variable is
            // the coordinate variable for the dimension along which we're
            // appending (ie if it's the time variable and we're appending along
            // the time axis).
            rangeOut.Start = rangeIn.Start + offset;

            // The input range count may be smaller on the final iteration.
            rangeOut.Count = rangeIn.Count;

            // Write data to the output file.
            varOut.Write(chunk, rangeOut);

            // Progress reporting.
            progressReporter(1.0 * (rangeIn.Start + rangeIn.Count) / dimension.Size);
        }
    }

    private static void Append2D(NetCDFFile ncIn, NetCDFFile ncOut, string variableName, string dimensionName, int minChunkSize, int offset, Action<double> progressReporter)
    {
        Variable varIn = ncIn.GetVariable(variableName);
        Variable varOut = ncOut.GetVariable(variableName);

        IReadOnlyList<Dimension> dimensions = varIn.Dimensions.ToArray(d => ncIn.GetDimension(d));

        string unitsIn = varIn.GetUnits();
        string unitsOut = varOut.GetUnits();

        if (unitsIn != unitsOut)
            throw new NotImplementedException("TBI: units conversion");

        // The size of each dimension in the input file.
        int[] dimensionSizes = dimensions.ToArray(d => d.Size);

        // The chunk sizes to be used for reading data.
        int[] chunkSizes = varIn.ChunkSizes.ToArray(c => GetChunkSize(c, minChunkSize));

        // Total number of iterations per dimension.
        int[] niter = chunkSizes.Zip(dimensionSizes, (c, s) => (int)Math.Ceiling(1.0 * s / c)).ToArray();

        // Total number of iterations across all dimensions.
        long itMax = niter.Product();

        // Check if we are appending or copying.
        int offsetIndex = dimensions.IndexOf(d => d.Name == dimensionName);
        bool hasOffset = offsetIndex >= 0;

        int offseti = offsetIndex == 0 ? offset : 0;
        int offsetj = offsetIndex == 1 ? offset : 0;

        // Create an array to hold each chunk of outputs.
        Array chunk = Array.CreateInstance(varIn.DataType, chunkSizes.Product());

        // The number of elements that have been read.
        MutableRange iread = new MutableRange();
        MutableRange iwrite = new MutableRange();
        MutableRange jread = new MutableRange();
        MutableRange jwrite = new MutableRange();

        long it = 0;

        for (int i = 0; i < niter[0]; i++)
        {
            // Start index for this iteration on the i-th dimension.
            int ilow = i * chunkSizes[0];

            // Don't allow more values to be read along this dimension than
            // exist along this dimension.
            int ihigh = Math.Min(dimensionSizes[0], ilow + chunkSizes[0]);

            iread.Start = ilow;
            iread.Count = ihigh - ilow;

            iwrite.Start = ilow + offseti;
            iwrite.Count = iread.Count;

            for (int j = 0; j < niter[1]; j++)
            {
                // Start index for this iteration on the j-th dimension.
                int jlow = j * chunkSizes[1];

                // Don't allow more values to be read along this dimension than
                // exist along this dimension.
                int jhigh = Math.Min(dimensionSizes[1], jlow + chunkSizes[1]);

                jread.Start = jlow;
                jread.Count = jhigh - jlow;

                jwrite.Start = jlow + offsetj;
                jwrite.Count = jread.Count;

                varIn.Read(chunk, iread, jread);

                // Write data to the output file.
                varOut.Write(chunk, iwrite, jwrite);

                // Progress reporting.
                it++;
                progressReporter(1.0 * it / itMax);
            }
        }
    }

    private static void Append3D(NetCDFFile ncIn, NetCDFFile ncOut, string variableName, string dimensionName, int minChunkSize, int offset, Action<double> progressReporter)
    {
        Variable varIn = ncIn.GetVariable(variableName);
        Variable varOut = ncOut.GetVariable(variableName);

        IReadOnlyList<Dimension> dimensions = varIn.Dimensions.ToArray(ncIn.GetDimension);

        string unitsIn = varIn.GetUnits();
        string unitsOut = varOut.GetUnits();

        if (unitsIn != unitsOut)
            throw new NotImplementedException("TBI: units conversion");

        // The size of each dimension in the input file.
        int[] dimensionSizes = dimensions.ToArray(d => d.Size);

        // The chunk sizes to be used for reading data.
        int[] chunkSizes = varIn.ChunkSizes.ToArray(c => GetChunkSize(c, minChunkSize));

        // Total number of iterations per dimension.
        int[] niter = chunkSizes.Zip(dimensionSizes, (c, s) => (int)Math.Ceiling(1.0 * s / c)).ToArray();

        // Total number of iterations across all dimensions.
        long itMax = niter.Product();

        // Offsets for each dimension.
        int[] offsets = dimensions.ToArray(d => d.Name == dimensionName ? offset : 0);

        // Create an array to hold each chunk of outputs.
        Array chunk = Array.CreateInstance(varIn.DataType, chunkSizes.Product());

        // The number of elements that have been read.
        MutableRange iread = new MutableRange();
        MutableRange iwrite = new MutableRange();
        MutableRange jread = new MutableRange();
        MutableRange jwrite = new MutableRange();
        MutableRange kread = new MutableRange();
        MutableRange kwrite = new MutableRange();

        long it = 0;

        for (int i = 0; i < niter[0]; i++)
        {
            // Start index for this iteration on the i-th dimension.
            int ilow = i * chunkSizes[0];

            // Don't allow more values to be read along this dimension than
            // exist along this dimension.
            int ihigh = Math.Min(dimensionSizes[0], ilow + chunkSizes[0]);

            iread.Start = ilow;
            iread.Count = ihigh - ilow;

            iwrite.Start = ilow + offsets[0];
            iwrite.Count = iread.Count;

            for (int j = 0; j < niter[1]; j++)
            {
                // Start index for this iteration on the j-th dimension.
                int jlow = j * chunkSizes[1];

                // Don't allow more values to be read along this dimension than
                // exist along this dimension.
                int jhigh = Math.Min(dimensionSizes[1], jlow + chunkSizes[1]);

                jread.Start = jlow;
                jread.Count = jhigh - jlow;

                jwrite.Start = jlow + offsets[1];
                jwrite.Count = jread.Count;

                for (int k = 0; k < niter[2]; k++)
                {
                    // Start index for this iteration on the k-th dimension.
                    int klow = k * chunkSizes[2];

                    // Don't allow more values to be read along this dimension
                    // than exist along this dimension.
                    int khigh = Math.Min(dimensionSizes[2], klow + chunkSizes[2]);

                    kread.Start = klow;
                    kread.Count = khigh - klow;

                    kwrite.Start = klow + offsets[2];
                    kwrite.Count = kread.Count;

                    varIn.Read(chunk, iread, jread, kread);

                    // Write data to the output file.
                    varOut.Write(chunk, iwrite, jwrite, kwrite);

                    // Progress reporting.
                    it++;
                    progressReporter(1.0 * it / itMax);
                }
            }
        }
    }

    private static int GetChunkSize(int chunkSize, int minChunkSize)
    {
        // Don't allow zero or negative chunk sizes.
        chunkSize = Math.Max(1, chunkSize);

        // Attempt to use the multiplier provided by the user.
        return Math.Max(chunkSize * minChunkSize, chunkSize);
    }
}
