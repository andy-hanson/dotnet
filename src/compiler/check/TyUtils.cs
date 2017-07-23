using Model;

using static Utils;

static class TyUtils {
	/** Returns None to signify an error. */
	internal static Op<Ty> getCombinedType(Ty a, Ty b) {
		switch (a) {
			case Ty.Bogus _:
				return Op.Some(b); // No need for any additional errors.
			case Ty.PlainTy plainA:
				switch (b) {
					case Ty.Bogus _:
						return Op.Some(a);
					case Ty.PlainTy plainB:
						return getCombinedPlainTy(plainA, plainB);
					default:
						throw unreachable();
				}
			default:
				throw unreachable();
		}
	}

	static Op<Ty> getCombinedPlainTy(Ty.PlainTy a, Ty.PlainTy b) {
		var effect = a.effect.minCommonEffect(b.effect);
		return a.cls.fastEquals(b.cls) ? Op.Some<Ty>(Ty.of(effect, a.cls)) : Op<Ty>.None;
	}

	internal static bool isAssignable(Ty expectedTy, Ty actualTy) {
		switch (expectedTy) {
			case Ty.Bogus _:
				return true;
			case Ty.PlainTy expectedPlainTy:
				switch (actualTy) {
					case Ty.Bogus _:
						return true;
					case Ty.PlainTy actualPlainTy:
						return isPlainTySubtype(expectedPlainTy, actualPlainTy);
					default:
						throw unreachable();
				}
			default:
				throw unreachable();
		}
	}

	static bool isPlainTySubtype(Ty.PlainTy expected, Ty.PlainTy actual) =>
		actual.effect.contains(expected.effect) && // Pure `Foo` can't be assigned to `io Foo`.
			isSubclass(expected.cls, actual.cls);

	static bool isSubclass(ClsRef expected, ClsRef actual) {
		if (expected.fastEquals(actual))
			return true;
		foreach (var s in actual.supers) {
			if (isSubclass(expected, s.superClass))
				return true;
		}
		return false;
	}

	internal static Ty getTyWithNarrowedEffect(Effect effect, Ty ty) {
		switch (ty) {
			case Ty.Bogus _:
				return Ty.bogus;
			case Ty.PlainTy p:
				return Ty.of(effect.minCommonEffect(p.effect), p.cls);
			default:
				throw unreachable();
		}
	}
}
