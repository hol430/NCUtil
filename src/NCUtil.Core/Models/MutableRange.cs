namespace NCUtil.Core.Models;

public class MutableRange : IRange
{
	public int Start { get; set; }
	public int Count { get; set; }
	public MutableRange()
	{
	}

    public override string ToString()
    {
        return $"{Start}:{Start + Count}";
    }
}
