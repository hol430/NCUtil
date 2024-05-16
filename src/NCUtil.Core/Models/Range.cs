namespace NCUtil.Core.Models;

public class Range : IRange
{
	public int Start { get; private init; }
	public int Count { get; private init; }
	public Range(int start, int count)
	{
		Start = start;
		Count = count;
	}

    public override string ToString()
    {
        return $"{Start}:{Start + Count}";
    }
}
