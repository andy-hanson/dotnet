using System;
using System.Collections.Immutable;

static class Arr {
    internal static ImmutableArray<U> fromMappedArray<T, U>(T[] inputs, Func<T, U> mapper) {
        var b = ImmutableArray.CreateBuilder<U>(inputs.Length);
        for (var i = 0; i < inputs.Length; i++)
            b[i] = mapper(inputs[i]);
        return b.ToImmutable();
    }

    internal static ImmutableArray<T> rcons<T>(ImmutableArray<T> inputs, T next) {
        var b = ImmutableArray.CreateBuilder<T>(inputs.Length + 1);
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

    internal static ImmutableArray<T> Slice<T>(this ImmutableArray<T> imm, int start, int length) =>
        ImmutableArray.Create(imm, start, length);

    internal static ImmutableArray<T> Concat<T>(this ImmutableArray<T> imm, ImmutableArray<T> other) {
        var b = ImmutableArray.CreateBuilder<T>(imm.Length + other.Length);
        for (var i = 0; i < imm.Length; i++)
            b[i] = imm[i];
        for (var i = 0; i < other.Length; i++)
            b[imm.Length + i] = other[i];
        return b.ToImmutable();
    }
}
