using System;
using System.Diagnostics;
using System.Text;
using static ErrU;

abstract class Lexer {
	readonly string source;
	// Index of the character we are *about* to take.
	protected int pos { get; private set; } = 0;
	int indent = 0;
	// Number of Token.Dedent we have to output before continuing to read.
	int dedenting = 0;

    // This is set after taking one of several kinds of token, such as Name or NumberLiteral
    protected string value;

	protected Lexer(string source) {
		//Ensure "source" ends in a newline.
		//Add an EOF too.
		if (!source.EndsWith("\n")) {
			source += "\n";
		}
		this.source = source + '\0';

		skipNewlines();
	}

	protected Loc locFrom(int start) => new Loc(start, pos);

	private char peek => source[pos];

	private char readChar() {
		var ch = source[pos];
		pos++;
		return ch;
	}

	private void skip() { readChar(); }

    //Returns number skipped.
    private int skipWhile(Func<char, bool> pred) {
        Debug.Assert(!pred('\0'));
        var start = pos;
        if (pred(peek)) {
            char ch;
            do {
                pos++;
                ch = source[pos];
            } while (pred(ch));
        }
        return pos - start;
    }

    private void skipNewlines() {
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
        this.value = s.ToString();
        return isEnd ? QuoteEnd.QuoteEnd : QuoteEnd.QuoteInterpolation;
    }

    private static char escape(char escaped) {
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

    private string sliceFrom(int startPos) {
        return source.Substring(startPos, pos - startPos);
    }

    //This is called *after* having skipped the first char of the number.
    private Token takeNumber(int startPos) {
        skipWhile(isDigit);
        var isFloat = peek == '.';
        if (isFloat) {
            skip();
            must(isDigit(peek), Loc.singleChar(pos), Err.TooMuchIndent);
        }
        this.value = sliceFrom(startPos);
        return isFloat ? Token.FloatLiteral : Token.IntLiteral;
    }

    private Token takeStringLike(Token kind, int startPos, Func<char, bool> pred) {
        skipWhile(pred);
        this.value = sliceFrom(startPos);
        return kind;
    }

    private static bool isDigit(char ch) {
        return '0' <= ch && ch <= '9';
    }

    private int lexIndent() {
        var start = pos;
        skipWhile(ch => ch == '\t');
        var count = pos - start;
        must(peek != ' ', locFrom(start), Err.LeadingSpace);
        return count;
    }

    private Token handleNewline(bool indentOnly) {
        skipNewlines();
        var oldIndent = indent;
        indent = lexIndent();
        if (indent > oldIndent) {
            must(indent == oldIndent + 1, Loc.singleChar(pos), Err.TooMuchIndent);
            return Token.Indent;
        } else if (indent == oldIndent) {
            return indentOnly ? nextToken() : Token.Newline;
        } else {
            dedenting = oldIndent - indent - 1;
            return Token.Dedent;
        }
    }

    protected Token nextToken() {
        if (dedenting != 0) {
            dedenting--;
            return Token.Dedent;
        }
        return takeNext();
    }

    private Token takeNext() {
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
                must(peek != '\n', Loc.singleChar(pos), Err.TrailingSpace);
                throw new Exception("TODO");

            case '\n':
                return handleNewline(false);

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
                return takeStringLike(Token.Name, start, isNameChar);

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
                throw new CompileError(Loc.singleChar(start), new Err.UnrecognizedCharacter(ch));
        }
    }

    private static bool isNameChar(char ch) {
        return 'a' <= ch && ch <= 'z' ||
            'A' <= ch && ch <= 'Z' ||
                isDigit(ch) || ch == '-';
    }

    private static bool isOperatorChar(char ch) {
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

    private bool tryTake(char ch) {
        if (peek == ch) {
            skip();
            return true;
        }
        return false;
    }

    private void expectCharacter(char expected) {
        var actual = readChar();
        if (actual != expected) {
            raise(Loc.singleChar(pos), new Err.UnexpectedCharacter(actual, $"'{expected}'"));
        }
    }

    private void expectCharacter(string expected, Func<char, bool> pred) {
        var ch = readChar();
        if (!pred(ch)) {
            raise(Loc.singleChar(pos), new Err.UnexpectedCharacter(ch, expected));
        }
    }

    protected bool tryTakeNewline() {
        if (!tryTake('\n')) return false;
        while (tryTake('\n')) {}
        for (var i = this.indent; i >= 0; i--)
            expectCharacter('\t');
        return true;
    }

    protected void takeSpace() => expectCharacter(' ');
    protected void takeLparen() => expectCharacter('(');
    protected void takeComma() => expectCharacter(',');
    protected void takeDot() => expectCharacter('.');
    protected bool tryTakeRParen() => tryTake(')');
    protected bool tryTakeDot() => tryTake('.');

    //What's this for?
    protected Token takeKeyword() {
        var startPos = pos;
        expectCharacter("keyword", ch => 'a' <= ch && ch <= 'z');
        skipWhile(isNameChar);
        var name = sliceFrom(startPos);
        return TokenU.keywordFromName(name) ?? unexpected<Token>(startPos, name);
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

    protected void takeIndent() {
        this.indent++;
        expectCharacter('\n');
        for (var i = 0; i < this.indent; i++)
            expectCharacter('\t');
    }

    protected T unexpected<T>(int startPos, string desc) {
        return raise<T>(locFrom(startPos), new Err.UnexpectedToken(desc));
    }
}
