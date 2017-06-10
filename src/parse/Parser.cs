using System.Collections.Immutable;
using System.Linq;
using static Arr;
using static ParserExit;
using static Utils;

using Pos = System.UInt32;

sealed class Parser : Lexer {
	internal static Either<Ast.Module, CompileError> parse(Sym name, string source) {
		try {
			return Either<Ast.Module, CompileError>.Left(parseOrFail(name, source));
		} catch (ParserExit e) {
			return Either<Ast.Module, CompileError>.Right(e.err);
		}
	}

	internal static Ast.Module parseOrFail(Sym name, string source) => new Parser(source).parseModule(name);

	Parser(string source) : base(source) {}

	Ast.Module parseModule(Sym name) {
		var start = pos;
		var kw = takeKeyword();

		ImmutableArray<Ast.Module.Import> imports;
		var classStart = start;
		var nextKw = kw;
		if (kw == Token.Import) {
			imports = buildUntilNull(parseImport);
			classStart = pos;
			nextKw = takeKeyword();
		} else {
			imports = ImmutableArray.Create<Ast.Module.Import>();
		}

		var klass = parseClass(name, classStart, nextKw);
		return new Ast.Module(locFrom(start), imports, klass);
	}

	//`import foo .bar ..baz`
	Op<Ast.Module.Import> parseImport() {
		if (tryTakeNewline())
			return Op<Ast.Module.Import>.None;

		takeSpace();

		var startPos = pos;
		var leadingDots = 0;
		while (tryTakeDot()) leadingDots++;

		var pathParts = ImmutableArray.CreateBuilder<Sym>();
		pathParts.Add(takeName());
		while (tryTakeDot()) pathParts.Add(takeName());

		var path = new Path(pathParts.ToImmutable());
		var loc = locFrom(startPos);
		return Op.Some<Ast.Module.Import>(leadingDots == 0
			? (Ast.Module.Import) new Ast.Module.Import.Global(loc, path)
			: (Ast.Module.Import) new Ast.Module.Import.Relative(loc, new RelPath(leadingDots, path)));
	}

	Ast.Klass parseClass(Sym name, Pos start, Token kw) {
		var head = parseHead(start, kw);
		var members = buildUntilNull(parseMember);
		return new Ast.Klass(locFrom(start), name, head, members);
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

		var start = pos;
		var next = takeKeyword();
		bool isStatic;
		switch (next) {
			case Token.Fun:
				isStatic = true;
				break;
			case Token.Def:
				isStatic = false;
				break;
			default:
				throw unexpected(start, "'fun' or 'def'", next);
		}

		takeSpace();
		var returnTy = parseTy();
		takeSpace();
		var name = takeName();
		takeLparen();
		ImmutableArray<Ast.Member.Method.Parameter> parameters;
		if (tryTakeRparen())
			parameters = ImmutableArray.Create<Ast.Member.Method.Parameter>();
		else {
			var first = parseJustParameter();
			buildUntilNullWithFirst(first, parseParameter);
		}

		takeIndent();
		var body = parseBlock();
		return Op.Some<Ast.Member>(new Ast.Member.Method(locFrom(start), isStatic, returnTy, name, parameters, body));
	}

	Op<Ast.Member.Method.Parameter> parseParameter() {
		if (tryTakeRparen())
			return Op<Ast.Member.Method.Parameter>.None;
		takeComma();
		takeSpace();
		return Op.Some(parseJustParameter());
	}

	Ast.Member.Method.Parameter parseJustParameter() {
		var start = pos;
		var ty = parseTy();
		takeSpace();
		var name = takeName();
		return new Ast.Member.Method.Parameter(locFrom(start), ty, name);
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

		Next(Kind kind, Ast.Pattern pattern) { this.kind = kind; this._pattern = Op.FromNullable(pattern); }

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
		var parts = ImmutableArray.CreateBuilder<Ast.Expr>();
		Ast.Expr finishRegular() {
			var loc = locFrom(exprStart);
			switch (parts.Count) {
				case 0:
					throw exit(loc, Err.EmptyExpression);
				case 1:
					return parts[0];
				default:
					var head = parts.popLeft();
					return new Ast.Expr.Call(loc, head, parts.ToImmutable());
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
					if (!parts.Any())
						throw TODO();
					var left = finishRegular();
					Ast.Expr right;
					parseExpr(ctx, out right, out next);
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

				case Token.TyName: {
					var className = tokenSym;
					takeDot();
					var staticMethodName = takeName();
					parts.Add(new Ast.Expr.StaticAccess(locFrom(loopStart), className, staticMethodName));
					readAndLoop();
					break;
				}

				case Token.When:
					//It will give us newlineafterequals or dedent
					parts.Add(parseWhen(loopStart));
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

				default: {
					// Single-token expressions:
					var loc = locFrom(loopStart);
					Ast.Expr e;
					switch (loopNext) {
						case Token.Name:
							e = new Ast.Expr.Access(loc, tokenSym);
							break;
						case Token.IntLiteral:
							e = new Ast.Expr.Literal(loc, new Model.Expr.Literal.LiteralValue.Int(int.Parse(tokenValue)));
							break;
						case Token.FloatLiteral:
							e = new Ast.Expr.Literal(loc, new Model.Expr.Literal.LiteralValue.Float(double.Parse(tokenValue)));
							break;
						case Token.StringLiteral:
							e = new Ast.Expr.Literal(loc, new Model.Expr.Literal.LiteralValue.Str(tokenValue));
							break;
						default:
							throw TODO(); //diagnostic
					}
					parts.Add(e);
					readAndLoop();
					break;
				}
			}
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

		return new Ast.Expr.WhenTest(locFrom(startPos), ImmutableArray.Create(firstCase), elseResult);
	}

	static Ast.Pattern partsToPattern(Loc loc, ImmutableArray<Ast.Expr>.Builder parts) {
		Ast.Pattern partToPattern(Ast.Expr part) {
			var a = part as Ast.Expr.Access;
			return a == null ? throw exit(loc, Err.PrecedingEquals) : new Ast.Pattern.Single(a.loc, a.name);
		}
		switch (parts.Count) {
			case 0: throw exit(loc, Err.PrecedingEquals);
			case 1: return partToPattern(parts[0]);
			default: return new Ast.Pattern.Destruct(loc, parts.map(partToPattern));
		}
	}
}
