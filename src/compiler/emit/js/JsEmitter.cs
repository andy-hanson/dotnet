using Model;

using static Utils;

class JsEmitter {
	internal static string emitToString(Module m) {
		var p = emitToProgram(m);
		return JsWriter.writeToString(p);
	}


	static Estree.Program emitToProgram(Module m) {
		var body = Arr.builder<Estree.Statement>();

		foreach (var import in m.imports)
			body.add(emitImport(m, import));

		body.add(moduleExportsEquals(emitClass(m.klass)));

		return new Estree.Program(Loc.zero, body.finish());
	}

	static Estree.Identifier baseId(string name) => new Estree.Identifier(Loc.zero, Sym.of(name));

	static readonly Estree.Identifier requireId = baseId("require");
	static Estree.Statement emitImport(Module importer, Module imported) {
		// Don't care about Loc since shouldn't be possible to get an error here.
		var loc = Loc.zero;
		// Must find relative path.
		var relPath = importer.fullPath().relTo(imported.fullPath());
		Estree.Expression required = new Estree.Literal(loc, new Expr.Literal.LiteralValue.Str(relPath.ToString()));
		var require = new Estree.CallExpression(loc, requireId, Arr.of(required));
		return Estree.VariableDeclaration.constant(loc, new Estree.Identifier(loc, imported.name), require);
	}

	static readonly Estree.MemberExpression moduleExports = new Estree.MemberExpression(Loc.zero, baseId("module"), baseId("exports"));
	static Estree.Statement moduleExportsEquals(Estree.Expression e) =>
		new Estree.ExpressionStatement(
			Loc.zero,
			new Estree.AssignmentExpression(Loc.zero, moduleExports, e));

	static Estree.ClassExpression emitClass(Klass klass) {
		var methods = klass.methods.map(emitMethod);
		var body = new Estree.ClassBody(klass.loc, methods);
		return new Estree.ClassExpression(klass.loc, new Estree.Identifier(klass.loc, klass.name), Op<Estree.Expression>.None, body);
	}

	static Estree.MethodDefinition emitMethod(Method.MethodWithBody method) {
		var pms = method.parameters.map(emitParameter);
		var body = exprToBlockStatement(method.body);
		var fn = new Estree.FunctionExpression(method.loc, pms, body);
		return new Estree.MethodDefinition(method.loc, new Estree.Identifier(method.loc, method.name), fn, "method", method.isStatic);
	}

	static Estree.Pattern emitParameter(Method.Parameter p) => new Estree.Identifier(p.loc, p.name);

	static Estree.BlockStatement exprToBlockStatement(Expr expr) {
		var parts = Arr.builder<Estree.Statement>();

		while (true) {
			var loc = expr.loc;
			switch (expr) {
				case Expr.Let l:
					var x = (Pattern.Single) l.assigned; // TODO: handle other patterns
					parts.add(Estree.VariableDeclaration.constant(loc, new Estree.Identifier(x.loc, x.name), exprToExpr(l.value)));
					expr = l.then;
					break;
				case Expr.Seq s:
					parts.add(new Estree.ExpressionStatement(s.action.loc, exprToExpr(s.action)));
					expr = s.then;
					break;
				case Expr.Literal l:
					if (l.value is Expr.Literal.LiteralValue.Pass)
						goto end;
					goto default;
				default:
					parts.add(new Estree.ReturnStatement(expr.loc, exprToExpr(expr)));
					goto end;
			}
		}

		end:
		return new Estree.BlockStatement(expr.loc, parts.finish());
	}

	static Estree.Expression iife(Expr expr) =>
		new Estree.ArrowFunctionExpression(expr.loc, Arr.empty<Estree.Pattern>(), exprToBlockStatement(expr));

	static Estree.Expression exprToExpr(Expr expr) {
		var loc = expr.loc;
		switch (expr) {
			case Expr.AccessParameter p:
				return new Estree.Identifier(loc, p.param.name);
			case Expr.AccessLocal lo:
				return new Estree.Identifier(loc, lo.local.name);
			case Expr.Let l:
			case Expr.Seq s:
				return iife(expr);
			case Expr.Literal li:
				return new Estree.Literal(loc, li.value);
			case Expr.StaticMethodCall sm:
				var method = sm.method;
				var access = new Estree.MemberExpression(loc, new Estree.Identifier(loc, method.klass.name), new Estree.Identifier(loc, method.name));
				return new Estree.CallExpression(loc, access, sm.args.map(exprToExpr));
			case Expr.GetSlot g:
				return new Estree.MemberExpression(loc, exprToExpr(g.target), new Estree.Identifier(g.loc, g.slot.name));
			case Expr.WhenTest w:
				return whenToExpr(w);
			default:
				throw TODO();
		}
	}

	static Estree.Expression whenToExpr(Expr.WhenTest w) {
		var cases = w.cases;

		// Build it backwards.
		var res = exprToExpr(w.elseResult);
		for (var i = cases.length - 1; i != 0; i--) {
			var kase = cases[i];
			res = new Estree.ConditionalExpression(kase.loc,  exprToExpr(kase.test), exprToExpr(kase.result), res);
		}

		return res;
	}
}
