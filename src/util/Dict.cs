using System;
using System.Collections.Generic;

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

    internal Dictionary<K, V>.ValueCollection values => inner.Values;
}

//TODO: struct Dict<K, V> where K : IEquatable<K>, IHash
static class Dict {
    internal static Dictionary<K, V> builder<K, V>() where K : IEquatable<K> => new Dictionary<K, V>();

    internal static Dict<K, V> create<K, V>(params KeyValuePair<K, V>[] pairs) where K : IEquatable<K> {
        var b = new Dictionary<K, V>();
        foreach (var pair in pairs)
            b[pair.Key] = pair.Value;
        return new Dict<K, V>(b);
    }

    internal static Dict<V, K> reverse<K, V>(this Dict<K, V> dict) where K : IEquatable<K> where V : IEquatable<V> {
        var b = new Dictionary<V, K>();
        foreach (var pair in dict)
            b[pair.Value] = pair.Key;
        return new Dict<V, K>(b);
    }

    internal static KeyValuePair<K, V> to<K, V>(this K key, V value) => KeyValuePair.Create(key, value);

    internal static Dict<U, V> mapToDict<T, U, V>(this T[] xs, Func<T, KeyValuePair<U, V>?> mapper) where U : IEquatable<U> {
        var b = new Dictionary<U, V>();
        foreach (var x in xs) {
            var pair = mapper(x);
            if (pair.HasValue)
                b[pair.Value.Key] = pair.Value.Value;
        }
        return new Dict<U, V>(b);
    }
}
