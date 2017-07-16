using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using static Utils;

[DebuggerDisplay("[{join(\", \")}]")]
struct Arr<T> : IEnumerable<T> {
	readonly T[] inner;

	internal T head => this[0];

	internal T only {
		get {
			assert(length == 1);
			return head;
		}
	}

	internal U foldBackwards<U>(U u, Func<U, T, U> fold) {
		var i = length;
		do {
			i--;
			u = fold(u, this[i]);
		} while (i != 0);
		return u;
	}

	internal void Deconstruct(out T first, out T second) {
		assert(length == 2);
		first = head;
		second = this[1];
	}

	internal Arr<T> addLeft(T value) {
		var b = new T[length + 1];
		b[0] = value;
		for (uint i = 0; i < length; i++)
			b[i + 1] = this[i];
		return new Arr<T>(b);
	}

	IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator(inner);
	System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => throw new NotSupportedException();
	public Enumerator GetEnumerator() => new Enumerator(inner);
	internal struct Enumerator : IEnumerator<T> {
		readonly T[] array;
		uint i;

		internal Enumerator(T[] array) {
			this.array = array;
			this.i = uint.MaxValue;
		}

		public T Current => array[i];

		public bool MoveNext() {
			i++;
			return i < array.Length;
		}

		object System.Collections.IEnumerator.Current => throw new NotSupportedException();
		void System.Collections.IEnumerator.Reset() => throw new NotSupportedException();
		void IDisposable.Dispose() { /*pass*/ }
	}

	internal List<T> toList() =>
		new List<T>(inner);

	internal uint length => (uint)inner.Length;

	internal Arr(T[] inner) { this.inner = inner; }

	internal T this[uint idx] => inner[signed(idx)];

	internal Arr<T> keep(Func<T, bool> keepIf) {
		var b = Arr.builder<T>();
		foreach (var em in this) {
			if (keepIf(em))
				b.add(em);
		}
		return b.finish();
	}

	internal Arr<U> keepOfType<U>() where U : T {
		var b = Arr.builder<U>();
		foreach (var em in this) {
			if (em is U u)
				b.add(u);
		}
		return b.finish();
	}

	internal U[] mapBuilder<U>() => new U[length];

	internal Arr<U> map<U>(Func<T, U> mapper) {
		var b = new U[length];
		for (uint i = 0; i < length; i++)
			b[i] = mapper(this[i]);
		return new Arr<U>(b);
	}

	internal Arr<U> map<U>(Func<T, uint, U> mapper) {
		var b = new U[length];
		for (uint i = 0; i < length; i++)
			b[i] = mapper(this[i], i);
		return new Arr<U>(b);
	}

	internal Arr<U> mapWithFirst<U>(Op<U> first, Func<T, uint, U> mapper) =>
		first.get(out var f) ? mapWithFirst(f, mapper) : map(mapper);

	internal Arr<U> mapWithFirst<U>(U first, Func<T, uint, U> mapper) =>
		new Arr<U>(mapToArrayWithFirst(first, mapper));

	internal U[] mapToArrayWithFirst<U>(U first, Func<T, uint, U> mapper) {
		var b = new U[length + 1];
		b[0] = first;
		for (uint i = 0; i < length; i++)
			b[i + 1] = mapper(this[i], i);
		return b;
	}

	internal U[] mapToArrayWithFirst<U>(Op<U> first, Func<T, U> mapper) =>
		first.get(out var f) ? mapToArrayWithFirst<U>(f, mapper) : mapToArray<U>(mapper);
	internal U[] mapToArrayWithFirst<U>(U first, Func<T, U> mapper) {
		var b = new U[length + 1];
		b[0] = first;
		for (uint i = 0; i < length; i++)
			b[i + 1] = mapper(this[i]);
		return b;
	}

	internal Arr<U> mapDefined<U>(Func<T, Op<U>> mapper) => mapDefinedToBuilder(mapper).finish();

	internal U[] mapDefinedToArray<U>(Func<T, Op<U>> mapper) => mapDefinedToBuilder(mapper).finishToArray();

	Arr.Builder<U> mapDefinedToBuilder<U>(Func<T, Op<U>> mapper) {
		var b = Arr.builder<U>();
		for (uint i = 0; i < length; i++) {
			var res = mapper(this[i]);
			if (res.get(out var r))
				b.add(r);
		}
		return b;

	}

	internal Dict<K, V> mapDefinedToDict<K, V>(Func<T, Op<(K, V)>> mapper) where K : IEquatable<K> {
		var b = Dict.builder<K, V>();
		for (uint i = 0; i < length; i++) {
			var res = mapper(this[i]);
			if (res.get(out var r))
				b.add(r.Item1, r.Item2);
		}
		return b.finish();
	}

	internal Arr<T> rcons(T next) {
		var b = new T[length + 1];
		for (uint i = 0; i < length; i++)
			b[i] = this[i];
		b[length] = next;
		return new Arr<T>(b);
	}

	internal Arr<T> rcons(T next, T nextNext) {
		var b = new T[length + 2];
		for (uint i = 0; i < length; i++)
			b[i] = this[i];
		b[length] = next;
		b[length + 1] = nextNext;
		return new Arr<T>(b);
	}

	internal Arr<T> rtail() => slice(0, length - 1);
	internal Arr<T> tail() => slice(1);

	internal T[] sliceToBuilder(uint lo, uint hi) {
		var slicedLength = hi - lo;
		var res = new T[slicedLength];
		Array.Copy(inner, lo, res, 0, slicedLength);
		return res;
	}

	internal Arr<T> slice(uint lo) => slice(lo, length);
	internal Arr<T> slice(uint lo, uint hi) => new Arr<T>(sliceToBuilder(lo, hi));

	internal U[] mapToArray<U>(Func<T, U> mapper) {
		var res = new U[length];
		for (uint i = 0; i < length; i++) {
			res[i] = mapper(this[i]);
		}
		return res;
	}

	internal void doZip<U>(Arr<U> b, Action<T, U> zipper) {
		assert(length == b.length);
		for (uint i = 0; i < length; i++)
			zipper(this[i], b[i]);
	}

	internal Arr<V> zip<U, V>(Arr<U> b, Func<T, U, V> zipper) {
		assert(length == b.length);
		var res = new V[length];
		for (uint i = 0; i < length; i++)
			res[i] = zipper(this[i], b[i]);
		return new Arr<V>(res);
	}

	internal bool find(out T found, Func<T, bool> predicate) {
		for (uint i = 0; i < length; i++) {
			var em = this[i];
			if (predicate(em)) {
				found = em;
				return true;
			}
		}

		found = default(T);
		return false;
	}

	internal Arr<T> Concat(Arr<T> other) {
		var b = new T[length + other.length];
		for (uint i = 0; i < length; i++)
			b[i] = this[i];
		for (uint i = 0; i < other.length; i++)
			b[length + i] = other[i];
		return new Arr<T>(b);
	}

	internal T last => this[this.length - 1];

	internal bool isEmpty => length == 0;

	internal T[] toBuilder() => sliceToBuilder(0, length);

	internal string join(string joiner) {
		if (length == 0)
			return string.Empty;

		var res = new StringBuilder();
		join(joiner, res);
		return res.ToString();
	}

	internal void join(string joiner, StringBuilder sb) {
		if (length == 0)
			return;

		for (uint i = 0; i < length - 1; i++) {
			sb.Append(this[i]);
			sb.Append(joiner);
		}

		sb.Append(this[this.length - 1]);
	}
}

static class Arr {
	internal static Arr<T> slice<T>(this T[] arr, uint low, uint high) {
		var len = high - low;
		var res = new T[len];
		for (uint i = 0; i < len; i++)
			res[i] = arr[low + i];
		return new Arr<T>(res);
	}

	internal static Arr<T> slice<T>(this T[] arr, uint low) =>
		slice(arr, low, unsigned(arr.Length));

	internal static Arr<T> toArr<T>(this IEnumerable<T> xs) {
		var b = builder<T>();
		foreach (var x in xs)
			b.add(x);
		return b.finish();
	}

	internal static void each<T>(this T[] a, Action<T, uint> action) {
		for (uint i = 0; i < a.Length; i++)
			action(a[i], i);
	}

	internal static V[] zip<T, U, V>(this T[] a, U[] b, Func<T, U, V> zipper) {
		assert(a.Length == b.Length);
		var res = new V[a.Length];
		for (uint i = 0; i < a.Length; i++)
			res[i] = zipper(a[i], b[i]);
		return res;
	}

	//Have to define this here to get the type constraint.
	internal static bool deepEqual<T>(this Arr<T> a, Arr<T> b) where T : DeepEqual<T> =>
		deepEqual(a, b, (x, y) => x.deepEqual(y));

	internal static bool deepEqual(this Arr<string> a, Arr<string> b) => deepEqual(a, b, (x, y) => x == y);

	internal static bool eachEqualId<T, U>(this Arr<T> a, Arr<T> b) where T : Identifiable<U> where U : ToData<U> =>
		deepEqual(a, b, (x, y) => x.equalsId<T, U>(y));

	static bool deepEqual<T>(this Arr<T> a, Arr<T> b, Func<T, T, bool> equal) {
		if (a.length != b.length)
			return false;

		for (uint i = 0; i < a.length; i++)
			if (!equal(a[i], b[i]))
				return false;

		return true;
	}

	internal static bool find<T>(this IEnumerable<T> elements, out T found, Func<T, bool> predicate) {
		foreach (var em in elements) {
			if (predicate(em)) {
				found = em;
				return true;
			}
		}

		found = default(T);
		return false;
	}

	internal static Arr<T> empty<T>() => new Arr<T>(new T[] {});
	internal static Arr<T> of<T>(params T[] args) =>
		new Arr<T>(args);

	internal static Builder<T> builder<T>() => new Builder<T>(true);
	internal static Builder<T> builder<T>(T first) {
		var b = new Builder<T>(true);
		b.add(first);
		return b;
	}

	internal static Arr<U> map<T, U>(this T[] xs, Func<T, U> mapper) => new Arr<U>(mapToArray(xs, mapper));

	internal static Arr<U> mapSlice<T, U>(this T[] xs, uint start, Func<T, uint, U> mapper) {
		var b = new U[xs.Length - start];
		for (uint i = 0; i < b.Length; i++)
			b[i] = mapper(xs[i + start], i);
		return new Arr<U>(b);
	}

	internal static U[] mapToArray<T, U>(this T[] xs, Func<T, U> mapper) {
		var b = new U[xs.Length];
		for (uint i = 0; i < xs.Length; i++)
			b[i] = mapper(xs[i]);
		return b;
	}

	internal static Arr<U> map<T, U>(this T[] xs, Func<T, uint, U> mapper) {
		var b = new U[xs.Length];
		for (uint i = 0; i < xs.Length; i++)
			b[i] = mapper(xs[i], i);
		return new Arr<U>(b);
	}

	internal struct Builder<T> {
		readonly List<T> b;
		internal Builder(bool dummy) { unused(dummy); b = new List<T>(); }

		/** Number of elements added so far. */
		internal uint curLength => unsigned(b.Count);

		internal T this[uint idx] {
			get => b[signed(idx)];
			set => b[signed(idx)] = value;
		}

		internal void add(T u) { b.Add(u); }
		internal T[] finishToArray() => b.ToArray();
		internal Arr<T> finish() => new Arr<T>(b.ToArray());

		internal Arr<T> finishWithFirst(T first) {
			var a = new T[b.Count + 1];
			a[0] = first;
			for (uint i = 0; i < b.Count; i++)
				a[i + 1] = b[signed(i)];
			return new Arr<T>(a);
		}

		internal Arr<T> finishWithFirstTwo(T first, T second) {
			var a = new T[b.Count + 2];
			a[0] = first;
			a[1] = second;
			for (uint i = 0; i < b.Count; i++)
				a[i + 2] = b[signed(i)];
			return new Arr<T>(a);
		}

		internal Arr<T> finishTail() {
			b.RemoveAt(0);
			return finish();
		}

		internal void clear() => b.Clear();

		internal Arr<U> map<U>(Func<T, U> mapper) {
			var b2 = new U[curLength];
			for (uint i = 0; i < curLength; i++)
				b2[i] = mapper(this[i]);
			return new Arr<U>(b2);
		}

		internal T last => b[b.Count - 1];
		internal void setLast(T last) {
			b[b.Count - 1] = last;
		}

		internal bool isEmpty => curLength == 0;
	}

	internal static Arr<T> buildUntilNull<T>(Func<Op<T>> f) {
		var b = builder<T>();
		while (true) {
			var x = f();
			if (x.get(out var added))
				b.add(added);
			else
				return b.finish();
		}
	}

	internal static Arr<T> buildUntilNullWithFirst<T>(T first, Func<Op<T>> f) {
		var b = builder<T>();
		b.add(first);
		while (true) {
			var x = f();
			if (x.get(out var added))
				b.add(added);
			else
				return b.finish();
		}
	}

	internal static Arr<T> buildUntilNull<T>(Func<bool, Op<T>> f) {
		var b = builder<T>();
		while (true) {
			var x = f(false);
			if (x.get(out var added))
				b.add(added);
			else
				return b.finish();
		}
	}

	// Unlike buildUntilNull, this allows you to return a value *and* indicate that `f` shouldn't be called again.
	internal static Arr<T> build2<T>(Func<(T item, bool isNext)> f) {
		var b = builder<T>();
		while (true) {
			var (item, isNext) = f();
			b.add(item);
			if (!isNext) return b.finish();
		}
	}
}
