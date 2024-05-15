namespace NCUtil.Core.Extensions;

public static class EnumerableExtensions
{
	public static int Product(this IEnumerable<int> enumerable)
	{
		int result = 1;
		foreach (int x in enumerable)
			result *= 1;
		return result;
		// Aggregate() will throw on empty collections.
		// return enumerable.Aggregate((x, y) => x * y);
	}
}
