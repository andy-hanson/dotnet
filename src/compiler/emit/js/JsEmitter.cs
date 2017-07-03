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
	static readonly Estree.Statement requireNzlib = importStatement(Sym.of("_"), "nzlib");

	Estree.Expression getFromLib(Loc loc, Sym id) {
		needsLib = true;
		return JsBuiltins.getFromLib(loc, id);
	}

	Estree.Program emitToProgram(Module m) {
		var body = Arr.builder<Estree.Statement>();

		// Eagerly add lib import. If we don't need this, we'll finishTail()
		body.add(requireNzlib);

		foreach (var import in m.imports)
			body.add(emitImport(m, import));

		var cls = emitClass(m.klass);
		body.add(assign(m.klass.loc, moduleExports, cls));

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
	Estree.ClassExpression emitClass(Klass klass) {
		var body = Arr.builder<Estree.MethodDefinition>();

		switch (klass.head) {
			case Klass.Head.Static _:
			case Klass.Head.Abstract _:
				// No constructor
				break;
			case Klass.Head.Slots slots:
				body.add(emitSlotsConstructor(slots, needSuperCall: klass.supers.length != 0));
				break;
			default:
				throw TODO();
		}

		Op<Estree.Expression> superClass = Op<Estree.Expression>.None;
		if (klass.supers.length != 0) {
			if (klass.supers.length > 1) throw TODO(); //We will need a mixer in this case.

			var super = klass.supers.only;
			superClass = Op.Some<Estree.Expression>(id(super.loc, super.superClass.name));

			// Add impls
			foreach (var impl in super.impls)
				body.add(emitMethodOrImpl(impl.loc, impl.implemented, impl.body, isStatic: false));
		}

		foreach (var method in klass.methods) {
			switch (method) {
				case Method.MethodWithBody mb:
					body.add(emitMethodOrImpl(method.loc, method, mb.body, method.isStatic));
					break;
				case Method.AbstractMethod a:
					// These compile to nothing -- they are abstract.
					break;
				default:
					throw unreachable();
			}
		}

		var loc = klass.loc;
		return new Estree.ClassExpression(loc, id(loc, klass.name), superClass, new Estree.ClassBody(loc, body.finish()));
	}

	//mv
	static Estree.Statement assign(Loc loc, Estree.Pattern lhs, Estree.Expression rhs) =>
		Estree.ExpressionStatement.of(new Estree.AssignmentExpression(loc, lhs, rhs));
	static Estree.Statement assign(Loc loc, Sym a, Sym b, Estree.Expression rhs) =>
		assign(loc, Estree.MemberExpression.simple(loc, a, b), rhs);
	static Estree.Statement assign(Loc loc, Sym a, Sym b, Sym c, Estree.Expression rhs) =>
		assign(loc, Estree.MemberExpression.simple(loc, a, b, c), rhs);

	static Estree.MethodDefinition emitSlotsConstructor(Klass.Head.Slots s, bool needSuperCall) {
		// constructor(x) { this.x = x; }
		var patterns = s.slots.map<Estree.Pattern>(slot => id(slot.loc, slot.name));
		var first = needSuperCall ? Op.Some(superCall(s.loc)) : Op<Estree.Statement>.None;
		var statements = s.slots.mapWithFirst<Estree.Statement>(first, (slot, i) => {
			var slotLoc = slot.loc;
			var id = (Estree.Identifier)patterns[i];
			var member = Estree.MemberExpression.notComputed(slotLoc, new Estree.ThisExpression(slotLoc), id);
			return assign(slotLoc, member, id);
		});
		return Estree.MethodDefinition.constructor(s.loc, patterns, statements);
	}

	static Estree.Statement superCall(Loc loc) =>
		Estree.ExpressionStatement.of(Estree.CallExpression.of(loc, new Estree.Super(loc)));

	Estree.MethodDefinition emitMethodOrImpl(Loc loc, Method method, Expr body, bool isStatic) =>
		Estree.MethodDefinition.method(loc, method.name, method.parameters.map(emitParameter), exprToBlockStatement(body), isStatic);

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
				return Estree.ExpressionStatement.of(exprToExpr(expr));
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

	Estree.Expression iife(Expr expr) {
		var fn = new Estree.ArrowFunctionExpression(expr.loc, Arr.empty<Estree.Pattern>(), exprToBlockStatement(expr));
		return new Estree.CallExpression(expr.loc, fn, Arr.empty<Estree.Expression>());
	}

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
				return emitStaticMethodCall(sm);
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

	Estree.Expression emitStaticMethodCall(Expr.StaticMethodCall sm) =>
		JsBuiltins.emitStaticMethodCall(ref needsLib, sm.method, sm.loc, sm.args.map(exprToExpr));

	Estree.Expression emitInstanceMethodCall(Expr.InstanceMethodCall m) =>
		JsBuiltins.emitInstanceMethodCall(ref needsLib, m.method, m.loc, exprToExpr(m.target), m.args.map(exprToExpr));

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
