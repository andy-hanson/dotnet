using System;
using System.Collections.Generic;

using static Utils;

struct Dict<K, V> where K : IEquatable<K> {
	readonly Dictionary<K, V> inner;

	internal Dict(Dictionary<K, V> inner) { this.inner = inner; }

	internal Dict<K2, V> mapKeys<K2>(Func<K, K2> mapper) where K2 : IEquatable<K2> {
		var b = new Dictionary<K2, V>();
		foreach (var pair in inner)
			b[mapper(pair.Key)] = pair.Value;
		return new Dict<K2, V>(b);
	}

	public Dictionary<K, V>.Enumerator GetEnumerator() => inner.GetEnumerator();

	internal bool has(K k) => inner.ContainsKey(k);

	internal bool get(K k, out V v) => inner.TryGetValue(k, out v);

	internal uint size => unsigned(inner.Count);

	internal Dictionary<K, V>.ValueCollection values => inner.Values;
}

static class Dict {
	internal static bool deepEqual<K, V>(this Dict<K, V> a, Dict<K, V> b) where K : IEquatable<K> where V : DeepEqual<V> {
		if (a.size != b.size)
			return false;

		foreach (var pair in a) {
			if (!b.get(pair.Key, out var bv))
				return false;
			if (!pair.Value.deepEqual(bv))
				return false;
		}

		return true;
	}

	internal static bool TryAdd<K, V>(this Dictionary<K, V> dict, K key, V value) {
		if (dict.ContainsKey(key)) {
			return false;
		}
		dict[key] = value;
		return true;
	}

	internal static Dictionary<K, V> builder<K, V>() where K : IEquatable<K> => new Dictionary<K, V>();

	internal static Dict<K, V> of<K, V>(params (K, V)[] pairs) where K : IEquatable<K> {
		var b = new Dictionary<K, V>();
		foreach (var pair in pairs)
			b[pair.Item1] = pair.Item2;
		return new Dict<K, V>(b);
	}

	internal static Dict<V, K> reverse<K, V>(this Dict<K, V> dict) where K : IEquatable<K> where V : IEquatable<V> {
		var b = new Dictionary<V, K>();
		foreach (var pair in dict)
			b[pair.Value] = pair.Key;
		return new Dict<V, K>(b);
	}

	internal static Dict<K, V> mapToDict<T, K, V>(this T[] xs, Func<T, Op<(K, V)>> mapper) where K : IEquatable<K> {
		var b = new Dictionary<K, V>();
		foreach (var x in xs) {
			var pair = mapper(x);
			if (pair.get(out var p))
				b[p.Item1] = p.Item2;
		}
		return new Dict<K, V>(b);
	}

	internal static Dict<K, V2> mapValues<K, V1, V2>(this IDictionary<K, V1> d, Func<V1, V2> mapper) where K : IEquatable<K> {
		var b = new Dictionary<K, V2>();
		foreach (var pair in d)
			b[pair.Key] = mapper(pair.Value);
		return new Dict<K, V2>(b);
	}
}

static class DictionaryUtils {
	internal static Op<V> getOp<K, V>(this IDictionary<K, V> dict, K key) =>
		dict.TryGetValue(key, out var value) ? Op.Some(value) : Op<V>.None;

	internal static V getOrUpdate<K, V>(this IDictionary<K, V> dict, K key, Func<V> getValue) {
		if (dict.TryGetValue(key, out var value))
			return value;

		value = getValue();
		dict.Add(key, value);
		return value;
	}
}