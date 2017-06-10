using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

using static Utils;

struct Arr<T> {
    readonly ImmutableArray<T> inner;

    public ImmutableArray<T>.Enumerator GetEnumerator() => inner.GetEnumerator();

    internal uint length => (uint) inner.Length;

    internal Arr(ImmutableArray<T> inner) { this.inner = inner; }

    internal T this[uint idx] {
        get { return inner[signed(idx)]; }
    }

    internal Arr<U> map<U>(Func<T, U> mapper) {
        var b = Arr.fixedSizeBuilder<U>(length);
        for (uint i = 0; i < length; i++)
            b[i] = mapper(this[i]);
        return b.finish();
    }

    internal Arr<T> rcons(T next) {
        var b = Arr.fixedSizeBuilder<T>(length + 1);
        for (uint i = 0; i < length; i++)
            b[i] = this[i];
        b[length] = next;
        return b.finish();
    }

    internal Arr<T> rtail() =>
        new Arr<T>(ImmutableArray.Create(inner, 0, inner.Length - 1));

    internal U[] mapToArray<U>(Func<T, U> mapper) {
        U[] res = new U[length];
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

    internal Arr<V> zip<U, V>(Arr<U> b, Func<T, U, V> zipper) {
        assert(length == b.length);
        var res = Arr.builder<V>();
        for (uint i = 0; i < length; i++)
            res[i] = zipper(this[i], b[i]);
        return res.finish();
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

    internal Arr<T> Slice(uint start, uint length) =>
        new Arr<T>(ImmutableArray.Create(inner, signed(start), signed(length)));

    internal Arr<T> tail() => Slice(1, length - 1);

    internal Arr<T> Concat(Arr<T> other) {
        var b = Arr.fixedSizeBuilder<T>(length + other.length);
        for (uint i = 0; i < length; i++)
            b[i] = this[i];
        for (uint i = 0; i < other.length; i++)
            b[length + i] = other[i];
        return b.finish();
    }

    internal T last => this[this.length - 1];

    internal bool isEmpty => length == 0;

    internal Arr.Builder<T> toBuilder() => new Arr.Builder<T>(inner.ToBuilder());

}

static class Arr {
    internal static string join<T>(this Arr<T> xs, string joiner) {
        if (xs.length == 0) return "";

        var res = new StringBuilder();
        for (uint i = 0; i < xs.length - 1; i++) {
            res.Append(xs[i]);
            res.Append(joiner);
        }
        res.Append(xs[xs.length - 1]);
        return res.ToString();
    }

    //Have to define this here to get the type constraint.
    internal static bool eachEqual<T>(this Arr<T> a, Arr<T> b) where T : IEquatable<T> {
        if (a.length != b.length)
            return false;

        for (uint i = 0; i < a.length; i++)
            if (!a[i].Equals(b[i]))
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

    internal static Arr<T> empty<T>() => new Arr<T>(ImmutableArray.Create<T>());
    internal static Arr<T> of<T>(params T[] args) =>
        new Arr<T>(ImmutableArray.Create<T>(args));

    internal static Builder<T> builder<T>() => new Builder<T>(true);
    internal static Builder<T> fixedSizeBuilder<T>(uint size) => Builder<T>.fixedSize(size);

    internal static Arr<U> map<T, U>(this T[] xs, Func<T, U> mapper) {
        var b = Arr.fixedSizeBuilder<U>(unsigned(xs.Length));
        for (uint i = 0; i < xs.Length; i++)
            b[i] = mapper(xs[i]);
        return b.finish();
    }

    internal struct Builder<T> {
        ImmutableArray<T>.Builder b;
        internal Builder(bool dummy) { b = ImmutableArray.CreateBuilder<T>(); }
        Builder(uint size) {
            b = ImmutableArray.CreateBuilder<T>(signed(size));
            b.Count = signed(size);
        }
        internal Builder(ImmutableArray<T>.Builder b) { this.b = b; }

        internal uint length => unsigned(b.Count);

        internal static Builder<T> fixedSize(uint size) => new Builder<T>(size);

        internal T this[uint idx] {
            get { return b[signed(idx)]; }
            set { b[signed(idx)] = value; }
        }

        internal void add(T u) { b.Add(u); }
        internal Arr<T> finish() => new Arr<T>(b.ToImmutable());

        //Where is this used?
        internal T popLeft() {
            var v = b[0];
            b.RemoveAt(0);
            return v;
        }

        internal Arr<U> map<U>(Func<T, U> mapper) {
            var b = new Builder<U>(length);
            for (uint i = 0; i < length; i++)
                b[i] = mapper(this[i]);
            return b.finish();
        }

        internal bool isEmpty => length == 0;
    }

    internal static Arr<T> buildUntilNull<T>(Func<Op<T>> f) where T : class {
        var b = builder<T>();
        while (true) {
            var x = f();
            if (x.get(out var added))
                b.add(added);
            else
                return b.finish();
        }
    }

    internal static Arr<T> buildUntilNullWithFirst<T>(T first, Func<Op<T>> f) where T : class {
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

    internal static Arr<T> buildUntilNull<T>(Func<bool, Op<T>> f) where T : class {
        var b = builder<T>();
        while (true) {
            var x = f(false);
            if (x.get(out var added))
                b.add(added);
            else
                return b.finish();
        }
    }

    internal class Iter<T> {
        internal readonly T item;
        internal bool next;
        internal Iter(T item, bool next) { this.item = item; this.next = next; }
    }

    // Unlike buildUntilNull, this allows you to return a value *and* indicate that `f` shouldn't be called again.
    internal static Arr<T> build2<T>(Func<Iter<T>> f) {
        var b = builder<T>();
        do {
            var t = f();
            b.add(t.item);
            if (!t.next) return b.finish();
        } while (true);
    }
}
