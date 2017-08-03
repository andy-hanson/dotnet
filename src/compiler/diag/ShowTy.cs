using Model;

static class ShowTy {
	internal static S showTy<S>(this S s, Ty ty) where S : Shower<S> {
		switch (ty) {
			case Ty.Bogus _:
				s.add("<error>");
				break;
			case Ty.PlainTy p:
				showPlainTy(s, p);
				break;
		}
		return s;
	}

	static void showPlainTy<S>(this S s, Ty.PlainTy p) where S : Shower<S> {
		if (!p.effect.isPure) {
			s.add(p.effect.show);
			s.add(' ');
		}

		s.add(p.cls.name.str);
	}
}
