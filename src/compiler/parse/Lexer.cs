using System;

using Diag.ParseDiags;
using static CharUtils;
using static ParserExitException;
using static Utils;

//mv
static class ReaderU {
	internal static string debug(string source, Pos pos) {
		var nlBefore = pos.index;
		while (nlBefore > 0 && source.at(nlBefore - 1) != '\n')
			nlBefore--;
		var nlAfter = pos.index;
		while (nlAfter < source.Length && source.at(nlAfter) != '\n')
			nlAfter++;

		var lineNo = 0;
		for (uint i = 0; i < nlBefore; i++)
			if (source.at(i) == '\n')
				lineNo++;

		return StringMaker.create()
			.add("Line ")
			.add(lineNo + 1)
			.add(": ")
			.add(source.slice(nlBefore, pos.index))
			.add('|')
			.add(source.slice(pos.index, nlAfter))
			.finish();
	}
}

abstract class Reader {
	readonly string source;
	// Index of the character we are *about* to take.
	protected Pos pos = Pos.start;

	protected Reader(string source) {
		//Ensure "source" ends in a newline.
		//Add an EOF too.
		if (!source.EndsWith("\n")) {
			source += "\n";
		}
		this.source = source + '\0';
	}

	protected char peek => source.at(pos.index);
	protected char peek2 => source.at(pos.index + 1);

	protected void backup() { pos = pos.decr; }
	protected void skip() { pos = pos.incr; }
	protected void skip2() { pos = pos.incr.incr; }

	protected char readChar() {
		var res = peek;
		skip();
		return res;
	}

	protected string debug() => ReaderU.debug(source, pos);

	protected string sliceFrom(Pos startPos) =>
		source.slice(startPos.index, pos.index);
}

abstract class Lexer : Reader {
	uint indent = 0;
	// Number of Token.Dedent we have to output before continuing to read.
	uint dedenting = 0;

	protected Loc singleCharLoc => Loc.singleChar(pos);

	// This is set after taking one of several kinds of token, such as Name or NumberLiteral
	protected string tokenValue;
	protected Sym tokenSym => Sym.of(tokenValue);

	protected Lexer(string source) : base(source) {
		skipEmptyLines();
	}

	protected Loc locFrom(Pos start) => new Loc(start, pos);

	void skipWhile(Func<char, bool> pred) {
		while (pred(peek)) skip();
	}

	void skipEmptyLines() {
		skipWhile(ch => ch == '\n');
	}

	protected enum QuoteEnd {
		QuoteEnd,
		QuoteInterpolation,
	}

	protected QuoteEnd nextQuotePart() {
		var s = StringMaker.create();
		var isEnd = false;
		while (true) {
			var ch = readChar();
			switch (ch) {
				case '"':
					isEnd = true;
					goto done;
				case '{':
					isEnd = false;
					goto done;
				case '\n':
					throw TODO(); // unterminated quote
				case '\\':
					s.add(escape(ch));
					break;
				default:
					s.add(ch);
					break;
			}
		}
		done:
		this.tokenValue = s.finish();
		return isEnd ? QuoteEnd.QuoteEnd : QuoteEnd.QuoteInterpolation;
	}

	static char escape(char escaped) {
		switch (escaped) {
			case '"':
			case '{':
				return escaped;
			case 'n':
				return '\n';
			case 't':
				return '\t';
			default:
				throw TODO(); //bad escape
		}
	}

	//This is called *after* having skipped the first char of the number.
	Token takeNumber(Pos startPos, bool isSigned) {
		skipWhile(isDigit);
		var isFloat = peek == '.' && isDigit(peek2);
		if (isFloat) {
			skip2();
			skipWhile(isDigit);
		}
		this.tokenValue = sliceFrom(startPos);
		return isFloat ? Token.RealLiteral : isSigned ? Token.IntLiteral : Token.NatLiteral;
	}

	Token takeNameOrKeyword(Pos startPos) {
		skipWhile(isNameChar);
		var s = sliceFrom(startPos);

		if (TokenU.keywordFromName(s, out var kw))
			return kw;

		this.tokenValue = s;
		return Token.Name;
	}

	Token takeStringLike(Token kind, Pos startPos, Func<char, bool> pred) {
		skipWhile(pred);
		this.tokenValue = sliceFrom(startPos);
		return kind;
	}

	protected Token nextToken() {
		if (dedenting != 0) {
			dedenting--;
			return Token.Dedent;
		}
		return takeNext();
	}

	Token takeNext() {
		var start = pos;
		var ch = readChar();
		switch (ch) {
			case '\0':
				// Remember to dedent before finishing
				if (indent != 0) {
					indent--;
					dedenting = indent;
					backup();
					return Token.Dedent;
				} else {
					return Token.EOF;
				}

			case ' ':
				if (peek == '\n') throw exit(singleCharLoc, TrailingSpace.instance);
				return Token.Space;

			case '\n':
				return handleNewline();

			case '|':
				throw TODO();

			case '\\': return Token.Backslash;
			case ':': return Token.Colon;
			case '(': return Token.ParenL;
			case ')': return Token.ParenR;
			case '[': return Token.BracketL;
			case ']': return Token.BracketR;
			case '{': return Token.CurlyL;
			case '}': return Token.CurlyR;
			case '_': return Token.Underscore;

			case '.':
				if (peek == '.') {
					skip();
					return Token.DotDot;
				} else
					return Token.Dot;

			case '"':
				var qp = nextQuotePart();
				return qp == QuoteEnd.QuoteEnd ? Token.StringLiteral : Token.QuoteStart;

			case '0': case '1': case '2': case '3': case '4':
			case '5': case '6': case '7': case '8': case '9':
				return takeNumber(start, isSigned: false);

			case 'a': case 'b': case 'c': case 'd': case 'e':
			case 'f': case 'g': case 'h': case 'i': case 'j':
			case 'k': case 'l': case 'm': case 'n': case 'o':
			case 'p': case 'q': case 'r': case 's': case 't':
			case 'u': case 'v': case 'w': case 'x': case 'y': case 'z':
				return takeNameOrKeyword(start);

			case 'A': case 'B': case 'C': case 'D': case 'E':
			case 'F': case 'G': case 'H': case 'I': case 'J':
			case 'K': case 'L': case 'M': case 'N': case 'O':
			case 'P': case 'Q': case 'R': case 'S': case 'T':
			case 'U': case 'V': case 'W': case 'X': case 'Y': case 'Z':
				return takeStringLike(Token.TyName, start, isNameChar);

			case '=':
				if (peek == '=') {
					skip();
					this.tokenValue = "==";
					return Token.Operator;
				}
				return Token.Equals;

			case '-': case '+':
				if (isDigit(peek)) return takeNumber(start, isSigned: true);
				goto case '*';

			case '*': case '/': case '^': case '?': case '<': case '>':
				return takeStringLike(Token.Operator, start, isOperatorChar);

			default:
				throw exit(singleCharLoc, new UnrecognizedCharacter(ch));
		}
	}

	static bool isOperatorChar(char ch) {
		switch (ch) {
			case '+':
			case '-':
			case '*':
			case '/':
			case '^':
			case '?':
			case '<':
			case '>':
				return true;
			default:
				return false;
		}
	}

	bool tryTake(char ch) {
		if (peek == ch) {
			skip();
			return true;
		}
		return false;
	}

	void expectNewlineCharacter() => expectCharacter('\n', "newline");
	void expectTabCharacter() => expectCharacter('\t', "tab");

	void expectCharacter(char expected, string expectedDesc) {
		var actual = readChar();
		if (actual != expected)
			throw exit(singleCharLoc, new UnexpectedCharacter(actual, expectedDesc));
	}

	void expectCharacter(string expected, Func<char, bool> pred) {
		var ch = readChar();
		if (!pred(ch)) {
			System.Diagnostics.Debugger.Break(); //VSCode shows an empty call stack on the below exception...
			throw exit(singleCharLoc, new UnexpectedCharacter(ch, expected));
		}
	}

	uint lexIndent() {
		var start = pos;
		skipWhile(ch => ch == '\t');
		var count = pos - start;
		if (peek == ' ') throw exit(locFrom(start), LeadingSpace.instance);
		return count;
	}

	Token handleNewline() {
		skipEmptyLines();
		var oldIndent = indent;
		indent = lexIndent();
		if (indent > oldIndent) {
			if (indent != oldIndent + 1) throw exit(singleCharLoc, new TooMuchIndent(oldIndent + 1, indent));
			return Token.Indent;
		} else if (indent == oldIndent) {
			return Token.Newline;
		} else {
			// `- 1` becuase the Token.Dedent that we're about to return doesn't go in dedenting
			dedenting = oldIndent - indent - 1;
			return Token.Dedent;
		}
	}

	protected enum NewlineOrDedent { Newline, Dedent }
	protected NewlineOrDedent takeNewlineOrDedent() {
		expectNewlineCharacter();
		skipEmptyLines();
		doTimes(indent - 1, () => expectTabCharacter());
		if (tryTake('\t'))
			return NewlineOrDedent.Newline;
		else {
			indent--;
			return NewlineOrDedent.Dedent;
		}
	}

	protected enum NewlineOrIndent { Newline, Indent }
	protected NewlineOrIndent takeNewlineOrIndent() {
		expectNewlineCharacter();
		skipEmptyLines();
		doTimes(indent, () => expectTabCharacter());
		if (tryTake('\t')) {
			indent++;
			return NewlineOrIndent.Indent;
		}
		else
			return NewlineOrIndent.Newline;
	}

	protected bool atEOF => peek == '\0';

	protected bool tryTakeDedentFromDedenting() {
		if (dedenting == 0)
			return false;
		else {
			dedenting--;
			return true;
		}
	}

	protected bool tryTakeDedent() {
		if (dedenting != 0) {
			dedenting--;
			return true;
		}

		var start = pos;
		if (!tryTake('\n'))
			return false;

		// If a newline is taken, it *must* be a dedent.
		var x = handleNewline();
		if (x != Token.Dedent) {
			unexpected(start, "dedent", x);
		}
		return true;
	}

	protected void takeDedent() {
		if (dedenting != 0) {
			dedenting--;
			return;
		}

		expectNewlineCharacter();
		skipEmptyLines();
		this.indent--;
		doTimes(this.indent, expectTabCharacter);
	}

	protected void takeNewline() {
		expectNewlineCharacter();
		skipEmptyLines();
		doTimes(this.indent, expectTabCharacter);
	}

	protected bool tryTakeNewline() {
		if (!tryTake('\n')) return false;
		skipEmptyLines();
		doTimes(this.indent, expectTabCharacter);
		return true;
	}

	protected void takeIndent() {
		expectNewlineCharacter();
		this.indent++;
		doTimes(this.indent, expectTabCharacter);
	}

	protected bool tryTakeSpace() => tryTake(' ');

	protected void takeSpace() => expectCharacter(' ', "space");
	protected void takeLparen() => expectCharacter('(', "'('");
	protected void takeRparen() => expectCharacter(')', "')'");
	protected void takeComma() => expectCharacter(',', "','");
	protected void takeDot() => expectCharacter('.', "'.'");
	protected void takeEquals() => expectCharacter('=', "'='");

	protected bool tryTakeRparen() => tryTake(')');
	protected bool tryTakeDot() => tryTake('.');
	protected bool tryTakeColon() => tryTake(':');

	protected void takeSpecificKeyword(string kw) {
		var startPos = pos;
		var actual = takeWord();
		if (actual != kw)
			throw unexpected(startPos, kw, actual);
	}

	protected string takeTyNameString() {
		var startPos = pos;
		expectCharacter("type name", isUpperCaseLetter);
		skipWhile(isNameChar);
		return sliceFrom(startPos);
	}

	protected string takeNameString() {
		var startPos = pos;
		expectCharacter("(non-type) name", isLowerCaseLetter);
		skipWhile(isNameChar);
		return sliceFrom(startPos);
	}

	protected Sym takeName() => Sym.of(takeNameString());
	protected Sym takeTyName() => Sym.of(takeTyNameString());

	protected ParserExitException unexpected(Pos startPos, string expectedDesc, Token token) =>
		unexpected(startPos, expectedDesc, TokenU.TokenName(token));

	ParserExitException unexpected(Pos startPos, string expectedDesc, string actualDesc) =>
		exit(locFrom(startPos), new UnexpectedToken(expectedDesc, actualDesc));

	protected enum CatchOrFinally { Catch, Finally }
	protected CatchOrFinally takeCatchOrFinally() {
		var startPos = pos;
		var word = takeWord();
		switch (word) {
			case "catch":
				return CatchOrFinally.Catch;
			case "finally":
				return CatchOrFinally.Finally;
			default:
				throw unexpected(startPos, "'catch' or 'finally'", word);
		}
	}

	protected enum SlotKw { Val, Var }
	protected SlotKw takeSlotKeyword() {
		var startPos = pos;
		var word = takeWord();
		switch (word) {
			case "val":
				return SlotKw.Val;
			case "var":
				return SlotKw.Var;
			default:
				throw unexpected(startPos, "'catch' or 'finally'", word);
		}
	}

	protected enum MethodKw { Def, Fun, Is, Eof }
	protected MethodKw takeMethodKeywordOrEof() {
		if (atEOF)
			return MethodKw.Eof;

		var startPos = pos;
		var word = takeWord();
		switch (word) {
			case "def":
				return MethodKw.Def;
			case "fun":
				return MethodKw.Fun;
			case "is":
				return MethodKw.Is;
			default:
				throw unexpected(startPos, "'def' or 'fun'", word);
		}
	}

	string takeWord() {
		var startPos = pos;
		expectCharacter("keyword", isLowerCaseLetter);
		skipWhile(isNameChar);
		return sliceFrom(startPos);
	}
}
