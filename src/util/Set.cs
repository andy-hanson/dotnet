using System;
using System.Collections.Generic;

using static Utils;

struct Set<T> where T : IEquatable<T> {
	readonly HashSet<T> inner;

	internal Set(HashSet<T> inner) { this.inner = inner; }

	public HashSet<T>.Enumerator GetEnumerator() => inner.GetEnumerator();

	internal bool has(T value) =>
		inner.Contains(value);
}

static class Set {
	internal static Builder<T> builder<T>() where T : IEquatable<T> =>
		new Builder<T>(dummy: false);

	internal struct Builder<T> where T : IEquatable<T> {
		readonly HashSet<T> inner;

		internal Builder(bool dummy) { unused(dummy); inner = new HashSet<T>(); }

		internal Set<T> finish() => new Set<T>(inner);

		internal void add(T value) =>
			inner.Add(value);
	}

	internal static HashSet<T> toMutableSet<E, T>(this E xs) where E : IEnumerable<T> where T : IEquatable<T> {
		var h = new HashSet<T>();
		foreach (var x in xs)
			h.Add(x);
		return h;
	}

	internal static Set<T> toSet<E, T>(this E xs) where E : IEnumerable<T> where T : IEquatable<T> =>
		new Set<T>(toMutableSet<E, T>(xs));

	// Type inference won't work on above overload
	internal static Set<T> toSet<T>(this Arr<T> xs) where T : IEquatable<T> =>
		toSet<Arr<T>, T>(xs);

	internal static IEnumerable<T> setDifference<T>(Arr<T> a, Set<T> b) where T : IEquatable<T> {
		foreach (var elem in a) {
			if (!b.has(elem))
				yield return elem;
		}
	}
}
