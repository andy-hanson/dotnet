using System.Text;

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

		var sb = new StringBuilder();
		foreach (var ch in name.str) {
			if (ch == '-')
				sb.Append('_');
			else {
				assert(isNameChar(ch));
				sb.Append(ch);
			}
		}
		return sb.ToString();
	}

	// Should look like `Foo-Bar`. Forbid `FooBar` or `Foo-bar`.
	internal static Sym unescapeTypeName(string name) {
		assert(isUpperCaseLetter(name[0]));

		var sb = new StringBuilder();
		sb.Append(name[0]);
		for (uint i = 1; i < name.Length; i++) {
			var ch = name.at(i);
			if (ch == '_') {
				sb.Append('-');
				i++;
				var ch2 = name.at(i);
				assert(isUpperCaseLetter(ch2));
				sb.Append(ch2);
			} else {
				assert(isLowerCaseLetter(ch));
				sb.Append(ch);
			}
		}
		return Sym.of(sb);
	}

	// Should look like `foo-bar`. Forbid `fooBar`.
	internal static Sym unescapeMethodName(string name) {
		if (operatorUnescapes.get(name, out var v))
			return v;

		var sb = new StringBuilder();
		foreach (var ch in name) {
			if (ch == '_')
				sb.Append('-');
			else {
				assert(isLowerCaseLetter(ch));
				sb.Append(ch);
			}
		}
		return Sym.of(sb);
	}
}
