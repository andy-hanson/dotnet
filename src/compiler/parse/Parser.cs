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
		var head = parseHead(start, kw);
		Arr<Ast.Super> supers;
		Arr<Ast.Member> methods;
		if (atEOF) {
			supers = Arr.empty<Ast.Super>();
			methods = Arr.empty<Ast.Member>();
		} else {
			var (superz, nextStart, next) = parseSupers();
			supers = superz;
			methods = next == Token.EOF ? Arr.empty<Ast.Member>() : parseMethods(nextStart, next);
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
		var supers = Arr.builder<Ast.Super>();
		while (true) {
			var start = pos;
			var next = takeKeywordOrEof();
			if (next != Token.Is)
				return (supers.finish(), start, next);

			supers.add(parseSuper(start));
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

	(Ast.Ty, Sym, Arr<Ast.Parameter>, Model.Effect) parseMethodHead() {
		takeSpace();
		var returnTy = parseTy();
		takeSpace();
		var name = takeName();
		takeLparen();
		Arr<Ast.Parameter> parameters;
		if (tryTakeRparen())
			parameters = Arr.empty<Ast.Parameter>();
		else {
			var first = parseJustParameter();
			parameters = buildUntilNullWithFirst(first, parseParameter);
		}

		var effect = parseEffect();

		return (returnTy, name, parameters, effect);
	}

	Model.Effect parseEffect() {
		if (!tryTakeSpace())
			return Model.Effect.Pure;

		var start = pos;
		var kw = this.takeKeyword();
		switch (kw) {
			case Token.Get:
				return Model.Effect.Get;
			case Token.Set:
				return Model.Effect.Set;
			case Token.Io:
				return Model.Effect.Io;
			default:
				throw unexpected(start, "'get', 'set', or 'io'", kw);
		}
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
