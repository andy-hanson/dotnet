using static Arr;
using static Utils;

sealed class Parser : ExprParser {
	internal static Either<Ast.Module, CompileError> parse(string source) {
		try {
			return Either<Ast.Module, CompileError>.Left(parseOrFail(source));
		} catch (ParserExitException e) {
			return Either<Ast.Module, CompileError>.Right(e.err);
		}
	}

	static Ast.Module parseOrFail(string source) => new Parser(source).parseModule();

	Parser(string source) : base(source) {}

	Ast.Module parseModule() {
		var start = pos;
		var kw = takeKeywordOrEof();

		Arr<Ast.Module.Import> imports;
		var classStart = start;
		var nextKw = kw;
		if (kw == Token.Import) {
			imports = buildUntilNull(parseImport);
			classStart = pos;
			nextKw = takeKeywordOrEof();
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
		pathParts.add(takeTyNameString());
		//TODO: really want to allow delving into grandchildren?
		while (tryTakeDot()) pathParts.add(takeNameString());

		var path = new Path(pathParts.finish());
		var loc = locFrom(startPos);
		return Op.Some<Ast.Module.Import>(leadingDots == 0
			? (Ast.Module.Import)new Ast.Module.Import.Global(loc, path)
			: (Ast.Module.Import)new Ast.Module.Import.Relative(loc, new RelPath(leadingDots, path)));
	}

	Ast.Klass parseClass(Pos start, Token kw) {
		var (head, nextStart, nextKw) = parseHead(start, kw);
		var (supers, nextNextStart, nextNextKw) = parseSupers(nextStart, nextKw);
		var methods = nextNextKw == Token.EOF ? Arr.empty<Ast.Member>() : parseMethods(nextNextStart, nextNextKw);
		return new Ast.Klass(locFrom(start), head, supers, methods);
	}

	(Op<Ast.Klass.Head> head, Pos nextStart, Token nextKeyword) parseHead(Pos start, Token kw) {
		switch (kw) {
			case Token.EOF:
			case Token.Fun:
				return (Op<Ast.Klass.Head>.None, start, kw);
			case Token.Abstract: {
				takeNewline();
				var head = new Ast.Klass.Head.Abstract(locFrom(start));
				return (Op.Some<Ast.Klass.Head>(head), pos, takeKeywordOrEof());
			}
			case Token.Slots: {
				var head = parseSlots(start);
				return (Op.Some<Ast.Klass.Head>(head), pos, takeKeywordOrEof());
			}
			case Token.Enum:
				throw TODO();
			default:
				throw unexpected(start, "'abstract', 'static', 'slots' or 'enum'", kw);
		}
	}

	(Arr<Ast.Super>, Pos, Token) parseSupers(Pos start, Token next) {
		var supers = Arr.builder<Ast.Super>();
		while (true) {
			if (next != Token.Is)
				return (supers.finish(), start, next);

			supers.add(parseSuper(start));

			start = pos;
			next = takeKeywordOrEof();
		}
	}

	Ast.Super parseSuper(Pos start) {
		takeSpace();
		var name = takeTyName();
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
		var isNext = !tryTakeDedentFromDedenting();
		return (impl, isNext);
	}

	Ast.Klass.Head.Slots parseSlots(Pos start) {
		takeIndent();
		var vars = build2(parseSlot); // At least one slot must exist.
		return new Ast.Klass.Head.Slots(locFrom(start), vars);
	}

	(Ast.Slot, bool isNext) parseSlot() {
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
		var slot = new Ast.Slot(locFrom(start), mutable, ty, name);
		var isNext = takeNewlineOrDedent() == NewlineOrDedent.Newline;
		return (slot, isNext);
	}

	Arr<Ast.Member> parseMethods(Pos start, Token next) {
		//TODO: helper fn for this pattern
		var b = Arr.builder<Ast.Member>();
		while (true) {
			var m = parseMethod(start, next);
			if (!m.get(out var mm))
				return b.finish();

			b.add(mm);

			start = pos;
			next = takeKeywordOrEof();
		}
	}

	Op<Ast.Member> parseMethod(Pos start, Token next) {
		switch (next) {
			case Token.Fun:
				return Op.Some<Ast.Member>(parseMethodWithBody(start, isStatic: true));
			case Token.Def:
				return Op.Some<Ast.Member>(parseMethodWithBody(start, isStatic: false));
			case Token.Abstract:
				return Op.Some<Ast.Member>(parseAbstractMethod(start));
			case Token.EOF:
				return Op<Ast.Member>.None;
			default:
				throw unexpected(start, "'fun' or 'def' or 'abstract'", next);
		}
	}

	Ast.Member.AbstractMethod parseAbstractMethod(Pos start) {
		var (returnTy, name, parameters, effect) = parseMethodHead();
		takeNewline();
		return new Ast.Member.AbstractMethod(locFrom(start), returnTy, name, parameters, effect);
	}

	Op<Sym> parseImplParameter() {
		if (tryTakeRparen())
			return Op<Sym>.None;
		takeComma();
		takeSpace();
		return Op.Some(takeName());
	}

	(Ast.Ty, Sym, Model.Effect selfEffect, Arr<Ast.Parameter>) parseMethodHead() {
		takeSpace();
		var returnTy = parseTy();
		takeSpace();
		var name = takeName();
		takeLparen();

		var selfEffect = Model.Effect.Pure;
		Arr<Ast.Parameter> parameters;
		if (tryTakeRparen())
			parameters = Arr.empty<Ast.Parameter>();
		else {
			var firstStart = pos;
			var x = parseSelfEffectOrTy();
			if (x.isLeft) {
				selfEffect = x.left;
				parameters = tryTakeRparen() ? Arr.empty<Ast.Parameter>() : buildUntilNull(parseParameter);
			} else {
				var firstTy = x.right;
				takeSpace();
				var firstName = takeName();
				var first = new Ast.Parameter(locFrom(firstStart), firstTy, firstName);
				parameters = buildUntilNullWithFirst(first, parseParameter);
			}
		}

		return (returnTy, name, selfEffect, parameters);
	}

	Ast.Member.Method parseMethodWithBody(Pos start, bool isStatic) {
		var (returnTy, name, parameters, effect) = parseMethodHead();
		takeIndent();
		var body = parseBlock();
		return new Ast.Member.Method(locFrom(start), isStatic, returnTy, name, parameters, effect, body);
	}

	Op<Ast.Parameter> parseParameter() {
		if (tryTakeRparen())
			return Op<Ast.Parameter>.None;
		takeComma();
		takeSpace();
		return Op.Some(parseJustParameter());
	}

	Ast.Parameter parseJustParameter() {
		var start = pos;
		var ty = parseTy();
		takeSpace();
		var name = takeName();
		return new Ast.Parameter(locFrom(start), ty, name);
	}
}
