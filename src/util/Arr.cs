using System;
using System.Collections.Generic;
using System.Text;

using static Utils;

struct Arr<T> {
    readonly T[] inner;

    public Enumerator GetEnumerator() => new Enumerator(inner);
    internal struct Enumerator {
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

    internal Arr<U> mapDefined<U>(Func<T, Op<U>> mapper) {
        var b = Arr.builder<U>();
        for (uint i = 0; i < length; i++) {
            var res = mapper(this[i]);
            if (res.get(out var r))
                b.add(r);
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
        var length = hi - lo;
        var res = new T[length];
        Array.Copy(inner, lo, res, 0, length);
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

    internal void each(Action<T> action) {
        for (uint i = 0; i < length; i++)
            action(this[i]);
    }

    internal void each(Action<T, uint> action) {
        for (uint i = 0; i < length; i++)
            action(this[i], i);
    }

    internal void eachInSlice(uint start, uint end, Action<T> action) {
        for (var i = start; i < end; i++)
            action(this[i]);
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
}

static class Arr {
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

    internal static string join<T>(this Arr<T> xs, string joiner) {
        if (xs.length == 0) return string.Empty;

        var res = new StringBuilder();
        for (uint i = 0; i < xs.length - 1; i++) {
            res.Append(xs[i]);
            res.Append(joiner);
        }

        res.Append(xs[xs.length - 1]);
        return res.ToString();
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

    internal static Arr<U> map<T, U>(this T[] xs, Func<T, U> mapper) => new Arr<U>(mapToArray(xs, mapper));

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
        internal Arr<T> finish() => new Arr<T>(b.ToArray());

        internal Arr<T> finishTail() {
            b.RemoveAt(0);
            return finish();
        }

        internal void clear() {
            this.clear();
        }

        internal Arr<U> map<U>(Func<T, U> mapper) {
            var b = new U[curLength];
            for (uint i = 0; i < curLength; i++)
                b[i] = mapper(this[i]);
            return new Arr<U>(b);
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

//mv
static class ListUtils {
    /*internal static Op<T> deleteFirstWhere<T>(this List<T> list, Func<T, bool> f) {
        for (uint i = 0; i < list.Count; i++) {
            var res = list[signed(i)];
            if (f(res)) {

            }
        }
    }*/
}
