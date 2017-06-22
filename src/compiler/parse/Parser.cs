using static Arr;
using static ParserExit;
using static Utils;

using Pos = System.UInt32;

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
			? (Ast.Module.Import) new Ast.Module.Import.Global(loc, path)
			: (Ast.Module.Import) new Ast.Module.Import.Relative(loc, new RelPath(leadingDots, path)));
	}

	Ast.Klass parseClass(Pos start, Token kw) {
		var head = parseHead(start, kw);
		var members = buildUntilNull(parseMember);
		return new Ast.Klass(locFrom(start), head, members);
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
				takeIndent();
				var vars = build2(parseSlot); // At least one slot must exist.
				return new Ast.Klass.Head.Slots(locFrom(start), vars);
			case Token.Enum:
				throw TODO();
			default:
				throw unexpected(start, "'slots' or 'enum'", kw);
		}
	}

	Arr.Iter<Ast.Klass.Head.Slots.Slot> parseSlot() {
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
		return new Arr.Iter<Ast.Klass.Head.Slots.Slot>(slot, isNext);
	}

	Op<Ast.Member> parseMember() {
		if (atEOF) {
			return Op<Ast.Member>.None;
		}
		return Op.Some<Ast.Member>(doParseMember());
	}

	Ast.Member doParseMember() {
		var start = pos;
		var next = takeKeyword();
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
		ExprOnly,
		/** Parse an expression and expect ')' at the end. */
		Paren,
		/** Looking for a QuoteEnd */
		Quote,
		/** In this context, and indent will result in CtxEnded. */
		SingleLineThenIndent,
		List,
	}

	struct Next {
		internal enum Kind {
			NewlineAfterEquals, //comes with a Pattern
			NewlineAfterStatement,
			EndNestedBlock,
			CtxEnded
		}

		internal readonly Kind kind;
		readonly Op<Ast.Pattern> _pattern; // Use only if we are not a special kind.

		internal Ast.Pattern pattern => _pattern.force;

		Next(Kind kind, Ast.Pattern pattern) { this.kind = kind; this._pattern = Op.fromNullable(pattern); }

		internal static Next NewlineAfterEquals(Ast.Pattern p) => new Next(Kind.NewlineAfterEquals, p);
		internal static Next NewlineAfterStatement => new Next(Kind.NewlineAfterStatement, null);
		internal static Next EndNestedBlock => new Next(Kind.EndNestedBlock, null);
		internal static Next CtxEnded => new Next(Kind.CtxEnded, null);

		internal bool isCtxEnded => kind == Kind.CtxEnded;
	}

	Ast.Expr parseBlockWithStart(Pos start, Token first) {
		Ast.Expr expr;
		Next next;
		parseExprWithNext(start, first, Ctx.Line, out expr, out next);

		switch (next.kind) {
			case Next.Kind.NewlineAfterEquals: {
				var rest = parseBlock();
				return new Ast.Expr.Let(locFrom(start), next.pattern, expr, rest);
			}
			case Next.Kind.NewlineAfterStatement: {
				var rest = parseBlock();
				return new Ast.Expr.Seq(locFrom(start), expr, rest);
			}
			case Next.Kind.EndNestedBlock: {
				var start2 = pos;
				var first2 = nextToken();
				if (first2 == Token.Dedent)
					return expr;
				var rest = parseBlockWithStart(start2, first2);
				return new Ast.Expr.Seq(locFrom(start2), expr, rest);
			}
			case Next.Kind.CtxEnded:
				return expr;
			default:
				throw unreachable();
		}
	}

	Ast.Expr parseExprAndEndContext(Ctx ctx) {
		var start = pos;
		var startToken = nextToken();
		parseExprWithNext(start, startToken, ctx, out var expr, out var next);
		assert(next.isCtxEnded);
		return expr;
	}

	void parseExpr(Ctx ctx, out Ast.Expr expr, out Next next) {
		var start = pos;
		var startToken = nextToken();
		parseExprWithNext(start, startToken, ctx, out expr, out next);
	}

	void parseExprWithNext(Pos exprStart, Token startToken, Ctx ctx, out Ast.Expr expr, out Next next) {
		var parts = Arr.builder<Ast.Expr>();
		Ast.Expr finishRegular() {
			var loc = locFrom(exprStart);
			switch (parts.curLength) {
				case 0:
					throw exit(loc, Err.EmptyExpression);
				case 1:
					return parts[0];
				default:
					var head = parts[0];
					return new Ast.Expr.Call(loc, head, parts.finishTail());
			}
		}

		var loopStart = exprStart;
		var loopNext = startToken;
		void readAndLoop() {
			loopStart = pos;
			loopNext = nextToken();
		}

		while (true) {
			switch (loopNext) {
				case Token.Dot:
					var name = takeName();
					parts.setLast(new Ast.Expr.GetProperty(locFrom(loopStart), parts.last, name));
					readAndLoop();
					break;

				case Token.Equals: {
					var patternLoc = locFrom(loopStart);
					if (ctx != Ctx.Line) throw TODO(); //diagnostic
					var pattern = partsToPattern(patternLoc, parts);
					Next nnext;
					parseExpr(Ctx.ExprOnly, out expr, out nnext);
					if (nnext.kind == Next.Kind.CtxEnded) throw exit(locFrom(loopStart), Err.BlockCantEndInLet);
					assert(nnext.kind == Next.Kind.NewlineAfterStatement);
					next = Next.NewlineAfterEquals(pattern);
					return;
				}

				case Token.Operator: {
					if (parts.isEmpty)
						throw TODO();
					var left = finishRegular();
					parseExpr(ctx, out var right, out next);
					expr = new Ast.Expr.OperatorCall(locFrom(exprStart), left, tokenSym, right);
					return;
				}

				case Token.Newline:
				case Token.Dedent: {
					if (ctx != Ctx.Line && ctx != Ctx.ExprOnly) throw unreachable();
					switch (loopNext) {
						case Token.Newline:
							next = Next.NewlineAfterStatement;
							break;
						case Token.Dedent:
							next = Next.CtxEnded;
							break;
						default:
							throw unreachable();
					}
					expr = finishRegular();
					return;
				}

				case Token.Lparen:
					if (parts.isEmpty)
						throw TODO();
					takeRparen();
					parts.setLast(new Ast.Expr.Call(locFrom(loopStart), parts.last, Arr.empty<Ast.Expr>()));
					readAndLoop();
					break;

				case Token.TyName: {
					var className = tokenSym;
					takeDot();
					var staticMethodName = takeName();
					parts.add(new Ast.Expr.StaticAccess(locFrom(loopStart), className, staticMethodName));
					readAndLoop();
					break;
				}

				case Token.When:
					//It will give us newlineafterequals or dedent
					parts.add(parseWhen(loopStart));
					expr = finishRegular();
					next = tryTakeDedentFromDedenting() ? Next.CtxEnded : Next.NewlineAfterStatement;
					return;

				case Token.Indent:
					if (ctx != Ctx.SingleLineThenIndent) {
						throw TODO();
					}
					expr = finishRegular();
					next = Next.CtxEnded;
					return;

				default:
					parts.add(singleTokenExpr(loopNext, locFrom(loopStart)));
					readAndLoop();
					break;
			}
		}
	}

	Ast.Expr singleTokenExpr(Token token, Loc loc) {
		switch (token) {
			case Token.Name:
				return new Ast.Expr.Access(loc, tokenSym);
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
