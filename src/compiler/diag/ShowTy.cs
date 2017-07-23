using Model;

static class ShowTy {
	internal static StringMaker show(StringMaker s, Ty ty) {
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

	static void showPlainTy(StringMaker s, Ty.PlainTy p) {
		switch (p.effect) {
			case Effect.Pure:
				break;
			default:
				s.add(p.effect.show());
				s.add(' ');
				break;
		}

		s.add(p.cls.name.str);
	}
}
