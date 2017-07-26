static class EstreeUtils {
	internal static bool isAsync(Model.Method method) =>
		method.selfEffect.canIo || method.parameters.some(p => p.ty.effect.canIo);

	internal static Estree.Identifier id(Loc loc, string name) =>
		new Estree.Identifier(loc, name);

	internal static Estree.Expression callPossiblyAsync(Loc loc, bool @async, Estree.Expression target, Arr<Estree.Expression> args) {
		Estree.Expression call = new Estree.CallExpression(loc, target, args);
		return @async ? new Estree.AwaitExpression(loc, call) : call;
	}

	internal static Estree.Statement assign(Loc loc, Estree.Pattern lhs, Estree.Expression rhs) =>
		Estree.ExpressionStatement.of(new Estree.AssignmentExpression(loc, lhs, rhs));

	internal static Estree.Statement assign(Loc loc, string a, string b, Estree.Expression rhs) =>
		assign(loc, Estree.MemberExpression.simple(loc, a, b), rhs);

	internal static Estree.Statement assign(Loc loc, string a, string b, string c, Estree.Expression rhs) =>
		assign(loc, Estree.MemberExpression.simple(loc, a, b, c), rhs);
}
