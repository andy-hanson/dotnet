using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using static Utils;

static class Op {
	internal static Op<T> fromNullable<T>(T value) =>
		value != null ? Op<T>.Some(value) : Op<T>.None;

	internal static Op<T> Some<T>(T value) => Op<T>.Some(value);
}

struct Op<T> {
    readonly T value;
    Op(T value) { this.value = value; }

	internal T unsafeValue => value;

	internal bool has => !RuntimeHelpers.Equals(value, default(T));

	internal bool get(out T v) {
		v = value;
		return has;
	}

	internal T force {
		get {
			if (!has) throw new Exception("Tried to force null value.");
			return value;
		}
	}

	internal static Op<T> None => new Op<T>(default(T));
	internal static Op<T> Some(T value) {
		var o = new Op<T>(value);
		assert(!(value is uint)); // Use OpUint instead
		assert(o.has);
		return o;
	}

    internal Op<U> map<U>(Func<T, U> mapper) =>
        has ? Op.Some(mapper(value)) : Op<U>.None;
	internal OpUint mapToUint(Func<T, uint> mapper) =>
		has ? OpUint.Some(mapper(value)) : OpUint.None;

	internal void each(Action<T> action) {
		if (has)
			action(value);
	}

	internal T or(Func<T> or) =>
		has ? value : or();

	internal Op<T> orTry(Func<Op<T>> orTry) =>
		has ? this : orTry();

	internal bool equalsRaw(Op<T> other) =>
		EqualityComparer<T>.Default.Equals(value, other.value);

	//public bool Equals(Op<T> o) =>
	//	has ? o.has && value.Equals(o.value) : !o.has;

	//public override int GetHashCode() =>
	//	has ? value.GetHashCode() : -1;

	//public static bool operator ==(Op<T> a, Op<T> b) => a.Equals(b);
	//public static bool operator !=(Op<T> a, Op<T> b) => !a.Equals(b);
}

static class OpU {
	public static bool eq<T>(this Op<T> a, Op<T> b) where T : IEquatable<T> =>
		a.get(out var av)
			? b.get(out var bv) && av.Equals(bv)
			: !b.has;

	public static bool eq<U>(this Op<U> a, Op<U> b, Func<U, U, bool> compare) =>
		a.get(out var av)
			? b.get(out var bv) && compare(av, bv)
			: !b.has;
}

// Optional unsigned integer.
struct OpUint : ToData<OpUint> {
	readonly uint value;
	OpUint(uint value) { this.value = value; }

	internal bool has => value != uint.MaxValue;

	internal bool get(out uint v) {
		v = value;
		return has;
	}

	internal uint force {
		get {
			if (!has) throw new Exception("Tried to force null value.");
			return value;
		}
	}

	internal static OpUint None => new OpUint(uint.MaxValue);
	internal static OpUint Some(uint value) {
		var o = new OpUint(value);
		assert(o.has);
		return o;
	}

    internal Op<U> map<U>(Func<uint, U> mapper) =>
        has ? Op.Some(mapper(value)) : Op<U>.None;

	internal uint or(Func<uint> or) =>
		has ? value : or();

	public bool Equals(OpUint u) =>
		value == u.value;

	public Dat toDat() =>
		Dat.op(map(Dat.num));
}