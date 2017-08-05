using System;

using Diag;
using Diag.CheckExprDiags;
using Model;

using static Utils;

static class TyUtils {
	/** Returns None to signify an error. */
	internal static Op<Ty> getCommonCompatibleType(Ty a, Ty b) {
		switch (a) {
			case BogusTy _:
				return Op.Some(b); // No need for any additional errors.

			case PlainTy plainA:
				switch (b) {
					case BogusTy _:
						return Op.Some(a);
					case PlainTy plainB:
						return getCombinedPlainTy(plainA, plainB);
					default:
						throw unreachable();
				}

			default:
				throw unreachable();
		}
	}

	static Op<Ty> getCombinedPlainTy(PlainTy a, PlainTy b) =>
		a.instantiatedClass.fastEquals(b.instantiatedClass)
			? Op.Some<Ty>(Ty.of(a.effect.minCommonEffect(b.effect), a.instantiatedClass))
			: Op<Ty>.None;

	internal static bool isAssignable(Ty expectedTy, Ty actualTy) {
		switch (expectedTy) {
			case BogusTy _:
				return true;
			case TypeParameter tp:
				// Only thing assignable to a type parameter is itself.
				return tp.fastEquals(actualTy);
			case PlainTy expectedPlainTy:
				switch (actualTy) {
					case BogusTy _:
						return true;
					case PlainTy actualPlainTy:
						return isPlainTySubtype(expectedPlainTy, actualPlainTy);
					default:
						throw unreachable();
				}
			default:
				throw unreachable();
		}
	}

	static bool isPlainTySubtype(PlainTy expected, PlainTy actual) =>
		actual.effect.contains(expected.effect) && // Pure `Foo` can't be assigned to `io Foo`.
			isSubclass(expected.instantiatedClass, actual.instantiatedClass);

	static bool isSubclass(InstCls expected, InstCls actual) {
		// TODO: generics variance. Until then, only a subtype if every generic parameter is *exactly* equal.
		if (expected.fastEquals(actual))
			return true;

		foreach (var s in actual.classDeclaration.supers) {
			var instantiatedSuperClass = instantiateInstCls(s.superClass, TyReplacer.ofInstCls(actual));
			if (isSubclass(expected, instantiatedSuperClass))
				return true;
		}

		return false;
	}

	/*
	When accessing a slot, we need to simultaneously instantiate types *and* narrow effects.
	Say we have:
		class Foo[T]
			slots
				val io Console console;
				val T a;
				val set MList[T] b;
				val MList[io Console] c;

		fun f(get Foo[io Console] foo)
			foo.console || This is a `get Console`, because we didn't explicitly specify an effect.
			foo.a || This is an `io Console`. Its type comes from the type parameter.
			foo.b || This is a `get MList[io Console]`. Don't narrow `io Console` because that was the type argument.
			foo.c || Forbidden. Can't narrow to pure `Console` because that would allow us to *add* a pure Console, but we must only add `io Console`s.

		The rule is, we always *either* instantiate a type parameter *xor* narrow an effect.
	*/
	internal static Ty instantiateTypeAndNarrowEffects(Effect narrowedEffect, Ty ty, TyReplacer replacer, Loc loc, Arr.Builder<Diagnostic> diags) {
		switch (ty) {
			case TypeParameter p:
				// Either it remains a type parameter, or it is replaced with *no* narrowing of effects.
				return replacer.replaceOrSame(p);
			case PlainTy pl: {
				var (originalEffect, instCls) = pl;
				return PlainTy.of(
					originalEffect.minCommonEffect(narrowedEffect),
					instantiateInstClsAndForbidEffects(narrowedEffect, instCls, replacer, loc, diags));
			}
			case BogusTy _:
				return Ty.bogus;
			default:
				throw unreachable();
		}
	}

	/** `instantiateTypeAndNarrowEffects` without instantiation. */
	internal static Ty narrowEffects(Effect narrowedEffect, Ty ty, Loc loc, Arr.Builder<Diagnostic> diags) =>
		instantiateTypeAndNarrowEffects(narrowedEffect, ty, TyReplacer.doNothingReplacer, loc, diags);

	static Ty instantiateTypeAndForbidEffects(Effect narrowedEffect, Ty ty, TyReplacer replacer, Loc loc, Arr.Builder<Diagnostic> diags) {
		switch (ty) {
			case TypeParameter p:
				return replacer.replaceOrSame(p);
			case PlainTy pl:
				var (effect, instCls) = pl;
				if (!narrowedEffect.contains(effect)) {
					diags.add(new Diagnostic(loc, new CantNarrowEffectOfNonCovariantGeneric(narrowedEffect, pl)));
					return Ty.bogus;
				}
				return PlainTy.of(effect, instantiateInstClsAndForbidEffects(narrowedEffect, instCls, replacer, loc, diags));
			case BogusTy _:
				return Ty.bogus;
			default:
				throw unreachable();
		}
	}

	static InstCls instantiateInstClsAndForbidEffects(Effect narrowedEffect, InstCls instCls, TyReplacer replacer, Loc loc, Arr.Builder<Diagnostic> diags) =>
		mapInstCls(instCls, arg => instantiateTypeAndForbidEffects(narrowedEffect, arg, replacer, loc, diags));

	static InstCls instantiateInstCls(InstCls instCls, TyReplacer replacer) =>
		mapInstCls(instCls, arg => instantiateType(arg, replacer));

	static InstCls mapInstCls(InstCls instCls, Func<Ty, Ty> replaceArg) {
		var (classDeclaration, typeArguments) = instCls;
		var newTypeArguments = typeArguments.map(replaceArg);
		return InstCls.of(classDeclaration, newTypeArguments);
	}

	internal static Ty instantiateType(Ty ty, TyReplacer replacer) {
		switch (ty) {
			case TypeParameter p:
				return replacer.replaceOrSame(p);
			case PlainTy pl: {
				var (effect, instCls) = pl;
				return PlainTy.of(pl.effect, instantiateInstCls(instCls, replacer));
			}
			case BogusTy _:
				return Ty.bogus;
			default:
				throw unreachable();
		}
	}
}
