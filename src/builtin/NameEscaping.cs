using static CharUtils;
using static Utils;

static class NameEscaping {
	static Dict<Sym, string> operatorEscapes = Dict.of(
		("==", "_eq"),
		("+", "_add"),
		("-", "_sub"),
		("*", "_mul"),
		("/", "_div"),
		("^", "_pow")).mapKeys(Sym.of);
	static Dict<string, Sym> operatorUnescapes = operatorEscapes.reverse();

	internal static string escapeName(Sym name) {
		if (operatorEscapes.get(name, out var sym))
			return sym;

		var sb = StringMaker.create();
		foreach (var ch in name.str) {
			if (ch == '-')
				sb.add('_');
			else {
				assert(isNameChar(ch));
				sb.add(ch);
			}
		}
		return sb.finish();
	}

	// Should look like `Foo-Bar`. Forbid `FooBar` or `Foo-bar`.
	internal static Sym unescapeTypeName(string name) {
		assert(isUpperCaseLetter(name[0]));

		var sb = StringMaker.create();
		sb.add(name[0]);
		for (uint i = 1; i < name.Length; i++) {
			var ch = name.at(i);
			if (ch == '_') {
				sb.add('-');
				i++;
				var ch2 = name.at(i);
				assert(isUpperCaseLetter(ch2));
				sb.add(ch2);
			} else {
				assert(isLowerCaseLetter(ch));
				sb.add(ch);
			}
		}
		return sb.finishSym();
	}

	// Should look like `foo-bar`. Forbid `fooBar`.
	internal static Sym unescapeMethodName(string name) {
		if (operatorUnescapes.get(name, out var v))
			return v;

		var sb = StringMaker.create();
		foreach (var ch in name) {
			if (ch == '_')
				sb.add('-');
			else {
				assert(isLowerCaseLetter(ch));
				sb.add(ch);
			}
		}
		return sb.finishSym();
	}
}
