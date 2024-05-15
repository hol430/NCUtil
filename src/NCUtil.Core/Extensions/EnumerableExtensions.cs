namespace NCUtil.Core.Extensions;

public static class EnumerableExtensions
{
	public static long Product(this IEnumerable<int> enumerable)
	{
		long result = 1L;
		foreach (int x in enumerable)
			result *= x;
		return result;
		// Aggregate() will throw on empty collections.
		// return enumerable.Aggregate((x, y) => x * y);
	}

	public static long Product<T>(this IEnumerable<T> enumerable, Func<T, int> selector)
	{
		return enumerable.Select(selector).Product();
	}
}
