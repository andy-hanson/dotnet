using Model;

using static EstreeUtils;
using static Utils;

sealed class JsExprEmitter {
	internal bool needsLib = false;
	readonly bool thisMethodIsAsync = false;

	internal JsExprEmitter(bool thisMethodIsAsync) {
		this.thisMethodIsAsync = thisMethodIsAsync;
	}

	Estree.Expression getFromLib(Loc loc, Sym id) {
		needsLib = true;
		return JsBuiltins.getFromLib(loc, id);
	}

	internal Estree.BlockStatement exprToBlockStatement(Expr expr) {
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
					if (l.value is LiteralValue.Pass)
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
