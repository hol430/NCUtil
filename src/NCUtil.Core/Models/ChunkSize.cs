namespace NCUtil.Core.Models;

public class ChunkSize
{
	public string Dimension { get; private init; }
	public int Size { get; private init; }

	public ChunkSize(string dimension, int size)
	{
		Dimension = dimension;
		Size = size;
	}

	public ChunkSize(string spec)
	{
		string[] parts = spec.Split("/");
		if (parts.Length != 2)
			throw new ArgumentException($"Unable to parse chunk size from spec: '{spec}'. The expected format is 'dim_name/size'");
		Dimension = parts[0];
		if (int.TryParse(parts[1], out int size))
			Size = size;
		else
			throw new ArgumentException($"Unable to parse chunk size '{parts[1]}' as an integer chunk size for dimension {Dimension}");
	}
}
