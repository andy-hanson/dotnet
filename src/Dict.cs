using System;
using System.Collections.Generic;
using System.Collections.Immutable;

//TODO: struct Dict<K, V> where K : IEquatable<K>, IHash
static class Dict {
    internal static ImmutableDictionary<K, V> create<K, V>(params KeyValuePair<K, V>[] pairs) {
        var b = ImmutableDictionary.CreateBuilder<K, V>();
        foreach (var pair in pairs)
            b[pair.Key] = pair.Value;
        return b.ToImmutable();
    }

    internal static ImmutableDictionary<K2, V> mapKeys<K1, K2, V>(this ImmutableDictionary<K1, V> dict, Func<K1, K2> mapper) {
        var b = ImmutableDictionary.CreateBuilder<K2, V>();
        foreach (var pair in dict)
            b[mapper(pair.Key)] = pair.Value;
        return b.ToImmutable();
    }

    internal static ImmutableDictionary<V, K> reverse<K, V>(this ImmutableDictionary<K, V> dict) {
        var b = ImmutableDictionary.CreateBuilder<V, K>();
        foreach (var pair in dict)
            b[pair.Value] = pair.Key;
        return b.ToImmutable();
    }

    internal static KeyValuePair<K, V> to<K, V>(this K key, V value) => KeyValuePair.Create(key, value);

    internal static ImmutableDictionary<U, V> mapToDict<T, U, V>(this T[] xs, Func<T, KeyValuePair<U, V>?> mapper) {
        var b = ImmutableDictionary.CreateBuilder<U, V>();
        foreach (var x in xs) {
            var pair = mapper(x);
            if (pair.HasValue)
                b[pair.Value.Key] = pair.Value.Value;
        }
        return b.ToImmutable();
    }
}
