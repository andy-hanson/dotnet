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

		emitClass(body, m.klass);

		body.add(moduleExportsEquals(new Estree.Identifier(m.klass.loc, m.klass.name)));

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
		return Estree.VariableDeclaration.simple(loc, imported.name, require);
	}

	static readonly Estree.MemberExpression moduleExports = new Estree.MemberExpression(Loc.zero, baseId("module"), baseId("exports"));
	static Estree.Statement moduleExportsEquals(Estree.Expression e) =>
		new Estree.ExpressionStatement(
			Loc.zero,
			new Estree.AssignmentExpression(Loc.zero, moduleExports, e));

	static void emitClass(Arr.Builder<Estree.Statement> body, Klass klass) {
		var loc = klass.loc;
		var name = klass.name;

		if (klass.supers.length != 0) throw TODO();

		switch (klass.head) {
			case Klass.Head.Static _:
				body.add(Estree.VariableDeclaration.simple(loc, name, new Estree.ObjectExpression(loc, Arr.empty<Estree.Property>())));
				break;
			case Klass.Head.Abstract _:
				body.add(new Estree.FunctionDeclaration(loc, new Estree.Identifier(loc, name), Arr.empty<Estree.Pattern>(), Estree.BlockStatement.empty(loc)));
				break;
			case Klass.Head.Slots s:
				//s.isClass;
				//s.slots;
				throw TODO();
			default:
				throw TODO();
		}

		foreach (var method in klass.methods)
			emitMethod(body, name, method);
	}

	static readonly Sym symSuperClass = Sym.of("superClass");

	static readonly Sym symPrototype = Sym.of("prototype");
	static void emitMethod(Arr.Builder<Estree.Statement> body, Sym klassName, Method method) {
		switch (method) {
			case Method.MethodWithBody mb:
				body.add(emitMethodWithBody(klassName, mb));
				break;
			case Method.AbstractMethod a:
				// These compile to nothing -- they are abstract.
				break;
			default:
				throw unreachable();
		}
	}

	static Estree.Statement emitMethodWithBody(Sym klassName, Method.MethodWithBody method) {
		var loc = method.loc;
		var lhs = method.isStatic
			? Estree.MemberExpression.simple(loc, klassName, method.name)
			: Estree.MemberExpression.simple(loc, klassName, symPrototype, method.name);
		return new Estree.ExpressionStatement(loc, new Estree.AssignmentExpression(loc, lhs, methodExpression(method)));
	}

	static Estree.FunctionExpression methodExpression(Method.MethodWithBody method) {
		var body = exprToBlockStatement(method.body);
		return new Estree.FunctionExpression(method.loc, method.parameters.map(emitParameter), body);
	}

	static Estree.Pattern emitParameter(Method.Parameter p) => new Estree.Identifier(p.loc, p.name);

	static Estree.BlockStatement exprToBlockStatement(Expr expr) {
		var parts = Arr.builder<Estree.Statement>();

		while (true) {
			var loc = expr.loc;
			switch (expr) {
				case Expr.Let l:
					var x = (Pattern.Single)l.assigned; // TODO: handle other patterns
					parts.add(Estree.VariableDeclaration.simple(loc, new Estree.Identifier(x.loc, x.name), exprToExpr(l.value)));
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
			case Expr.InstanceMethodCall m:
				var member = new Estree.MemberExpression(loc, exprToExpr(m.target), new Estree.Identifier(m.loc, m.method.name));
				return new Estree.CallExpression(loc, member, m.args.map(exprToExpr));
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
