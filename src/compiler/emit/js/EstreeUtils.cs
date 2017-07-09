static class EstreeUtils {
	internal static bool isAsync(Model.Method method) => method.effect == Model.Effect.Io;

	internal static bool isSafeMemberName(string s) {
		foreach (var ch in s) {
			if (!CharUtils.isDigit(ch) && !CharUtils.isLetter(ch))
				return false;
		}
		return true;
	}

	internal static Estree.Identifier id(Loc loc, Sym name) =>
		new Estree.Identifier(loc, name);

	internal static Estree.Expression callPossiblyAsync(Loc loc, bool @async, Estree.Expression target, Arr<Estree.Expression> args) {
		Estree.Expression call = new Estree.CallExpression(loc, target, args);
		return @async ? new Estree.AwaitExpression(loc, call) : call;
	}

	internal static Estree.Statement assign(Loc loc, Estree.Pattern lhs, Estree.Expression rhs) =>
		Estree.ExpressionStatement.of(new Estree.AssignmentExpression(loc, lhs, rhs));

	internal static Estree.Statement assign(Loc loc, Sym a, Sym b, Estree.Expression rhs) =>
		assign(loc, Estree.MemberExpression.simple(loc, a, b), rhs);

	internal static Estree.Statement assign(Loc loc, Sym a, Sym b, Sym c, Estree.Expression rhs) =>
		assign(loc, Estree.MemberExpression.simple(loc, a, b, c), rhs);
}
