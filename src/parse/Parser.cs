using System;
using System.Diagnostics;
using System.Text;
using static ErrU;

abstract class Lexer {
	readonly string source;
	// Index of the character we are *about* to take.
	int pos = 0;
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

    /*protected class QuotePart {
        string text;
        /** False if we start a new interpolation. * /
        bool isEndOfQuote;
        public QuotePart(string text, bool isEndOfQuote) {
            this.text = text;
            this.isEndOfQuote = isEndOfQuote;
        }
    }*/

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

    //This is called *after* having skipped the first char of the number.
    private Token takeNumber(int startPos) {
        skipWhile(isDigit);
        var isFloat = peek == '.';
        if (isFloat) {
            skip();
            must(isDigit(peek), Loc.singleChar(pos), Err.TooMuchIndent);
        }
        this.value = source.Substring(startPos, pos - startPos);
        return isFloat ? Token.FloatLiteral : Token.IntLiteral;
    }

    private Token takeStringLike(Token kind, int startPos, Func<char, bool> pred) {
        skipWhile(pred);
        this.value = source.Substring(startPos, pos - startPos);
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
}

//!!!
abstract class Err {
    private Err() {}

    sealed class PlainErr : Err {
        readonly string message;
        public PlainErr(string s) { this.message = s; }
    }

    public static Err TooMuchIndent => new PlainErr("Too much indent!");
    public static Err LeadingSpace => new PlainErr("Leading space!");
    public static Err TrailingSpace => new PlainErr("Trailing space");

    public class UnrecognizedCharacter : Err {
        readonly char ch;
        public UnrecognizedCharacter(char ch) { this.ch = ch; }
    }
}

sealed class CompileError : Exception {
    readonly Loc loc;
    readonly Err err;
    public CompileError(Loc loc, Err err) : base() {
        this.loc = loc;
        this.err = err;
    }
}

static class ErrU {
    public static void raise(Loc loc, Err err) => throw new CompileError(loc, err);

    public static void must(bool cond, Loc loc, Err err) {
        if (!cond) raise(loc, err);
    }
}

/*private Token() {}

abstract class NameLike {
    public readonly Sym name;
    public NameLike(Sym name) { this.name = name; }
    public override string ToString() => $"{GetType().Name}({name})";
}
class Name : NameLike {
    public Name(Sym name) : base(name) {}
}
class TyName : NameLike {
    public TyName(Sym name) : base(name) {}
}
class Operator : NameLike {
    public Operator(Sym name) : base(name) {}
}
class Literal : Token {
    public readonly Model.LiteralValue value;
    Literal(Model.LiteralValue value) { this.value = value; }
    public override string ToString() => value.ToString();
}
class QuoteStart : Token {
    public readonly string head;
    QuoteStart(string head) { this.head = head; }
}
class Keyword : Token {
    public readonly Kw kw;
    Keyword(Kw kw) { this.kw = kw; }

    public override string ToString() => KwName(kw);
}*/

public enum Token {
    // These will also write to lexer.value
    Name,
    TyName,
    Operator,
    StringLiteral,
    FloatLiteral,
    IntLiteral,
    QuoteStart,

    // Keywords that resemble names
    Def,
    Enum,
    Fun,
    Generic,
    Import,
    Slots,
    Val,
    Var,

    // Other keywords
    Backslash,
    Underscore,
    Equals,
    Indent,
    Dedent,
    Newline,
    Lparen,
    Rparen,
    Lbracket,
    Rbracket,
    Lcurly,
    Rcurly,
    EOF,
    Colon,
    Comma,
    Dot,
    DotDot,
}

static class TokenU {
    private static Token? keywordFromName(string s) {
        switch (s) {
            case "def": return Token.Def;
            case "enum": return Token.Enum;
            case "fun": return Token.Fun;
            case "generic": return Token.Generic;
            case "import": return Token.Import;
            case "slots": return Token.Slots;
            case "val": return Token.Val;
            case "var": return Token.Var;
            default: return null;
        }
    }

    static string TokenName(Token tk) {
        switch (tk) {
            case Token.Name: return "name";
            case Token.TyName: return "ty-name";
            case Token.Operator: return "operator";
            case Token.StringLiteral: return "string literal";
            case Token.IntLiteral: return "number literal";
            case Token.FloatLiteral: return "float literal";
            case Token.QuoteStart: return "quote start";

            case Token.Def: return "def";
            case Token.Enum: return "enum";
            case Token.Fun: return "fun";
            case Token.Generic: return "generic";
            case Token.Import: return "import";
            case Token.Slots: return "slots";
            case Token.Val: return "val";
            case Token.Var: return "var";

            case Token.Backslash: return "\\";
            case Token.Underscore: return "_";
            case Token.Equals: return "=";
            case Token.Indent: return "->";
            case Token.Dedent: return "<-";
            case Token.Newline: return "\\n";
            case Token.Lparen: return "(";
            case Token.Rparen: return ")";
            case Token.Lbracket: return "[";
            case Token.Rbracket: return "]";
            case Token.Lcurly: return "{";
            case Token.Rcurly: return "}";
            case Token.EOF: return "EOF";

            case Token.Colon: return ":";
            case Token.Comma: return ",";
            case Token.Dot: return ".";
            case Token.DotDot: return "..";
            default: throw new Exception(tk.ToString());
        }
    }
}
