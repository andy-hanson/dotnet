using Model;

using static Utils;

static class JsEmitter {
	internal static string emitToString(Module m) {
		var p = emitToProgram(m);
		return JsWriter.writeToString(p);
	}

	static Estree.Program emitToProgram(Module m) {
		var body = Arr.builder<Estree.Statement>();

		foreach (var import in m.imports)
			body.add(emitImport(m, import));

		emitClass(body, m.klass);

		body.add(assign(m.klass.loc, moduleExports, id(m.klass.loc, m.klass.name)));

		return new Estree.Program(Loc.zero, body.finish());
	}

	static Estree.Identifier baseId(string name) => id(Loc.zero, Sym.of(name));

	static readonly Estree.Identifier requireId = baseId("require");
	static Estree.Statement emitImport(Module importer, Module imported) {
		// Don't care about Loc since shouldn't be possible to get an error here.
		var loc = Loc.zero;

		var relPath = importer.fullPath().relTo(imported.fullPath());
		// Must find relative path.
		var pathStr = relPath.withoutExtension(ModuleResolver.extension).toPathString();
		Estree.Expression required = new Estree.Literal(loc, new Expr.Literal.LiteralValue.Str(pathStr));
		var require = Estree.CallExpression.of(loc, requireId, required);
		return Estree.VariableDeclaration.simple(loc, imported.name, require);
	}

	static readonly Estree.MemberExpression moduleExports = Estree.MemberExpression.simple(Loc.zero, Sym.of("module"), Sym.of("exports"));

	static readonly Estree.MemberExpression objectCreate = Estree.MemberExpression.simple(Loc.zero, Sym.of("Object"), Sym.of("create"));
	static void emitClass(Arr.Builder<Estree.Statement> body, Klass klass) {
		var loc = klass.loc;
		var klassName = klass.name;

		switch (klass.head) {
			case Klass.Head.Static _:
				body.add(Estree.VariableDeclaration.simple(loc, klassName, new Estree.ObjectExpression(loc, Arr.empty<Estree.Property>())));
				break;
			case Klass.Head.Abstract _:
				body.add(new Estree.FunctionDeclaration(loc, id(loc, klassName), Arr.empty<Estree.Pattern>(), Estree.BlockStatement.empty(loc)));
				break;
			case Klass.Head.Slots s:
				body.add(emitSlots(klass, s));
				break;
			default:
				throw TODO();
		}

		if (klass.supers.length != 0) {
			if (klass.supers.length > 1) throw TODO(); //We will need a mixer in this case.

			var super = klass.supers.only;
			// `Foo.prototype = Object.create(Super.prototype);`
			var superProto = Estree.MemberExpression.simple(super.loc, super.superClass.name, symPrototype);
			var create = Estree.CallExpression.of(super.loc, objectCreate, superProto);
			body.add(assign(super.loc, klassName, symPrototype, create));

			// Add impls
			foreach (var impl in super.impls)
				emitImpl(body, klassName, impl);
		}

		foreach (var method in klass.methods)
			emitMethod(body, klassName, method);
	}

	//mv
	static Estree.Statement assign(Loc loc, Estree.Pattern lhs, Estree.Expression rhs) =>
		new Estree.ExpressionStatement(loc, new Estree.AssignmentExpression(loc, lhs, rhs));
	static Estree.Statement assign(Loc loc, Sym a, Sym b, Estree.Expression rhs) =>
		assign(loc, Estree.MemberExpression.simple(loc, a, b), rhs);
	static Estree.Statement assign(Loc loc, Sym a, Sym b, Sym c, Estree.Expression rhs) =>
		assign(loc, Estree.MemberExpression.simple(loc, a, b, c), rhs);

	/*
	function Kls(x) {
		this.x = x;
	}
	*/
	static Estree.FunctionDeclaration emitSlots(Klass klass, Klass.Head.Slots s) {
		var patterns = s.slots.map<Estree.Pattern>(slot => id(slot.loc, slot.name));
		var statements = s.slots.map<Estree.Statement>((slot, i) => {
			var slotLoc = slot.loc;
			var id = (Estree.Identifier)patterns[i];
			var member = new Estree.MemberExpression(slotLoc, new Estree.ThisExpression(slotLoc), id);
			return assign(slotLoc, member, id);
		});
		var loc = klass.loc;
		return new Estree.FunctionDeclaration(loc, id(loc, klass.name), patterns, new Estree.BlockStatement(loc, statements));
	}

	static readonly Sym symSuperClass = Sym.of("superClass");

	static readonly Sym symPrototype = Sym.of("prototype");

	static void emitImpl(Arr.Builder<Estree.Statement> body, Sym klassName, Impl impl) =>
		body.add(emitInstanceMethodLike(impl.loc, klassName, impl.implemented.name, impl.implemented.parameters, impl.body));

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

	static Estree.Statement emitMethodWithBody(Sym klassName, Method.MethodWithBody method) =>
		method.isStatic
			// Cls.foo = function() { ... }
			? assign(method.loc, klassName, method.name, methodExpression(method.loc, method.parameters, method.body))
			: emitInstanceMethodLike(method.loc, klassName, method.name, method.parameters, method.body);

	static Estree.Statement emitInstanceMethodLike(Loc loc, Sym klassName, Sym methodName, Arr<Method.Parameter> parameters, Expr body) =>
		// Cls.prototype.foo = function() { ... }
		assign(loc, klassName, symPrototype, methodName, methodExpression(loc, parameters, body));

	static Estree.FunctionExpression methodExpression(Loc loc, Arr<Method.Parameter> parameters, Expr body) =>
		new Estree.FunctionExpression(loc, parameters.map(emitParameter), exprToBlockStatement(body));

	static Estree.Pattern emitParameter(Method.Parameter p) => id(p.loc, p.name);

	static Estree.BlockStatement exprToBlockStatement(Expr expr) {
		var parts = Arr.builder<Estree.Statement>();

		while (true) {
			var loc = expr.loc;
			switch (expr) {
				case Expr.Let l:
					var x = (Pattern.Single)l.assigned; // TODO: handle other patterns
					parts.add(Estree.VariableDeclaration.simple(loc, id(x.loc, x.name), exprToExpr(l.value)));
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
				return id(loc, p.param.name);
			case Expr.AccessLocal lo:
				return id(loc, lo.local.name);
			case Expr.Let l:
			case Expr.Seq s:
				return iife(expr);
			case Expr.Literal li:
				return new Estree.Literal(loc, li.value);
			case Expr.StaticMethodCall sm:
				var method = sm.method;
				var access = Estree.MemberExpression.simple(loc, method.klass.name, method.name);
				return new Estree.CallExpression(loc, access, sm.args.map(exprToExpr));
			case Expr.InstanceMethodCall m:
				var member = Estree.MemberExpression.simple(loc, exprToExpr(m.target), m.method.name);
				return new Estree.CallExpression(loc, member, m.args.map(exprToExpr));
			case Expr.New n:
				return new Estree.NewExpression(loc, id(loc, n.klass.name), n.args.map(exprToExpr));
			case Expr.GetSlot g:
				return Estree.MemberExpression.simple(loc, exprToExpr(g.target), g.slot.name);
			case Expr.GetMySlot g:
				return Estree.MemberExpression.simple(loc, new Estree.ThisExpression(loc), g.slot.name);
			case Expr.WhenTest w:
				return whenToExpr(w);
			default:
				throw TODO();
		}
	}

	static Estree.Identifier id(Loc loc, Sym name) =>
		new Estree.Identifier(loc, name);

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
