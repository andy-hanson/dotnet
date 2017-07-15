using System;
using System.Collections.Generic;

using static Utils;

struct Dict<K, V> where K : IEquatable<K> {
	readonly Dictionary<K, V> inner;

	internal Dict(Dictionary<K, V> inner) { this.inner = inner; }

	internal Dict<K2, V> mapKeys<K2>(Func<K, K2> mapper) where K2 : IEquatable<K2> {
		var b = new Dictionary<K2, V>();
		foreach (var (k, v) in inner)
			b[mapper(k)] = v;
		return new Dict<K2, V>(b);
	}

	internal Dict<K2, V2> map<K2, V2>(Func<K, V, (K2, V2)> mapper) where K2 : IEquatable<K2> {
		var b = new Dictionary<K2, V2>();
		foreach (var (k, v) in inner) {
			var (k2, v2) = mapper(k, v);
			b[k2] = v2;
		}
		return new Dict<K2, V2>(b);
	}

	internal Arr<T> mapToArr<T>(Func<K, V, T> mapper) {
		var b = Arr.builder<T>();
		foreach (var (k, v) in inner)
			b.add(mapper(k, v));
		return b.finish();
	}

	public Dictionary<K, V>.Enumerator GetEnumerator() => inner.GetEnumerator();
	public Dictionary<K, V>.ValueCollection values => inner.Values;

	internal bool has(K k) => inner.ContainsKey(k);

	internal V this[K k] => inner[k];

	internal bool get(K k, out V v) => inner.TryGetValue(k, out v);

	internal uint size => unsigned(inner.Count);
}

static class Dict {
	internal static bool deepEqual<K, V>(this Dict<K, V> a, Dict<K, V> b) where K : IEquatable<K> where V : DeepEqual<V> {
		if (a.size != b.size)
			return false;

		foreach (var (k, av) in a) {
			if (!b.get(k, out var bv))
				return false;
			if (!av.deepEqual(bv))
				return false;
		}

		return true;
	}

	internal static Builder<K, V> builder<K, V>() where K : IEquatable<K> =>
		new Builder<K, V>(dummy: false);

	internal static Dict<K, V> of<K, V>(params (K, V)[] pairs) where K : IEquatable<K> {
		var b = new Dictionary<K, V>();
		foreach (var pair in pairs)
			b[pair.Item1] = pair.Item2;
		return new Dict<K, V>(b);
	}

	internal static Dict<V, K> reverse<K, V>(this Dict<K, V> dict) where K : IEquatable<K> where V : IEquatable<V> {
		var b = new Dictionary<V, K>();
		foreach (var (k, v) in dict)
			b[v] = k;
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

	internal struct Builder<K, V> where K : IEquatable<K> {
		readonly Dictionary<K, V> inner;

		internal Builder(bool dummy) { unused(dummy); inner = new Dictionary<K, V>(); }

		internal Dict<K, V> finish() => new Dict<K, V>(inner);

		internal V this[K k] => inner[k];

		internal bool get(K key, out V value) => inner.TryGetValue(key, out value);

		internal void add(K key, V value) =>
			inner.Add(key, value);

		internal void change(K key, V newValue) {
			assert(inner.ContainsKey(key));
			inner[key] = newValue;
		}

		internal Dict<K2, V2> map<K2, V2>(Func<K, V, (K2, V2)> mapper) where K2 : IEquatable<K2> {
			var b = builder<K2, V2>();
			foreach (var (k, v) in inner) {
				var (k2, v2) = mapper(k, v);
				b.add(k2, v2);
			}
			return b.finish();
		}

		internal Dict<K, V2> mapValues<V2>(Func<V, V2> mapper) {
			var b = builder<K, V2>();
			foreach (var (k, v) in inner)
				b.add(k, mapper(v));
			return b.finish();
		}

		internal bool tryAdd(K key, V value) {
			if (inner.ContainsKey(key)) {
				return false;
			}

			inner[key] = value;
			return true;
		}
	}
}

static class DictBuilderUtils {
	internal static void multiMapAdd<K, V>(this Dict.Builder<K, Arr.Builder<V>> b, K key, V value) where K : IEquatable<K> {
		if (b.get(key, out var values))
			values.add(value);
		else
			b.add(key, Arr.builder<V>(value));
	}
}

static class DictionaryUtils {
	internal static V getOrUpdate<K, V>(this IDictionary<K, V> dict, K key, Func<V> getValue) {
		if (dict.TryGetValue(key, out var value))
			return value;

		value = getValue();
		dict.Add(key, value);
		return value;
	}
}
