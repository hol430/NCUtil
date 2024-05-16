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

	public static Array ToMultiDimensionalArray(this Array array1d, int[] shape)
	{
		if (array1d.Length <= 1)
			return array1d;
		if (shape.Length == 0)
			throw new InvalidOperationException($"Unable to convert to multi-dimensional array: no shape input was provided");

		Type elementType = array1d.GetType().GetElementType()!;
		Array result = Array.CreateInstance(elementType, shape);
		if (Buffer.ByteLength(result) != Buffer.ByteLength(array1d))
			throw new InvalidOperationException($"Unable to convert to multi-dimensional array: input array length does not match the desired output shape");

		// TODO: avoid a copy!!!
		Buffer.BlockCopy(array1d, 0, result, 0, Buffer.ByteLength(result));

		return result;
	}

	public static Array ToFlatArray(this Array array)
	{
		if (array.Rank == 1)
			return array;

		Type elementType = array.GetType().GetElementType()!;
		long length = Enumerable.Range(0, array.Rank).Product(array.GetLength);

		Array result = Array.CreateInstance(elementType, length);

		if (Buffer.ByteLength(result) != Buffer.ByteLength(array))
			throw new InvalidOperationException($"Unable to convert to flat array: destination array length does not match input array (this should never happen)");

		Buffer.BlockCopy(array, 0, result, 0, Buffer.ByteLength(result));
		return result;
	}

	public static int IndexOf<T>(this IEnumerable<T> enumerable, Func<T, bool> match)
	{
		int i = 0;
		foreach (T elem in enumerable)
		{
			if (match(elem))
				return i;
			i++;
		}
		return -1;
	}

	public static T2[] ToArray<T1, T2>(this IEnumerable<T1> enumerable, Func<T1, T2> selector)
	{
		return enumerable.Select(selector).ToArray();
	}
}
