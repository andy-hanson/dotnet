using System;
using System.Collections.Immutable;
using static Utils;

static class Utils {
	internal static Exception TODO(string message = "!") => new Exception(message);

	internal static Exception unreachable() => new Exception("UNREACHABLE");

	/*internal static T nonNull<T>(T t) {
		assert(t != null);
		return t;
	}*/

	internal static ImmutableArray<T> build<T>(Action<ImmutableArray<T>.Builder> builder) {
		var b = ImmutableArray.CreateBuilder<T>();
		builder(b);
		return b.ToImmutable();
	}

	internal static void assert(bool condition) {
		if (!condition) {
			throw new Exception("Assertion failed.");
		}
	}

	internal static void doTimes(int times, Action action) {
		assert(times >= 0);
		for (var i = times; i != 0; i--) {
			action();
		}
	}
}

//mv
class Either<L, R>  {
	bool isLeft;
	readonly L _left;
	readonly R _right;

	Either(bool isLeft, L left, R right) { this.isLeft = isLeft; this._left = left; this._right = right; }
	internal static Either<L, R> Left(L l) => new Either<L, R>(true, l, default(R));
	internal static Either<L, R> Right(R r) => new Either<L, R>(false, default(L), r);

	internal L left {
		get {
			assert(isLeft);
			return _left;
		}
	}
	internal R right {
		get {
			assert(!isLeft);
			return _right;
		}
	}
}
