using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using static Utils;

static class Op {
	[DebuggerStepThrough]
	internal static Op<T> Some<T>(T value) => Op<T>.Some(value);
}

struct Op<T> {
	readonly T value;
	[DebuggerStepThrough]
	Op(T value) { this.value = value; }

	internal T unsafeValue => value;

	internal bool has => !RuntimeHelpers.Equals(value, default(T));
	internal bool equalsRaw(Op<T> other) =>
		RuntimeHelpers.Equals(value, other.value);

	[DebuggerStepThrough]
	internal bool get(out T v) {
		v = value;
		return has;
	}

	internal T force {
		get {
			assert(has, "Tried to force null value.");
			return value;
		}
	}

	internal static Op<T> None => new Op<T>(default(T));
	[DebuggerStepThrough]
	internal static Op<T> Some(T value) {
		var o = new Op<T>(value);
		assert(!(value is uint)); // Use OpUint instead
		assert(o.has);
		return o;
	}

	public override bool Equals(object o) => throw new NotSupportedException();
	public override int GetHashCode() => throw new NotSupportedException();
}

static class OpUtils {
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
			assert(has, "Tried to force null value.");
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

	public override bool Equals(object o) => throw new NotSupportedException();
	public override int GetHashCode() => throw new NotSupportedException();
	public bool deepEqual(OpUint u) => value == u.value;

	public Dat toDat() =>
		Dat.op(map(Dat.nat));
}
