using System;
using System.Collections.Generic;

using static Utils;

static class Op {
	internal static Op<T> fromNullable<T>(T value) =>
		value != null ? Op<T>.Some(value) : Op<T>.None;

	internal static Op<T> Some<T>(T value) => Op<T>.Some(value);
}

struct Op<T> {
    readonly T value;
    Op(T value) { this.value = value; }

	//public static implicit operator Op<T>(T value) => Some(value);

	internal bool has => !EqualityComparer<T>.Default.Equals(value, default(T));

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
		assert(o.has);
		return o;
	}

    internal Op<U> map<U>(Func<T, U> mapper) =>
        has ? Op.Some(mapper(value)) : Op<U>.None;

	internal void each(Action<T> action) {
		if (has)
			action(value);
	}

	internal T or(Func<T> or) =>
		has ? value : or();

	internal Op<T> orTry(Func<Op<T>> orTry) =>
		has ? this : orTry();
}

// Optional unsigned integer.
struct OpUint {
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

	internal uint or(Func<uint> or) =>
		has ? value : or();
}
