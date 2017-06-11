using System;
using System.Text;

using static ParserExit;
using static Utils;

using Pos = System.UInt32;

abstract class Lexer {
	readonly string source;
	// Index of the character we are *about* to take.
	protected Pos pos = 0;
	uint indent = 0;
	// Number of Token.Dedent we have to output before continuing to read.
	uint dedenting = 0;

	// This is set after taking one of several kinds of token, such as Name or NumberLiteral
	protected string tokenValue;
	protected Sym tokenSym => Sym.of(tokenValue);

	string debug() {
		//Current line with a '|' in the middle.
		var ch = pos;
		var nlBefore = pos;
		while (nlBefore > 0 && source.at(nlBefore - 1) != '\n')
			nlBefore--;
		var nlAfter = pos;
		while (nlAfter < source.Length && source.at(nlAfter) != '\n')
			nlAfter++;

		return $"{source.slice(nlBefore, pos)}|{source.slice(pos, nlAfter)}";
	}


	protected Lexer(string source) {
		//Ensure "source" ends in a newline.
		//Add an EOF too.
		if (!source.EndsWith("\n")) {
			source += "\n";
		}
		this.source = source + '\0';

		skipNewlines();
	}

	protected Loc locFrom(Pos start) => new Loc(start, pos);

	char peek => source.at(pos);

	char readChar() => source.at(pos++);

	void skip() { pos++; }

	void skipWhile(Func<char, bool> pred) {
		while (pred(peek)) pos++;
	}

	void skipNewlines() {
		skipWhile(ch => ch == '\n');
	}

	protected enum QuoteEnd {
		QuoteEnd,
		QuoteInterpolation,
	}

	protected QuoteEnd nextQuotePart() {
		var s = new StringBuilder();
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
					throw new Exception("TODO: Compile error: unterminated quote");
				case '\\':
					s.Append(escape(readChar()));
					break;
				default:
					s.Append(readChar());
					break;
			}
		}
		done:
		this.tokenValue = s.ToString();
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
				throw new Exception("TODO: Compile error: bad escape");
		}
	}

	string sliceFrom(Pos startPos) {
		return source.slice(startPos, pos);
	}

	//This is called *after* having skipped the first char of the number.
	Token takeNumber(Pos startPos) {
		skipWhile(isDigit);
		var isFloat = peek == '.';
		if (isFloat) {
			skip();
			if (!isDigit(peek)) throw exit(Loc.singleChar(pos), Err.TooMuchIndent);
		}
		this.tokenValue = sliceFrom(startPos);
		return isFloat ? Token.FloatLiteral : Token.IntLiteral;
	}

	Token takeNameOrKeyword(Pos startPos) {
		skipWhile(isNameChar);
		var s = sliceFrom(startPos);
		var t = TokenU.keywordFromName(s);
		if (t != null) return t.Value;
		this.tokenValue = s;
		return Token.Name;
	}

	Token takeStringLike(Token kind, Pos startPos, Func<char, bool> pred) {
		skipWhile(pred);
		this.tokenValue = sliceFrom(startPos);
		return kind;
	}

	static bool isDigit(char ch) => '0' <= ch && ch <= '9';

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
					pos--;
					return Token.Dedent;
				} else {
					return Token.EOF;
				}

			case ' ':
				if (peek == '\n') throw exit(Loc.singleChar(pos), Err.TrailingSpace);
				throw new Exception("TODO");

			case '\n':
				return handleNewline();

			//case '|':
			//    //comment
			//    skipWhile(ch => ch != '\n');
			//    handleNewline(true);

			case '\\': return Token.Backslash;
			case ':': return Token.Colon;
			case '(': return Token.Lparen;
			case ')': return Token.Rparen;
			case '[': return Token.Lbracket;
			case ']': return Token.Rbracket;
			case '{': return Token.Lcurly;
			case '}': return Token.Rcurly;
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
				return takeNumber(start);

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

			case '-':
				var next = readChar();
				if (isDigit(next)) return takeNumber(start);
				goto case '+';

			case '+': case '*': case '/': case '^': case '?': case '<': case '>':
				return takeStringLike(Token.Operator, start, isOperatorChar);

			default:
				throw exit(Loc.singleChar(start), new Err.UnrecognizedCharacter(ch));
		}
	}

	static bool isNameChar(char ch) {
		return 'a' <= ch && ch <= 'z' ||
			'A' <= ch && ch <= 'Z' ||
				isDigit(ch) || ch == '-';
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

	void expectCharacter(char expected) {
		var actual = readChar();
		if (actual != expected)
			throw exit(Loc.singleChar(pos), new Err.UnexpectedCharacter(actual, $"'{expected}'"));
	}

	void expectCharacter(string expected, Func<char, bool> pred) {
		var ch = readChar();
		if (!pred(ch)) {
			throw exit(Loc.singleChar(pos), new Err.UnexpectedCharacter(ch, expected));
		}
	}

	uint lexIndent() {
		var start = pos;
		skipWhile(ch => ch == '\t');
		var count = pos - start;
		if (peek == ' ') throw exit(locFrom(start), Err.LeadingSpace);
		return count;
	}

	Token handleNewline() {
		skipNewlines();
		var oldIndent = indent;
		indent = lexIndent();
		if (indent > oldIndent) {
			if (indent != oldIndent + 1) throw exit(Loc.singleChar(pos), Err.TooMuchIndent);
			return Token.Indent;
		} else if (indent == oldIndent) {
			return Token.Newline;
		} else {
			dedenting = oldIndent - indent - 1;
			return Token.Dedent;
		}
	}

	protected enum NewlineOrDedent { Newline, Dedent }
	protected NewlineOrDedent takeNewlineOrDedent() {
		expectCharacter('\n');
		skipNewlines();
		doTimes(this.indent - 1, () => expectCharacter('\t'));
		if (tryTake('\t'))
			return NewlineOrDedent.Newline;
		else {
			this.indent--;
			return NewlineOrDedent.Dedent;
		}
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
		assert(dedenting == 0);

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

	protected void takeNewline() {
		expectCharacter('\n');
		skipNewlines();
		doTimes(this.indent, () => expectCharacter('\t'));
	}

	protected bool tryTakeNewline() {
		if (!tryTake('\n')) return false;
		skipNewlines();
		doTimes(this.indent, () => expectCharacter('\t'));
		return true;
	}

	protected void takeIndent() {
		this.indent++;
		expectCharacter('\n');
		doTimes(this.indent, () => expectCharacter('\t'));
	}

	protected void takeSpace() => expectCharacter(' ');
	protected void takeLparen() => expectCharacter('(');
	protected void takeComma() => expectCharacter(',');
	protected void takeDot() => expectCharacter('.');
	protected bool tryTakeRparen() => tryTake(')');
	protected bool tryTakeDot() => tryTake('.');

	protected Token takeKeyword() {
		var startPos = pos;
		expectCharacter("keyword", ch => 'a' <= ch && ch <= 'z');
		skipWhile(isNameChar);
		var name = sliceFrom(startPos);
		return TokenU.keywordFromName(name) ?? throw unexpected(startPos, "keyword", name);
	}

	protected void takeSpecificKeyword(Token kw) {
		var startPos = pos;
		var actual = takeKeyword();
		if (actual != kw) throw unexpected(startPos, TokenU.TokenName(kw), TokenU.TokenName(actual));
	}

	protected Sym takeName() {
		var startPos = pos;
		expectCharacter("(non-type) name", ch => 'a' <= ch && ch <= 'z');
		skipWhile(isNameChar);
		return Sym.of(sliceFrom(startPos));
	}

	protected Sym takeTyName() {
		var startPos = pos;
		expectCharacter("type name", ch => 'A' <= ch && ch <= 'Z');
		skipWhile(isNameChar);
		return Sym.of(sliceFrom(startPos));
	}

	protected ParserExit unexpected(Pos startPos, string expectedDesc, Token token) => unexpected(startPos, expectedDesc,TokenU.TokenName(token));
	protected ParserExit unexpected(Pos startPos, string expectedDesc, string actualDesc) => exit(locFrom(startPos), new Err.UnexpectedToken(expectedDesc, actualDesc));
}