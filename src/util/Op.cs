using System;
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
	internal bool equalsRaw(Op<T> other) =>
		RuntimeHelpers.Equals(value, other.value);

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

	public override bool Equals(object o) => throw new NotImplementedException();
	public override int GetHashCode() => throw new NotImplementedException();
}

static class OpU {
	public static bool deepEqual<T>(this Op<T> a, Op<T> b) where T : DeepEqual<T> =>
		a.get(out var av)
			? b.get(out var bv) && av.deepEqual(bv)
			: !b.has;

	public static bool deepEqual(this Op<string> a, Op<string> b) =>
		a.get(out var av) ? b.get(out var bv) && av == bv : !b.has;

	public static bool deepEqual<U>(this Op<U> a, Op<U> b, Func<U, U, bool> compare) =>
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

	public bool Equals(OpUint u) => throw new NotImplementedException();
	public bool deepEqual(OpUint u) => value == u.value;

	public Dat toDat() =>
		Dat.op(map(Dat.num));
}
