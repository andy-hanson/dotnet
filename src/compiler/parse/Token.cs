using System;
using System.Collections.Generic;
using System.Reflection;

[AttributeUsage(AttributeTargets.Field)]
sealed class TextAttribute : Attribute {
	internal readonly string text;
	internal TextAttribute(string text) { this.text = text; }
}

internal enum Token {
	Nil, // Reserved so that default(Token) is available for use by Op

	// These will also write to lexer.value
	Name,
	TyName,
	Operator,
	NatLiteral,
	IntLiteral,
	RealLiteral,
	StringLiteral,
	QuoteStart,

	// Keywords
	[Text("abstract")]
	Abstract,
	[Text("assert")]
	Assert,
	[Text("catch")]
	Catch,
	[Text("def")]
	Def,
	[Text("do")]
	Do,
	[Text("enum")]
	Enum,
	[Text("else")]
	Else,
	[Text("false")]
	False,
	[Text("finally")]
	Finally,
	[Text("for")]
	For,
	[Text("fun")]
	Fun,
	[Text("generic")]
	Generic,
	[Text("get")]
	Get,
	[Text("if")]
	If,
	[Text("io")]
	Io,
	[Text("import")]
	Import,
	[Text("in")]
	In,
	[Text("is")]
	Is,
	[Text("new")]
	New,
	[Text("pass")]
	Pass,
	[Text("recur")]
	Recur,
	[Text("self")]
	Self,
	[Text("set")]
	Set,
	[Text("slots")]
	Slots,
	[Text("then")]
	Then,
	[Text("true")]
	True,
	[Text("try")]
	Try,
	[Text("val")]
	Val,
	[Text("var")]
	Var,
	[Text("when")]
	When,

	// Punctuation
	Backslash,
	BracketL,
	BracketR,
	Colon,
	ColonEquals,
	Comma,
	CurlyL,
	CurlyR,
	Dedent,
	Dot,
	DotDot,
	EOF,
	Equals,
	Indent,
	Newline,
	ParenL,
	ParenR,
	Space,
	Underscore,
}

static class TokenU {
	static readonly Dictionary<Token, string> keywordToText = new Dictionary<Token, string>();
	static readonly Dictionary<string, Token> textToKeyword = new Dictionary<string, Token>();

	static TokenU() {
		foreach (var field in typeof(Token).GetFields()) {
			var textAttr = field.GetCustomAttribute<TextAttribute>();
			if (textAttr == null)
				continue;

			var text = textAttr.text;
			var token = (Token)field.GetValue(null);
			keywordToText.Add(token, text);
			textToKeyword.Add(text, token);
		}
	}

	internal static bool keywordFromName(string name, out Token token) =>
		textToKeyword.TryGetValue(name, out token);

	internal static string TokenName(Token tk) {
		switch (tk) {
			case Token.Name: return "name";
			case Token.TyName: return "ty-name";
			case Token.Operator: return "operator";
			case Token.NatLiteral: return "Nat literal";
			case Token.IntLiteral: return "Int literal";
			case Token.RealLiteral: return "Float literal";
			case Token.StringLiteral: return "String literal";
			case Token.QuoteStart: return "quote start";

			case Token.Space: return "<space>";
			case Token.Backslash: return "\\";
			case Token.Underscore: return "_";
			case Token.Equals: return "=";
			case Token.Indent: return "->";
			case Token.Dedent: return "<-";
			case Token.Newline: return "\\n";
			case Token.ParenL: return "(";
			case Token.ParenR: return ")";
			case Token.BracketL: return "[";
			case Token.BracketR: return "]";
			case Token.CurlyL: return "{";
			case Token.CurlyR: return "}";
			case Token.EOF: return "EOF";

			case Token.Colon: return ":";
			case Token.Comma: return ",";
			case Token.Dot: return ".";
			case Token.DotDot: return "..";
			default:
				return keywordToText[tk];
		}
	}
}
