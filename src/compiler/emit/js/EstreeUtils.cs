using static Utils;

static class EstreeUtils {
	internal static bool isAsync(Model.MethodDeclaration method) =>
		method.selfEffect.canIo || method.parameters.some(p => canPerformIo(p.ty));

	internal static bool canPerformIo(Model.Ty ty) {
		switch (ty) {
			case Model.TypeParameter tp:
				// Type parameter might be `io`, but we can't call it's io methods since we're just using it as a type parameter here.
				return false;

			case Model.PlainTy p:
				return p.effect.canIo;

			case Model.BogusTy _:
			default:
				throw unreachable();
		}
	}

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
