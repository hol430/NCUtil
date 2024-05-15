namespace NCUtil.Core.Models;

public class ChunkSizes
{
	private readonly IReadOnlyList<ChunkSize> chunking;

	public ChunkSizes(IEnumerable<ChunkSize> chunks)
	{
		this.chunking = chunks.ToList();
	}

	public ChunkSizes(IEnumerable<string> specs) : this(specs.Select(s => new ChunkSize(s)).ToList())
	{
	}

	public ChunkSizes(string spec) : this(spec.Split(","))
	{
	}

	public bool Contains(string dimension)
	{
		return chunking.Select(c => c.Dimension).Contains(dimension);
	}

	public bool ContainsAll(IEnumerable<string> dimensions)
	{
		return dimensions.All(Contains);
	}

	public int GetChunkSize(string dimension)
	{
		return chunking.First(c => c.Dimension == dimension).Size;
	}

	public int[] GetChunkSizes(IEnumerable<string> dimensions)
	{
		return dimensions.Select(GetChunkSize).ToArray();
	}
}
