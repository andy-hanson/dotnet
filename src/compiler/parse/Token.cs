using System;

internal enum Token {
	Nil, // Reserved so that default(Token) is available for use by Op

	// These will also write to lexer.value
	Name,
	TyName,
	Operator,
	StringLiteral,
	FloatLiteral,
	IntLiteral,
	QuoteStart,

	// Keywords
	Abstract,
	Def,
	Do,
	Enum,
	Else,
	False,
	For,
	Fun,
	Generic,
	If,
	Impl,
	Import,
	In,
	Is,
	Pass,
	Self,
	Slots,
	Static,
	True,
	Val,
	Var,
	When,

	// Punctuation
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
	EOF, //TODO: should never be reached as a token, just use unexpected() instead
	Colon,
	Comma,
	Dot,
	DotDot,
}

static class TokenU {
	internal static Op<Token> keywordFromName(string s) {
		switch (s) {
			case "abstract":
				return Op.Some(Token.Abstract);
			case "def":
				return Op.Some(Token.Def);
			case "do":
				return Op.Some(Token.Do);
			case "else":
				return Op.Some(Token.Else);
			case "enum":
				return Op.Some(Token.Enum);
			case "false":
				return Op.Some(Token.False);
			case "for":
				return Op.Some(Token.For);
			case "fun":
				return Op.Some(Token.Fun);
			case "generic":
				return Op.Some(Token.Generic);
			case "if":
				return Op.Some(Token.If);
			case "impl":
				return Op.Some(Token.Impl);
			case "import":
				return Op.Some(Token.Import);
			case "in":
				return Op.Some(Token.In);
			case "is":
				return Op.Some(Token.Is);
			case "pass":
				return Op.Some(Token.Pass);
			case "self":
				return Op.Some(Token.Self);
			case "slots":
				return Op.Some(Token.Slots);
			case "static":
				return Op.Some(Token.Static);
			case "true":
				return Op.Some(Token.True);
			case "val":
				return Op.Some(Token.Val);
			case "var":
				return Op.Some(Token.Var);
			case "when":
				return Op.Some(Token.When);
			default:
				return Op<Token>.None;
		}
	}

	internal static string TokenName(Token tk) {
		switch (tk) {
			case Token.Name: return "name";
			case Token.TyName: return "ty-name";
			case Token.Operator: return "operator";
			case Token.StringLiteral: return "string literal";
			case Token.IntLiteral: return "number literal";
			case Token.FloatLiteral: return "float literal";
			case Token.QuoteStart: return "quote start";

			case Token.Abstract: return "abstract";
			case Token.Def: return "def";
			case Token.Do: return "do";
			case Token.Enum: return "enum";
			case Token.Else: return "else";
			case Token.False: return "false";
			case Token.For: return "for";
			case Token.Fun: return "fun";
			case Token.Generic: return "generic";
			case Token.If: return "if";
			case Token.Impl: return "impl";
			case Token.Import: return "import";
			case Token.In: return "in";
			case Token.Is: return "is";
			case Token.Pass: return "pass";
			case Token.Self: return "self";
			case Token.Slots: return "slots";
			case Token.Static: return "static";
			case Token.True: return "true";
			case Token.Val: return "val";
			case Token.Var: return "var";
			case Token.When: return "when";

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
