using System;

using static Utils;

//mv
class Either<L, R> {
	internal readonly bool isLeft;
	readonly L _left;
	readonly R _right;

	Either(bool isLeft, L left, R right) { this.isLeft = isLeft; this._left = left; this._right = right; }
	internal static Either<L, R> Left(L l) => new Either<L, R>(true, l, default(R));
	internal static Either<L, R> Right(R r) => new Either<L, R>(false, default(L), r);

	internal Either<L2, R2> map<L2, R2>(Func<L, L2> mapLeft, Func<R, R2> mapRight) =>
		isLeft ? Either<L2, R2>.Left(mapLeft(_left)) : Either<L2, R2>.Right(mapRight(_right));

	internal Either<L2, R> map<L2>(Func<L, L2> mapLeft) =>
		isLeft ? Either<L2, R>.Left(mapLeft(_left)) : Either<L2, R>.Right(_right);

	//TODO:KILL
	internal L force() {
		assert(isLeft);
		return _left;
	}

	internal bool isRight => !isLeft;

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

static class EitherU {
	internal static bool deepEq<L, R>(this Either<L, R> a, Either<L, R> b) where L : DeepEqual<L> where R : DeepEqual<R> =>
		a.isLeft ? b.isLeft && a.left.Equals(b.left) : b.isRight && a.right.Equals(b.right);
}
