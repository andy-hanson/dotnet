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

    internal static ImmutableArray<U> fromMappedBuilder<T, U>(ImmutableArray<T>.Builder inputs, Func<T, U> mapper) {
        var b = FixedSizeBuilder<U>(inputs.Count);
        for (var i = 0; i < inputs.Count; i++)
            b[i] = mapper(inputs[i]);
        return b.ToImmutable();
    }

    internal static ImmutableArray<U> fromMappedArray<T, U>(T[] inputs, Func<T, U> mapper) {
        var b = FixedSizeBuilder<U>(inputs.Length);
        for (var i = 0; i < inputs.Length; i++)
            b[i] = mapper(inputs[i]);
        return b.ToImmutable();
    }

    internal static ImmutableArray<T> rcons<T>(ImmutableArray<T> inputs, T next) {
        var b = FixedSizeBuilder<T>(inputs.Length + 1);
        for (var i = 0; i < inputs.Length; i++)
            b[i] = inputs[i];
        b[inputs.Length] = next;
        return b.ToImmutable();
    }

    internal static ImmutableArray<T> rtail<T>(this ImmutableArray<T> imm) =>
        ImmutableArray.Create(imm, 0, imm.Length - 1);

    //mv
    internal static U[] MapToArray<T, U>(this ImmutableArray<T> imm, Func<T, U> mapper) {
        U[] res = new U[imm.Length];
        for (var i = 0; i < imm.Length; i++) {
            res[i] = mapper(imm[i]);
        }
        return res;
    }

    internal static void doZip<T, U>(this ImmutableArray<T> a, ImmutableArray<U> b, Action<T, U> zipper) {
        assert(a.Length == b.Length);
        for (var i = 0; i < a.Length; i++)
            zipper(a[i], b[i]);
    }

    internal static void each<T>(this ImmutableArray<T> a, Action<T, int> action) {
        for (var i = 0; i < a.Length; i++)
            action(a[i], i);
    }
    internal static void eachInSlice<T>(this ImmutableArray<T> a, int start, int end, Action<T> action) {
        for (var i = start; i < end; i++)
            action(a[i]);
    }

    internal static ImmutableArray<V> zip<T, U, V>(this ImmutableArray<T> a, ImmutableArray<U> b, Func<T, U, V> zipper) {
        assert(a.Length == b.Length);
        var res = ImmutableArray.CreateBuilder<V>();
        for (var i = 0; i < a.Length; i++)
            res[i] = zipper(a[i], b[i]);
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

    internal static ImmutableArray<T> Slice<T>(this ImmutableArray<T> imm, int start, int length) =>
        ImmutableArray.Create(imm, start, length);

    internal static T popLeft<T>(this ImmutableArray<T>.Builder imm) {
        var v = imm[0];
        imm.RemoveAt(0);
        return v;
    }

    internal static ImmutableArray<T> tail<T>(this ImmutableArray<T> imm) =>
        imm.Slice(1, imm.Length - 1);

    internal static ImmutableArray<U> map<T, U>(this ImmutableArray<T> imm, Func<T, U> mapper) {
        var b = FixedSizeBuilder<U>(imm.Length);
        for (var i = 0; i < imm.Length; i++)
            b[i] = mapper(imm[i]);
        return b.ToImmutable();
    }

    internal static ImmutableArray<T> Concat<T>(this ImmutableArray<T> imm, ImmutableArray<T> other) {
        var b = FixedSizeBuilder<T>(imm.Length + other.Length);
        for (var i = 0; i < imm.Length; i++)
            b[i] = imm[i];
        for (var i = 0; i < other.Length; i++)
            b[imm.Length + i] = other[i];
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
