using System;
using System.Text;

using static ParserExit;
using static Utils;

#pragma warning disable SA1121 // Use Pos instead of uint

using Pos = System.UInt32;

//mv
static class ReaderU {
	internal static string debug(string source, uint pos) {
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
}

abstract class Reader {
	readonly string source;
	// Index of the character we are *about* to take.
	protected Pos pos = 0;

	protected Reader(string source) {
		//Ensure "source" ends in a newline.
		//Add an EOF too.
		if (!source.EndsWith("\n")) {
			source += "\n";
		}
		this.source = source + '\0';
	}

	protected char peek => source.at(pos);

	protected char readChar() => source.at(pos++);

	protected string debug() => ReaderU.debug(source, pos);

	protected string sliceFrom(Pos startPos) {
		return source.slice(startPos, pos);
	}
}

abstract class Lexer : Reader {
	uint indent = 0;
	// Number of Token.Dedent we have to output before continuing to read.
	uint dedenting = 0;

	// This is set after taking one of several kinds of token, such as Name or NumberLiteral
	protected string tokenValue;
	protected Sym tokenSym => Sym.of(tokenValue);

	protected Lexer(string source) : base(source) {
		skipNewlines();
	}

	protected Loc locFrom(Pos start) => new Loc(start, pos);

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

	//This is called *after* having skipped the first char of the number.
	Token takeNumber(Pos startPos) {
		skipWhile(CharUtils.isDigit);
		var isFloat = peek == '.';
		if (isFloat) {
			skip();
			if (!CharUtils.isDigit(peek)) throw exit(Loc.singleChar(pos), Err.TooMuchIndent);
		}
		this.tokenValue = sliceFrom(startPos);
		return isFloat ? Token.FloatLiteral : Token.IntLiteral;
	}

	Token takeNameOrKeyword(Pos startPos) {
		skipWhile(CharUtils.isNameChar);
		var s = sliceFrom(startPos);
		var t = TokenU.keywordFromName(s);
		if (t.get(out var tok)) return tok;
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
					pos--;
					return Token.Dedent;
				} else {
					return Token.EOF;
				}

			case ' ':
				if (peek == '\n') throw exit(Loc.singleChar(pos), Err.TrailingSpace);
				throw TODO();

			case '\n':
				return handleNewline();

			case '|':
				throw TODO();

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
				return takeStringLike(Token.TyName, start, CharUtils.isNameChar);

			case '-':
				var next = readChar();
				if (CharUtils.isDigit(next)) return takeNumber(start);
				goto case '+';

			case '+': case '*': case '/': case '^': case '?': case '<': case '>':
				return takeStringLike(Token.Operator, start, isOperatorChar);

			default:
				throw exit(Loc.singleChar(start), new Err.UnrecognizedCharacter(ch));
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
		doTimes(indent - 1, () => expectCharacter('\t'));
		if (tryTake('\t'))
			return NewlineOrDedent.Newline;
		else {
			indent--;
			return NewlineOrDedent.Dedent;
		}
	}

	protected enum NewlineOrIndent { Newline, Indent }
	protected NewlineOrIndent takeNewlineOrIndent() {
		expectCharacter('\n');
		skipNewlines();
		doTimes(indent, () => expectCharacter('\t'));
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
	protected void takeRparen() => expectCharacter(')');
	protected void takeComma() => expectCharacter(',');
	protected void takeDot() => expectCharacter('.');
	protected bool tryTakeRparen() => tryTake(')');
	protected bool tryTakeDot() => tryTake('.');

	protected void takeSpecificKeyword(Token kw) {
		var startPos = pos;
		var actual = takeKeyword();
		if (actual != kw) throw unexpected(startPos, TokenU.TokenName(kw), TokenU.TokenName(actual));
	}

	protected string takeNameString() {
		var startPos = pos;
		expectCharacter("(non-type) name", CharUtils.isLowerCaseLetter);
		skipWhile(CharUtils.isNameChar);
		return sliceFrom(startPos);
	}

	protected Sym takeName() => Sym.of(takeNameString());

	protected Sym takeTyName() {
		var startPos = pos;
		expectCharacter("type name", CharUtils.isUpperCaseLetter);
		skipWhile(CharUtils.isNameChar);
		return Sym.of(sliceFrom(startPos));
	}

	protected ParserExit unexpected(Pos startPos, string expectedDesc, Token token) =>
		unexpected(startPos, expectedDesc, TokenU.TokenName(token));
	protected ParserExit unexpected(Pos startPos, string expectedDesc, string actualDesc) =>
		exit(locFrom(startPos), new Err.UnexpectedToken(expectedDesc, actualDesc));

	protected Token takeKeyword() {
		var startPos = pos;
		expectCharacter("keyword", CharUtils.isLowerCaseLetter);
		skipWhile(CharUtils.isNameChar);
		var name = sliceFrom(startPos);
		return TokenU.keywordFromName(name).or(() => throw unexpected(startPos, "keyword", name));
	}
}
