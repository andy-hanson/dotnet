using Model;

using static EstreeUtils;
using static Utils;

sealed class JsExprEmitter {
	internal bool needsLib = false;
	readonly Sym currentClassName;
	readonly bool thisMethodIsAsync;

	internal static Estree.BlockStatement emitMethodBody(ref bool needsLib, Sym className, bool @async, Expr body) {
		var ee = new JsExprEmitter(className, @async);
		var res = ee.exprToBlockStatement(body);
		if (ee.needsLib) needsLib = true;
		return res;
	}

	JsExprEmitter(Sym currentClassName, bool thisMethodIsAsync) {
		this.currentClassName = currentClassName;
		this.thisMethodIsAsync = thisMethodIsAsync;
	}

	Estree.Expression getFromLib(Loc loc, Sym id) {
		needsLib = true;
		return JsBuiltins.getFromLib(loc, id);
	}

	Estree.BlockStatement exprToBlockStatement(Expr expr) =>
		new Estree.BlockStatement(expr.loc, writeExprToBlockStatement(expr).finish());

	Estree.BlockStatement exprToBlockStatement(Expr expr, Estree.Statement firstStatement) =>
		new Estree.BlockStatement(expr.loc, writeExprToBlockStatement(expr).finishWithFirst(firstStatement));

	Arr.Builder<Estree.Statement> writeExprToBlockStatement(Expr expr) {
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
					parts.add(exprToStatementWorker(s.action, needReturn: false));
					expr = s.then;
					break;
				case Expr.Literal l:
					if (l.value is LiteralValue.Pass)
						return parts;
					goto default;
				default:
					parts.add(exprToStatementWorker(expr, needReturn: true));
					return parts;
			}
		}
	}

	Estree.Statement exprToReturnStatementOrBlock(Expr expr) {
		switch (expr) {
			case Expr.Let _:
			case Expr.Seq _:
				return exprToBlockStatement(expr);
			case Expr.Literal l:
				if (l.value is LiteralValue.Pass)
					return new Estree.BlockStatement(l.loc, Arr.empty<Estree.Statement>());
				goto default;
			default:
				return exprToStatementWorker(expr, needReturn: true);
		}
	}

	// e.g. `return f(1)` or `if (so) return 1; else return 2;`
	Estree.Statement exprToStatementWorker(Expr expr, bool needReturn) {
		switch (expr) {
			case Expr.Assert a:
				return assertToStatement(a);
			case Expr.WhenTest w:
				return whenToStatement(w);
			case Expr.Try t:
				return tryToStatement(t);
			default:
				var e = exprToExpr(expr);
				return needReturn ? Estree.ReturnStatement.of(e) : (Estree.Statement)Estree.ExpressionStatement.of(e);
		}
	}

	Estree.Statement assertToStatement(Expr.Assert a) {
		var loc = a.loc;
		var idAssertionException = getFromLib(loc, Sym.of(nameof(Builtins.AssertionException)));
		var fail = new Estree.ThrowStatement(loc,
			new Estree.NewExpression(loc, idAssertionException, Arr.of<Estree.Expression>()));
		var notAsserted = negate(exprToExpr(a.asserted));
		return new Estree.IfStatement(loc, notAsserted, fail);
	}

	static Dict<string, string> inverseOperators = Dict.of(
		("===", "!=="),
		("!==", "==="),
		("<", ">="),
		("<=", ">"),
		(">=", "<"),
		(">", "<="));
	static Estree.Expression negate(Estree.Expression e) {
		switch (e) {
			case Estree.BinaryExpression b:
				if (inverseOperators.get(b.@operator, out var inverse))
					return new Estree.BinaryExpression(b.loc, "!==", b.left, b.right);
				goto default;
			case Estree.UnaryExpression u:
				if (u.@operator == "!")
					return u.argument;
				goto default;
			default:
				return Estree.UnaryExpression.not(e.loc, e);
		}
	}

	Estree.Statement tryToStatement(Expr.Try t) {
		var do_ = exprToBlockStatement(t._do);
		var catch_ = t._catch.get(out var c) ? Op.Some(catchToCatch(c)) : Op<Estree.CatchClause>.None;
		var finally_ = t._finally.get(out var f) ? Op.Some(exprToBlockStatement(f)) : Op<Estree.BlockStatement>.None;
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
		var fn = new Estree.ArrowFunctionExpression(expr.loc, thisMethodIsAsync, Arr.empty<Estree.Pattern>(), exprToBlockStatement(expr));
		return callPossiblyAsync(expr.loc, thisMethodIsAsync, fn, Arr.empty<Estree.Expression>());
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
			case Expr.MyInstanceMethodCall my:
				return emitMyInstanceMethodCall(my);
			case Expr.New n:
				return new Estree.NewExpression(loc, id(loc, n.klass.name), n.args.map(exprToExpr));
			case Expr.Recur r:
				return emitRecur(r);
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

	Estree.Expression emitMyInstanceMethodCall(Expr.MyInstanceMethodCall m) =>
		JsBuiltins.emitMyInstanceMethodCall(ref needsLib, m.method, m.loc, m.args.map(exprToExpr));

	Estree.Expression emitRecur(Expr.Recur r) {
		var loc = r.loc;
		var implemented = r.recurseTo.implementedMethod;
		var methodName = implemented.name;
		var fn = implemented.isStatic ? Estree.MemberExpression.simple(loc, currentClassName, methodName) : Estree.MemberExpression.ofThis(loc, methodName);
		return new Estree.CallExpression(loc, fn, r.args.map(exprToExpr));
	}

	/** if (test1) { return result1; } else if (test2) { return result2; } else { return result3; } */
	Estree.Statement whenToStatement(Expr.WhenTest w) =>
		w.cases.foldBackwards(exprToReturnStatementOrBlock(w.elseResult), (res, kase) =>
			new Estree.IfStatement(kase.loc, exprToExpr(kase.test), exprToReturnStatementOrBlock(kase.result), res));

	/** test1 ? result1 : test2 ? result2 : elze */
	Estree.Expression whenToExpr(Expr.WhenTest w) =>
		w.cases.foldBackwards(exprToExpr(w.elseResult), (res, kase) =>
			new Estree.ConditionalExpression(kase.loc,  exprToExpr(kase.test), exprToExpr(kase.result), res));
}
