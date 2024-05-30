using NCUtil.Core.Models;
using NCUtil.Core.Logging;
using Attribute = NCUtil.Core.Models.Attribute;
using NCUtil.Core.Interop;

namespace NCUtil.Core.Extensions;

public static class NetCDFExtensions
{
    /// <summary>
    /// Name of the time dimension.
    /// </summary>
    private const string dimTime = "time";

    /// <summary>
    /// Name of the time variable.
    /// </summary>
    private const string varTime = "time";

    /// <summary>
    /// Name of the calendar attribute.
    /// </summary>
    private const string attrCalendar = "calendar";

    /// <summary>
    /// Name of the units attribute.
    /// </summary>
    private const string attrUnits = "units";

    private static readonly IDictionary<Type, NCType> typeLookup = new Dictionary<Type, NCType>()
    {
        { typeof(short), NCType.NC_SHORT },
        { typeof(int), NCType.NC_INT },
        { typeof(long), NCType.NC_INT64 },

        { typeof(ushort), NCType.NC_USHORT },
        { typeof(uint), NCType.NC_UINT },
        { typeof(ulong), NCType.NC_UINT64 },

        { typeof(float), NCType.NC_FLOAT },
        { typeof(double), NCType.NC_DOUBLE },

        { typeof(sbyte), NCType.NC_BYTE },
        { typeof(byte), NCType.NC_UBYTE },
        { typeof(char), NCType.NC_CHAR },
        { typeof(string), NCType.NC_STRING },
    };

    private static readonly IDictionary<NCType, int> dataSizes = new Dictionary<NCType, int>()
    {
        { NCType.NC_SHORT, sizeof(short) },
        { NCType.NC_INT, sizeof(int) },
        { NCType.NC_INT64, sizeof(long) },

        { NCType.NC_USHORT, sizeof(ushort) },
        { NCType.NC_UINT, sizeof(uint) },
        { NCType.NC_UINT64, sizeof(ulong) },

        { NCType.NC_FLOAT, sizeof(float) },
        { NCType.NC_DOUBLE, sizeof(double) },

        { NCType.NC_BYTE, sizeof(sbyte) },
        { NCType.NC_UBYTE, sizeof(byte) },
        { NCType.NC_CHAR, sizeof(char) },
        // { NCType.NC_STRING, sizeof(char) },
    };

    public static NCType ToNCType(this Type type)
    {
        if (typeLookup.ContainsKey(type))
            return typeLookup[type];

        throw new Exception($"Type {type.FullName} has no netcdf equivalent");
    }

    public static Type ToType(this NCType type)
    {
        // All values of the NCType enum have an entry in this dictionary, so
        // this should never throw unless ucar add a new NetCDF type in the future.
        // TODO: how does this behave for user-defined types?
        return typeLookup.First(pair => pair.Value == type).Key;
    }

    /// <summary>
    /// Convert a file open mode to an integer that may be passed to native lib.
    /// TBI: NC_SHARE.
    /// </summary>
    /// <param name="mode">File open mode.</param>
    public static NCOpenMode ToOpenMode(this NetCDFFileMode mode)
    {
        switch (mode)
        {
            case NetCDFFileMode.Read:
                return NCOpenMode.NC_NOWRITE;
            case NetCDFFileMode.Write:
            case NetCDFFileMode.Append:
                return NCOpenMode.NC_WRITE;
            default:
                throw new NotImplementedException($"Unknown file mode: {mode}");
        }
    }

    public static int DataSize(this NCType type)
    {
        if (dataSizes.ContainsKey(type))
            return dataSizes[type];
        throw new Exception($"Unknown data size for type {type}");
    }

    public static bool IsTime(this Dimension dimension)
    {
        return dimension.Name == dimTime;
    }

    public static Dimension GetTimeDimension(this NetCDFFile file)
    {
        return file.GetDimension(dimTime);
    }

    public static Variable GetTimeVariable(this NetCDFFile file)
    {
        return file.GetVariable(varTime);
    }

    public static void CopyMetadataTo(this NetCDFFile from, NetCDFFile to)
    {
        foreach (var attribute in from.GetAttributes())
        {
            Log.Diagnostic("Setting attribute {0} in output file", attribute.Name);
            to.SetGlobalAttribute(attribute);
        }
    }

    public static Attribute GetAttribute(this Variable variable, string name)
    {
        foreach (Attribute attribute in variable.Attributes)
            if (attribute.Name == name)
                return attribute;
        throw new InvalidOperationException($"Variable {variable.Name} has no {name} attribute");
    }

    public static Calendar ParseCalendar(this string attribute)
    {
        switch (attribute)
        {
            case "standard":
            case "gregorian":
                return Calendar.Standard;
            case "proleptic_gregorian":
                return Calendar.ProlepticGregorian;
            case "julian":
                return Calendar.Julian;
            case "noleap":
            case "365_day":
                return Calendar.NoLeap;
            case "360_day":
                return Calendar.EqualLength;
            case "none":
            case "":
                return Calendar.None;
            default:
                throw new InvalidOperationException($"Unable to parse calendar type from attribute value: '{attribute}'");
        }
    }

    public static string ReadStringAttribute(this Variable variable, string name)
    {
        // Get the attribute.
        Attribute attribute = variable.GetAttribute(name);

        // Verify that it's a string attribute.
        if (attribute.Value is not string)
            throw new InvalidOperationException($"Unable to read attribute {name} of variable {variable.Name}: attribute type in netcdf file is of type {attribute.DataType.ToFriendlyName()}, and the attribute value is of type {attribute.Value.GetType().ToFriendlyName()}");

        return (string)attribute.Value;
    }

    public static Calendar GetCalendar(this Variable variable)
    {
        if (variable.Name != varTime)
            throw new InvalidOperationException($"Attempted to get calendar for non-time variable");

        string value = variable.ReadStringAttribute(attrCalendar);
        return ParseCalendar(value);
    }

    public static string GetUnits(this Variable variable)
    {
        return variable.ReadStringAttribute(attrUnits);
    }

    public static string EnumToString(this Enum e)
    {
        return Enum.GetName(e.GetType(), e)!;
    }

    private static int GetChunkSize(int chunkSize, int minChunkSize)
    {
        // Don't allow zero or negative chunk sizes.
        chunkSize = Math.Max(1, chunkSize);

        // Attempt to use the multiplier provided by the user.
        return Math.Max(chunkSize * minChunkSize, chunkSize);
    }

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
}
