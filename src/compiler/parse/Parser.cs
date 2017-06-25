using static Arr;
using static ParserExit;
using static Utils;

sealed class Parser : Lexer {
	internal static Either<Ast.Module, CompileError> parse(string source) {
		try {
			return Either<Ast.Module, CompileError>.Left(parseOrFail(source));
		} catch (ParserExit e) {
			return Either<Ast.Module, CompileError>.Right(e.err);
		}
	}

	static Ast.Module parseOrFail(string source) => new Parser(source).parseModule();

	Parser(string source) : base(source) {}

	Ast.Module parseModule() {
		var start = pos;
		var kw = takeKeyword();

		Arr<Ast.Module.Import> imports;
		var classStart = start;
		var nextKw = kw;
		if (kw == Token.Import) {
			imports = buildUntilNull(parseImport);
			classStart = pos;
			nextKw = takeKeyword();
		} else {
			imports = Arr.empty<Ast.Module.Import>();
		}

		var klass = parseClass(classStart, nextKw);
		return new Ast.Module(locFrom(start), imports, klass);
	}

	//`import foo .bar ..baz`
	Op<Ast.Module.Import> parseImport() {
		if (tryTakeNewline())
			return Op<Ast.Module.Import>.None;

		takeSpace();

		var startPos = pos;
		uint leadingDots = 0;
		while (tryTakeDot()) leadingDots++;

		var pathParts = Arr.builder<string>();
		pathParts.add(takeNameString());
		while (tryTakeDot()) pathParts.add(takeNameString());

		var path = new Path(pathParts.finish());
		var loc = locFrom(startPos);
		return Op.Some<Ast.Module.Import>(leadingDots == 0
			? (Ast.Module.Import)new Ast.Module.Import.Global(loc, path)
			: (Ast.Module.Import)new Ast.Module.Import.Relative(loc, new RelPath(leadingDots, path)));
	}

	Ast.Klass parseClass(Pos start, Token kw) {
		var head = parseHead(start, kw);
		Arr<Ast.Super> supers;
		Arr<Ast.Member> methods;
		if (atEOF) {
			supers = Arr.empty<Ast.Super>();
			methods = Arr.empty<Ast.Member>();
		} else {
			var (superz, nextStart, next) = parseSupers();
			supers = superz;
			methods = atEOF ? Arr.empty<Ast.Member>() : parseMethods(nextStart, next);
		}
		return new Ast.Klass(locFrom(start), head, supers, methods);
	}

	Ast.Klass.Head parseHead(Pos start, Token kw) {
		switch (kw) {
			case Token.Abstract:
				takeNewline();
				return new Ast.Klass.Head.Abstract(locFrom(start));
			case Token.Static:
				takeNewline();
				return new Ast.Klass.Head.Static(locFrom(start));
			case Token.Slots:
				return parseSlots(start);
			case Token.Enum:
				throw TODO();
			default:
				throw unexpected(start, "'abstract', 'static', 'slots' or 'enum'", kw);
		}
	}

	(Arr<Ast.Super>, Pos, Token) parseSupers() {
		var start = pos;
		var next = takeKeyword();
		if (next != Token.Is) {
			return (Arr.empty<Ast.Super>(), start, next);
		}

		var super = parseSuper(start);
		var nextStart = pos;
		next = takeKeyword();
		//TODO: support multiple supers
		return (Arr.of(super), nextStart, next);
	}

	Ast.Super parseSuper(Pos start) {
		takeSpace();
		var name = takeName();
		Arr<Ast.Impl> impls;
		switch (takeNewlineOrIndent()) {
			case NewlineOrIndent.Indent:
				impls = Arr.build2(parseImpl);
				break;
			case NewlineOrIndent.Newline:
				impls = Arr.empty<Ast.Impl>();
				break;
			default:
				throw unreachable();
		}

		return new Ast.Super(locFrom(start), name, impls);
	}

	(Ast.Impl, bool isNext) parseImpl() {
		// foo(x, y)
		var start = pos;
		var name = takeName();
		takeLparen();
		Arr<Sym> parameters;
		if (tryTakeRparen())
			parameters = Arr.empty<Sym>();
		else {
			var first = takeName();
			parameters = buildUntilNullWithFirst(first, parseImplParameter);
		}

		takeIndent();
		var body = parseBlock();

		var impl = new Ast.Impl(locFrom(start), name, parameters, body);
		var isNext = !this.tryTakeDedentFromDedenting();
		return (impl, isNext);
	}

	Ast.Klass.Head.Slots parseSlots(Pos start) {
		takeIndent();
		var vars = build2(parseSlot); // At least one slot must exist.
		return new Ast.Klass.Head.Slots(locFrom(start), vars);
	}

	(Ast.Klass.Head.Slots.Slot, bool isNext) parseSlot() {
		var start = pos;
		var next = takeKeyword();
		bool mutable;
		switch (next) {
			case Token.Var:
				mutable = true;
				break;
			case Token.Val:
				mutable = false;
				break;
			default:
				throw unexpected(start, "'var' or 'val'", next);
		}

		takeSpace();
		var ty = parseTy();
		takeSpace();
		var name = takeName();
		var slot = new Ast.Klass.Head.Slots.Slot(locFrom(start), mutable, ty, name);
		var isNext = takeNewlineOrDedent() == NewlineOrDedent.Newline;
		return (slot, isNext);
	}

	Arr<Ast.Member> parseMethods(Pos start, Token next) {
		//TODO: helper fn for this pattern
		var b = Arr.builder<Ast.Member>();
		while (true) {
			b.add(parseMethod(start, next));
			if (atEOF)
				return b.finish();

			start = pos;
			next = takeKeyword();
		}
	}

	Ast.Member parseMethod(Pos start, Token next) {
		switch (next) {
			case Token.Fun:
				return parseMethodWithBody(start, isStatic: true);
			case Token.Def:
				return parseMethodWithBody(start, isStatic: false);
			case Token.Abstract:
				return parseAbstractMethod(start);
			default:
				throw unexpected(start, "'fun' or 'def' or 'abstract'", next);
		}
	}

	Ast.Member.AbstractMethod parseAbstractMethod(Pos start) {
		var (returnTy, name, parameters) = parseMethodHead();
		takeNewline();
		return new Ast.Member.AbstractMethod(locFrom(start), returnTy, name, parameters);
	}

	Op<Sym> parseImplParameter() {
		if (tryTakeRparen())
			return Op<Sym>.None;
		takeComma();
		takeSpace();
		return Op.Some(takeName());
	}

	(Ast.Ty, Sym, Arr<Ast.Member.Parameter>) parseMethodHead() {
		takeSpace();
		var returnTy = parseTy();
		takeSpace();
		var name = takeName();
		takeLparen();
		Arr<Ast.Member.Parameter> parameters;
		if (tryTakeRparen())
			parameters = Arr.empty<Ast.Member.Parameter>();
		else {
			var first = parseJustParameter();
			parameters = buildUntilNullWithFirst(first, parseParameter);
		}

		return (returnTy, name, parameters);
	}

	Ast.Member.Method parseMethodWithBody(Pos start, bool isStatic) {
		var (returnTy, name, parameters) = parseMethodHead();
		takeIndent();
		var body = parseBlock();
		return new Ast.Member.Method(locFrom(start), isStatic, returnTy, name, parameters, body);
	}

	Op<Ast.Member.Method.Parameter> parseParameter() {
		if (tryTakeRparen())
			return Op<Ast.Member.Parameter>.None;
		takeComma();
		takeSpace();
		return Op.Some(parseJustParameter());
	}

	Ast.Member.Parameter parseJustParameter() {
		var start = pos;
		var ty = parseTy();
		takeSpace();
		var name = takeName();
		return new Ast.Member.Parameter(locFrom(start), ty, name);
	}

	Ast.Ty parseTy() {
		var start = pos;
		var name = takeTyName();
		//TODO: handle Inst too
		return new Ast.Ty.Access(locFrom(start), name);
	}

	Ast.Expr parseBlock() {
		var start = pos;
		var next = nextToken();
		return parseBlockWithStart(start, next);
	}

	enum Ctx {
		//??
		Line,
		/** Line Line, but forbid `=` because we're already on the rhs of one. */
		EqualsRHS,
		/** Parse an expression and expect ')' at the end. */
		Paren,
		/** Looking for a QuoteEnd */
		Quote,
		/** In this context, and indent will result in CtxEnded. */
		SingleLineThenIndent,
		List,
	}

	enum Next {
		NewlineAfterEquals, // This means that we got an incomplete Let expression. Must fill in the body.
		NewlineAfterStatement,
		EndNestedBlock,
		CtxEnded,
	}

	Ast.Expr parseBlockWithStart(Pos start, Token first) {
		var (expr, next) = parseExprWithNext(Ctx.Line, start, first);

		switch (next) {
			case Next.NewlineAfterEquals: {
				if (!(expr is Ast.Expr.Let l))
					throw unreachable();
				l.then = parseBlock();
				return l;
			}
			case Next.NewlineAfterStatement: {
				var rest = parseBlock();
				return new Ast.Expr.Seq(locFrom(start), expr, rest);
			}
			case Next.EndNestedBlock: {
				var start2 = pos;
				var first2 = nextToken();
				if (first2 == Token.Dedent)
					return expr;
				var rest = parseBlockWithStart(start2, first2);
				return new Ast.Expr.Seq(locFrom(start2), expr, rest);
			}
			case Next.CtxEnded:
				return expr;
			default:
				throw unreachable();
		}
	}

	Ast.Expr parseExprAndEndContext(Ctx ctx) {
		var start = pos;
		var startToken = nextToken();
		var (expr, next) = parseExprWithNext(ctx, start, startToken);
		assert(next == Next.CtxEnded);
		return expr;
	}

	(Ast.Expr, Next) parseExpr(Ctx ctx) {
		var start = pos;
		var startToken = nextToken();
		return parseExprWithNext(ctx, start, startToken);
	}

	(Ast.Expr, Next) parseExprWithNext(Ctx ctx, Pos loopStart, Token loopNext) {
		var exprStart = loopStart;
		var isNew = loopNext == Token.New;
		if (isNew) {
			takeSpace();
			loopStart = pos;
			loopNext = nextToken();
		}

		var parts = Arr.builder<Ast.Expr>();

		while (true) {
			switch (loopNext) {
				case Token.Equals: {
					var patternLoc = locFrom(loopStart);
					if (ctx != Ctx.Line) throw TODO(); //diagnostic
					var pattern = partsToPattern(patternLoc, parts);
					takeSpace();
					var (value, next) = parseExpr(Ctx.EqualsRHS);
					if (next == Next.CtxEnded) throw exit(locFrom(loopStart), Err.BlockCantEndInLet);
					assert(next == Next.NewlineAfterStatement);
					return (new Ast.Expr.Let(locFrom(loopStart), pattern, value), Next.NewlineAfterEquals);
				}

				case Token.Operator: {
					if (parts.isEmpty)
						throw TODO();
					var left = finishRegular(exprStart, isNew, parts);
					var (right, next) = parseExpr(ctx);
					var expr = new Ast.Expr.OperatorCall(locFrom(left.loc.start), left, tokenSym, right);
					return (expr, next);
				}

				case Token.When: {
					if (ctx != Ctx.Line && ctx != Ctx.EqualsRHS) throw TODO();
					//It will give us newlineafterequals or dedent
					parts.add(parseWhen(loopStart));
					var expr = finishRegular(exprStart, isNew, parts);
					var next = tryTakeDedentFromDedenting() ? Next.CtxEnded : Next.NewlineAfterStatement;
					return (expr, next);
				}

				default: {
					var (single, nextPos, tokenAfter) = parseSimpleExpr(loopStart, loopNext);
					parts.add(single);
					switch (tokenAfter) {
						case Token.Space:
							loopStart = pos;
							loopNext = nextToken();
							// Continue adding parts
							break;

						case Token.Newline:
						case Token.Dedent: {
							if (ctx != Ctx.Line && ctx != Ctx.EqualsRHS) throw TODO();
							var expr = finishRegular(exprStart, isNew, parts);
							Next next;
							switch (tokenAfter) {
								case Token.Newline:
									next = Next.NewlineAfterStatement;
									break;
								case Token.Dedent:
									next = Next.CtxEnded;
									break;
								default:
									throw unreachable();
							}
							return (expr, next);
						}

						case Token.Indent: {
							if (ctx != Ctx.SingleLineThenIndent)
								throw TODO();
							var expr = finishRegular(exprStart, isNew, parts);
							return (expr, Next.CtxEnded);
						}

						default:
							throw unexpected(nextPos, "Space or newline", tokenAfter);
					}

					break;
				}
			}
		}
	}

	Ast.Expr finishRegular(Pos exprStart, bool isNew, Arr.Builder<Ast.Expr> parts) {
		if (parts.curLength == 0)
			throw exit(singleCharLoc, Err.EmptyExpression);

		if (isNew) {
			return new Ast.Expr.New(locFrom(exprStart), parts.finish());
		} else {
			var res = parts[0];
			if (parts.curLength > 1)
				res = new Ast.Expr.Call(locFrom(res.loc.start), res, parts.finishTail());
			return res;
		}
	}

	(Ast.Expr, Pos, Token) parseSimpleExpr(Pos pos, Token token) {
		switch (token) {
			case Token.Name:
				return takeSuffixes(new Ast.Expr.Access(locFrom(pos), tokenSym));
			case Token.TyName:
				var className = tokenSym;
				takeDot();
				var staticMethodName = takeName();
				return takeSuffixes(new Ast.Expr.StaticAccess(locFrom(pos), className, staticMethodName));
			default:
				var single = singleTokenExpr(locFrom(pos), token);
				var start = pos;
				var next = nextToken();
				return (single, pos, next);
		}
	}

	(Ast.Expr, Pos, Token) takeSuffixes(Ast.Expr expr) {
		while (true) {
			var start = pos;
			var next = nextToken();
			switch (next) {
				case Token.Dot:
					var name = takeName();
					expr = new Ast.Expr.GetProperty(locFrom(start), expr, name);
					break;

				case Token.Lparen:
					takeRparen();
					expr = new Ast.Expr.Call(locFrom(start), expr, Arr.empty<Ast.Expr>());
					break;

				default:
					return (expr, pos, next);
			}
		}
	}

	Ast.Expr singleTokenExpr(Loc loc, Token token) {
		switch (token) {
			case Token.IntLiteral:
				return new Ast.Expr.Literal(loc, new Model.Expr.Literal.LiteralValue.Int(int.Parse(tokenValue)));
			case Token.FloatLiteral:
				return new Ast.Expr.Literal(loc, new Model.Expr.Literal.LiteralValue.Float(double.Parse(tokenValue)));
			case Token.StringLiteral:
				return new Ast.Expr.Literal(loc, new Model.Expr.Literal.LiteralValue.Str(tokenValue));
			case Token.Pass:
				return new Ast.Expr.Literal(loc, Model.Expr.Literal.LiteralValue.Pass.instance);
			case Token.True:
			case Token.False:
				return new Ast.Expr.Literal(loc, new Model.Expr.Literal.LiteralValue.Bool(token == Token.True));
			case Token.Self:
				return new Ast.Expr.Self(loc);
			default:
				throw TODO(); //diagnostic
		}
	}

	Ast.Expr parseWhen(Pos startPos) {
		/*
		when
			firstTest
				firstResult
			else
				elseResult
		*/
		takeIndent();
		//Must take at least one non-'else' part.
		var firstCaseStartPos = pos;
		var firstTest = parseExprAndEndContext(Ctx.SingleLineThenIndent);
		var firstResult = parseBlock();
		var firstCase = new Ast.Expr.WhenTest.Case(locFrom(firstCaseStartPos), firstTest, firstResult);

		//TODO: support arbitrary number of clauses
		takeSpecificKeyword(Token.Else);
		takeIndent();
		var elseResult = parseBlock();

		return new Ast.Expr.WhenTest(locFrom(startPos), Arr.of(firstCase), elseResult);
	}

	static Ast.Pattern partsToPattern(Loc loc, Arr.Builder<Ast.Expr> parts) {
		switch (parts.curLength) {
			case 0: throw exit(loc, Err.PrecedingEquals);
			case 1: return partToPattern(parts[0]);
			default: return new Ast.Pattern.Destruct(loc, parts.map(partToPattern));
		}

		Ast.Pattern partToPattern(Ast.Expr part) {
			switch (part) {
				case Ast.Expr.Access a:
					return new Ast.Pattern.Single(a.loc, a.name);
				default:
					throw exit(loc, Err.PrecedingEquals);
			}
		}
	}
}
