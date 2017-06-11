using System;

using static Utils;

static class Op {
	internal static Op<T> fromNullable<T>(T value) where T : class =>
		value != null ? Op<T>.Some(value) : Op<T>.None;

	internal static Op<T> Some<T>(T value) where T : class => Op<T>.Some(value);
}

struct Op<T> where T : class {
    readonly T value;
    Op(T value) { this.value = value; }

	//public static implicit operator Op<T>(T value) => Some(value);

	internal bool has => value != null;

	internal bool get(out T v) {
		v = value;
		return v != null;
	}

	internal T force {
		get {
			if (value == null) throw new Exception("Tried to force null value.");
			return value;
		}
	}

	internal static Op<T> None => new Op<T>(null);
	internal static Op<T> Some(T value) {
		assert(value != null);
		return new Op<T>(value);
	}

    internal Op<U> map<U>(Func<T, U> mapper) where U : class =>
        value != null ? Op.Some(mapper(value)) : Op<U>.None;

	internal void each(Action<T> action) {
		if (value != null)
			action(value);
	}

	internal Op<T> or(Func<Op<T>> or) =>
		value != null ? this : or();
}
