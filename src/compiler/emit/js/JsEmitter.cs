using Model;

using static Utils;

sealed class JsEmitter {
	internal static string emitToString(Module m) {
		var p = new JsEmitter().emitToProgram(m);
		return JsWriter.writeToString(p);
	}

	private bool needsLib = false;

	static Estree.Identifier baseId(string name) => id(Loc.zero, Sym.of(name));
	static readonly Estree.Identifier requireId = baseId("require");
	static readonly Estree.Identifier idNzlib = baseId("_");
	static readonly Estree.Statement requireNzlib = importStatement(Sym.of("_"), "nzlib");

	Estree.Expression getFromLib(Loc loc, Sym id) {
		needsLib = true;
		return Estree.MemberExpression.simple(loc, idNzlib, id);
	}

	Estree.Program emitToProgram(Module m) {
		var body = Arr.builder<Estree.Statement>();

		// Eagerly add lib import. If we don't need this, we'll finishTail()
		body.add(requireNzlib);

		foreach (var import in m.imports)
			body.add(emitImport(m, import));

		emitClass(body, m.klass);

		body.add(assign(m.klass.loc, moduleExports, id(m.klass.loc, m.klass.name)));

		return new Estree.Program(Loc.zero, needsLib ? body.finish() : body.finishTail());
	}


	Estree.Statement emitImport(Module importer, Module imported) {
		var relPath = importer.fullPath().relTo(imported.fullPath());
		// Must find relative path.
		var pathStr = relPath.withoutExtension(ModuleResolver.extension).toPathString();
		return importStatement(imported.name, pathStr);
	}

	static Estree.Statement importStatement(Sym importedName, string importedPath) {
		var required = new Estree.Literal(Loc.zero, new Expr.Literal.LiteralValue.Str(importedPath));
		var require = Estree.CallExpression.of(Loc.zero, requireId, required);
		return Estree.VariableDeclaration.simple(Loc.zero, importedName, require);
	}

	static readonly Estree.MemberExpression moduleExports = Estree.MemberExpression.simple(Loc.zero, Sym.of("module"), Sym.of("exports"));

	static readonly Estree.MemberExpression objectCreate = Estree.MemberExpression.simple(Loc.zero, Sym.of("Object"), Sym.of("create"));
	void emitClass(Arr.Builder<Estree.Statement> body, Klass klass) {
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

	static Estree.FunctionDeclaration emitSlots(Klass klass, Klass.Head.Slots s) {
		// function Kls(x) { this.x = x; }
		var patterns = s.slots.map<Estree.Pattern>(slot => id(slot.loc, slot.name));
		var statements = s.slots.map<Estree.Statement>((slot, i) => {
			var slotLoc = slot.loc;
			var id = (Estree.Identifier)patterns[i];
			var member = Estree.MemberExpression.notComputed(slotLoc, new Estree.ThisExpression(slotLoc), id);
			return assign(slotLoc, member, id);
		});
		var loc = klass.loc;
		return new Estree.FunctionDeclaration(loc, id(loc, klass.name), patterns, new Estree.BlockStatement(loc, statements));
	}

	static readonly Sym symSuperClass = Sym.of("superClass");

	static readonly Sym symPrototype = Sym.of("prototype");

	void emitImpl(Arr.Builder<Estree.Statement> body, Sym klassName, Impl impl) =>
		body.add(emitInstanceMethodLike(impl.loc, klassName, impl.implemented.name, impl.implemented.parameters, impl.body));

	void emitMethod(Arr.Builder<Estree.Statement> body, Sym klassName, Method method) {
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

	Estree.Statement emitMethodWithBody(Sym klassName, Method.MethodWithBody method) =>
		method.isStatic
			// Cls.foo = function() { ... }
			? assign(method.loc, klassName, method.name, methodExpression(method.loc, method.parameters, method.body))
			: emitInstanceMethodLike(method.loc, klassName, method.name, method.parameters, method.body);

	Estree.Statement emitInstanceMethodLike(Loc loc, Sym klassName, Sym methodName, Arr<Method.Parameter> parameters, Expr body) =>
		// Cls.prototype.foo = function() { ... }
		assign(loc, klassName, symPrototype, methodName, methodExpression(loc, parameters, body));

	Estree.FunctionExpression methodExpression(Loc loc, Arr<Method.Parameter> parameters, Expr body) =>
		new Estree.FunctionExpression(loc, parameters.map(emitParameter), exprToBlockStatement(body));

	static Estree.Pattern emitParameter(Method.Parameter p) => id(p.loc, p.name);

	Estree.BlockStatement exprToBlockStatement(Expr expr) {
		var parts = Arr.builder<Estree.Statement>();
		writeExprToBlockStatement(parts, expr);
		return new Estree.BlockStatement(expr.loc, parts.finish());
	}

	Estree.BlockStatement exprToBlockStatement(Expr expr, Estree.Statement firstStatement) {
		var parts = Arr.builder<Estree.Statement>();
		parts.add(firstStatement);
		writeExprToBlockStatement(parts, expr);
		return new Estree.BlockStatement(expr.loc, parts.finish());
	}

	void writeExprToBlockStatement(Arr.Builder<Estree.Statement> parts, Expr expr) {
		while (true) {
			var loc = expr.loc;
			switch (expr) {
				case Expr.Let l:
					var x = (Pattern.Single)l.assigned; // TODO: handle other patterns
					parts.add(Estree.VariableDeclaration.simple(loc, id(x.loc, x.name), exprToExpr(l.value)));
					expr = l.then;
					break;
				case Expr.Seq s:
					parts.add(exprToStatement(s.action));
					expr = s.then;
					break;
				case Expr.Assert a:
					parts.add(assertToStatement(a));
					return;
				case Expr.Try t:
					parts.add(tryToStatement(t));
					return;
				case Expr.Literal l:
					if (l.value is Expr.Literal.LiteralValue.Pass)
						return;
					goto default;
				default:
					parts.add(new Estree.ReturnStatement(expr.loc, exprToExpr(expr)));
					return;
			}
		}

	}

	Estree.Statement exprToStatement(Expr expr) {
		switch (expr) {
			case Expr.Assert a:
				return assertToStatement(a);
			default:
				return new Estree.ExpressionStatement(expr.loc, exprToExpr(expr));
		}
	}

	Estree.Statement assertToStatement(Expr.Assert a) {
		var loc = a.loc;
		var idAssertionException = getFromLib(loc, Sym.of(nameof(Builtins.AssertionException)));
		var fail = new Estree.ThrowStatement(loc,
			new Estree.NewExpression(loc, idAssertionException, Arr.of<Estree.Expression>()));
		var notAsserted = Estree.UnaryExpression.not(a.asserted.loc, exprToExpr(a.asserted));
		return new Estree.IfStatement(loc, notAsserted, fail);
	}

	Estree.Statement tryToStatement(Expr.Try t) {
		var do_ = exprToBlockStatement(t.do_);
		var catch_ = t.catch_.get(out var c) ? Op.Some(catchToCatch(c)) : Op<Estree.CatchClause>.None;
		var finally_ = t.finally_.get(out var f) ? Op.Some(exprToBlockStatement(f)) : Op<Estree.BlockStatement>.None;
		return new Estree.TryStatement(t.loc, do_, catch_, finally_);
	}

	Estree.CatchClause catchToCatch(Expr.Try.Catch c) {
		var loc = c.loc;
		var caught = id(c.caught.loc, c.caught.name);

		// `if (!(e is ExceptionType)) throw e;`
		var isInstance = new Estree.BinaryExpression(loc, "instanceof", caught, accessTy(loc, c.exceptionTy));
		var test = Estree.UnaryExpression.not(loc, isInstance);
		var first = new Estree.IfStatement(loc, test, new Estree.ThrowStatement(c.loc, caught));

		var caughtBlock = exprToBlockStatement(c.then, first);
		return new Estree.CatchClause(loc, caught, caughtBlock);
	}

	Estree.Expression accessTy(Loc loc, Ty ty) {
		switch (ty) {
			case Klass k:
				return id(loc, k.name);
			case BuiltinClass b:
				return getFromLib(loc, b.name);
			default:
				throw TODO();
		}
	}

	Estree.Expression iife(Expr expr) =>
		throw TODO();//new Estree.ArrowFunctionExpression(expr.loc, Arr.empty<Estree.Pattern>(), exprToBlockStatement(expr));

	Estree.Expression exprToExpr(Expr expr) {
		var loc = expr.loc;
		switch (expr) {
			case Expr.AccessParameter p:
				return id(loc, p.param.name);
			case Expr.AccessLocal lo:
				return id(loc, lo.local.name);
			case Expr.Let l:
			case Expr.Seq s:
			case Expr.Assert a:
			case Expr.Try t:
				return iife(expr);
			case Expr.Literal li:
				return new Estree.Literal(loc, li.value);
			case Expr.StaticMethodCall sm:
				var method = sm.method;
				var access = Estree.MemberExpression.simple(loc, method.klass.name, method.name);
				return new Estree.CallExpression(loc, access, sm.args.map(exprToExpr));
			case Expr.InstanceMethodCall m:
				return emitInstanceMethodCall(m);
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

	Estree.Expression emitInstanceMethodCall(Expr.InstanceMethodCall m) {
		var loc = m.loc;
		var target = exprToExpr(m.target);
		var args = m.args.map(exprToExpr);

		//TODO: more general solution
		var eq = BuiltinClass.Int.membersMap[Sym.of("==")];
		if (m.method.Equals(eq)) {
			var other = args.only;
			return new Estree.BinaryExpression(loc, "===", target, other);
		}

		var member = Estree.MemberExpression.simple(loc, target, m.method.name);
		return new Estree.CallExpression(loc, member, args);
	}

	static Estree.Identifier id(Loc loc, Sym name) =>
		new Estree.Identifier(loc, name);

	Estree.Expression whenToExpr(Expr.WhenTest w) {
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
