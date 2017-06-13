using System;

using static Utils;

//mv
class Either<L, R>  {
	bool _isLeft;
	readonly L _left;
	readonly R _right;

	Either(bool isLeft, L left, R right) { this._isLeft = isLeft; this._left = left; this._right = right; }
	internal static Either<L, R> Left(L l) => new Either<L, R>(true, l, default(R));
	internal static Either<L, R> Right(R r) => new Either<L, R>(false, default(L), r);

	internal Either<L2, R2> map<L2, R2>(Func<L, L2> mapLeft, Func<R, R2> mapRight) =>
		_isLeft ? Either<L2, R2>.Left(mapLeft(_left)) : Either<L2, R2>.Right(mapRight(_right));

	internal Either<L2, R> map<L2>(Func<L, L2> mapLeft) =>
		_isLeft ? Either<L2, R>.Left(mapLeft(_left)) : Either<L2, R>.Right(_right);

	//TODO:KILL
	internal L force() {
		assert(_isLeft);
		return _left;
	}

	/*internal bool isLeft(out L left, out R right) {
		left = _left;
		right = _right;
		return _isLeft;
	}*/

	/*internal L left {
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
	}*/
}
