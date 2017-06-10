using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using static Utils;

static class Arr {
    /** Builder for an array with *exactly* `count` elements. */
    static ImmutableArray<T>.Builder FixedSizeBuilder<T>(int count) {
        var b = ImmutableArray.CreateBuilder<T>(count);
        b.Count = count;
        return b;
    }

    internal static char at(this string s, uint idx) =>
        s[checked ((int) idx)];
    internal static T at<T>(this ImmutableArray<T> xs, uint idx) =>
        xs[checked ((int) idx)];

    internal static ImmutableArray<U> map<T, U>(this ImmutableArray<T>.Builder xs, Func<T, U> mapper) {
        var b = FixedSizeBuilder<U>(xs.Count);
        for (var i = 0; i < xs.Count; i++)
            b[i] = mapper(xs[i]);
        return b.ToImmutable();
    }

    internal static ImmutableArray<T> rcons<T>(this ImmutableArray<T> xs, T next) {
        var b = FixedSizeBuilder<T>(xs.Length + 1);
        for (var i = 0; i < xs.Length; i++)
            b[i] = xs[i];
        b[xs.Length] = next;
        return b.ToImmutable();
    }

    internal static ImmutableArray<T> rtail<T>(this ImmutableArray<T> xs) =>
        ImmutableArray.Create(xs, 0, xs.Length - 1);

    //mv
    internal static U[] MapToArray<T, U>(this ImmutableArray<T> xs, Func<T, U> mapper) {
        U[] res = new U[xs.Length];
        for (var i = 0; i < xs.Length; i++) {
            res[i] = mapper(xs[i]);
        }
        return res;
    }

    internal static void doZip<T, U>(this ImmutableArray<T> xs, ImmutableArray<U> b, Action<T, U> zipper) {
        assert(xs.Length == b.Length);
        for (var i = 0; i < xs.Length; i++)
            zipper(xs[i], b[i]);
    }

    internal static void each<T>(this ImmutableArray<T> xs, Action<T, int> action) {
        for (var i = 0; i < xs.Length; i++)
            action(xs[i], i);
    }
    internal static void eachInSlice<T>(this ImmutableArray<T> xs, int start, int end, Action<T> action) {
        for (var i = start; i < end; i++)
            action(xs[i]);
    }

    internal static ImmutableArray<V> zip<T, U, V>(this ImmutableArray<T> xs, ImmutableArray<U> b, Func<T, U, V> zipper) {
        assert(xs.Length == b.Length);
        var res = ImmutableArray.CreateBuilder<V>();
        for (var i = 0; i < xs.Length; i++)
            res[i] = zipper(xs[i], b[i]);
        return res.ToImmutable();
    }

    internal static bool find<T>(this IEnumerable<T> xs, out T found, Func<T, bool> predicate) where T : class {
        foreach (var em in xs) {
            if (predicate(em)) {
                found = em;
                return true;
            }
        }
        found = default(T);
        return false;
    }

    internal static ImmutableArray<T> Slice<T>(this ImmutableArray<T> xs, int start, int length) =>
        ImmutableArray.Create(xs, start, length);

    internal static T popLeft<T>(this ImmutableArray<T>.Builder xs) {
        var v = xs[0];
        xs.RemoveAt(0);
        return v;
    }

    internal static ImmutableArray<T> tail<T>(this ImmutableArray<T> xs) =>
        xs.Slice(1, xs.Length - 1);

    internal static ImmutableArray<U> map<T, U>(this T[] xs, Func<T, U> mapper) {
        var b = FixedSizeBuilder<U>(xs.Length);
        for (var i = 0; i < xs.Length; i++)
            b[i] = mapper(xs[i]);
        return b.ToImmutable();
    }

    internal static ImmutableArray<U> map<T, U>(this ImmutableArray<T> xs, Func<T, U> mapper) {
        var b = FixedSizeBuilder<U>(xs.Length);
        for (var i = 0; i < xs.Length; i++)
            b[i] = mapper(xs[i]);
        return b.ToImmutable();
    }

    internal static ImmutableArray<T> Concat<T>(this ImmutableArray<T> xs, ImmutableArray<T> other) {
        var b = FixedSizeBuilder<T>(xs.Length + other.Length);
        for (var i = 0; i < xs.Length; i++)
            b[i] = xs[i];
        for (var i = 0; i < other.Length; i++)
            b[xs.Length + i] = other[i];
        return b.ToImmutable();
    }

    internal static ImmutableArray<T> buildUntilNull<T>(Func<Op<T>> f) where T : class {
        var b = ImmutableArray.CreateBuilder<T>();
        while (true) {
            var x = f();
            if (x.get(out var added))
                b.Add(added);
            else
                return b.ToImmutable();
        }
    }

    internal static ImmutableArray<T> buildUntilNullWithFirst<T>(T first, Func<Op<T>> f) where T : class {
        var b = ImmutableArray.CreateBuilder<T>();
        b.Add(first);
        while (true) {
            var x = f();
            if (x.get(out var added))
                b.Add(added);
            else
                return b.ToImmutable();
        }
    }

    //Overload taking 'first'
    internal static ImmutableArray<T> buildUntilNull<T>(Func<bool, Op<T>> f) where T : class {
        var b = ImmutableArray.CreateBuilder<T>();
        while (true) {
            var x = f(false);
            if (x.get(out var added))
                b.Add(added);
            else
                return b.ToImmutable();
        }
    }

    internal class Iter<T> {
        internal readonly T item;
        internal bool next;
        internal Iter(T item, bool next) { this.item = item; this.next = next; }
    }

    // Unlike buildUntilNull, this allows you to return a value *and* indicate that `f` shouldn't be called again.
    internal static ImmutableArray<T> build2<T>(Func<Iter<T>> f) {
        var b = ImmutableArray.CreateBuilder<T>();
        do {
            var t = f();
            b.Add(t.item);
            if (!t.next) return b.ToImmutable();
        } while (true);
    }
}
