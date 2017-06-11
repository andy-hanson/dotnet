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

	// Keywords
	Abstract,
	Def,
	Enum,
	Else,
	False,
	Fun,
	Generic,
	Import,
	If,
	Pass,
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
	internal static Token? keywordFromName(string s) {
		switch (s) {
			case "abstract": return Token.Abstract;
			case "def": return Token.Def;
			case "else": return Token.Else;
			case "enum": return Token.Enum;
			case "false": return Token.False;
			case "fun": return Token.Fun;
			case "generic": return Token.Generic;
			case "if": return Token.If;
			case "import": return Token.Import;
			case "pass": return Token.Pass;
			case "slots": return Token.Slots;
			case "static": return Token.Static;
			case "true": return Token.True;
			case "val": return Token.Val;
			case "var": return Token.Var;
			case "when": return Token.When;
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

			case Token.Abstract: return "abstract";
			case Token.Def: return "def";
			case Token.Enum: return "enum";
			case Token.Else: return "else";
			case Token.False: return "false";
			case Token.Fun: return "fun";
			case Token.Generic: return "generic";
			case Token.Import: return "import";
			case Token.If: return "if";
			case Token.Pass: return "pass";
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
