using System;

internal enum Token {
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
	EOF, //TODO: should never be reached as a token, just use unexpected() instead
	Colon,
	Comma,
	Dot,
	DotDot,
}

static class TokenU {
	internal static Token? keywordFromName(string s) {
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

	internal static string TokenName(Token tk) {
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
