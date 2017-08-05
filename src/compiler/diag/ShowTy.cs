using Model;
using static Utils;

static class ShowTy {
	internal static S showTy<S>(this S s, Ty ty) where S : Shower<S> {
		doShowTy(s, ty);
		return s;
	}

	static void doShowTy<S>(S s, Ty ty) where S : Shower<S> {
		switch (ty) {
			case BogusTy _:
				s.add("<error>");
				break;
			case PlainTy p:
				showPlainTy(s, p);
				break;
			default:
				throw unreachable();
		}
	}

	static void showPlainTy<S>(this S s, PlainTy pt) where S : Shower<S> {
		var (effect, (classDeclaration, typeArguments)) = pt;

		if (!effect.isPure) {
			s.add(effect);
			s.add(' ');
		}

		s.add(classDeclaration.name.str);

		if (typeArguments.any) {
			s.add('[');
			s.join(typeArguments, doShowTy);
			s.add(']');
		}
	}
}
