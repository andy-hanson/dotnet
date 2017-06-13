using Json;

namespace Lsp {
	abstract class LspMethod {
		LspMethod() {}

		//Not an ordinary request because the response is undefined.
		internal sealed class Shutdown : LspMethod {
			internal readonly uint id;
			internal Shutdown(uint id) { this.id = id; }
		}

		/** A Notification doesn't need a response. */
		internal class Notification : LspMethod {
			Notification() {}
			internal static readonly Notification Initialized = new Notification();
			internal static readonly Notification Ignore = new Notification();
			/**
			https://github.com/Microsoft/language-server-protocol/blob/master/protocol.md#didchangeconfiguration-notification
			{
				"jsonrpc":"2.0",
				"method":"workspace/didChangeConfiguration",
				"params":{
					"settings":{
						"languageServerExample":{
							"maxNumberOfProblems":100,
							"trace":{
								"server":"off"
							}
						}
					}
				}
			}
			*/
			internal static readonly Notification DidChangeConfiguration = new Notification();

			/*
			{
				"jsonrpc":"2.0",
				"method":"textDocument/didOpen",
				"params":{
					"textDocument":{
						"uri":"file:///home/andy/git/n/VSCODETEST/test.txt",
						"languageId":"plaintext",
						"version":1,
						"text":"abcd\nefgh\njjjj\nkkkk\n\n"
					}
				}
			}
			*/
			internal sealed class TextDocumentDidOpen : Notification {
				internal readonly string uri;
				internal readonly string languageId;
				internal readonly uint version;
				internal readonly string text;
				internal TextDocumentDidOpen(string uri, string languageId, uint version, string text) : base() {
					this.uri = uri;
					this.languageId = languageId;
					this.version = version;
					this.text = text;
				}
			}

			/*
			{
				"jsonrpc":"2.0",
				"method":"textDocument/didChange",
				"params": {
					"textDocument": {
						"uri":"file:///home/andy/git/n/VSCODETEST/test.txt",
						"version":4
					},
					"contentChanges": [
						{"text":"abcd\nefgh\njjjj\nkkkk\n\nsss"}
					]
				}
			}
			*/
			internal sealed class TextDocumentDidChange : Notification {
				internal readonly string uri;
				internal readonly uint version;
				internal readonly string text;
				internal TextDocumentDidChange(string uri, uint version, string text) : base() {
					this.uri = uri;
					this.version = version;
					this.text = text;
				}
			}

			/*
			{
				"jsonrpc":"2.0",
				"method":"textDocument/didSave",
				"params": {
					"textDocument": {
						"uri":"file:///home/andy/temp/SAMPLE/a.txt",
						"version":17
					}
				}
			}
			*/
			internal sealed class TextDocumentDidSave : Notification {
				internal readonly string uri;
				internal readonly uint version;
				internal TextDocumentDidSave(string uri, uint version) : base() {
					this.uri = uri;
					this.version = version;
				}
			}
		}

		internal abstract class Request : LspMethod {
			internal readonly uint id;
			Request(uint id) : base() { this.id = id; }

			/*
			{
				"jsonrpc":"2.0",
				"id":1,
				"method":"textDocument/definition",
				"params":{
					"textDocument":{
						"uri":"/test.txt"
					},
					"position":{
						"line":0,
						"character":0
					}
				}
			}
			*/
			internal sealed class Definition : Request {
				internal readonly TextDocumentPositionParams pms;
				internal Definition(uint id, TextDocumentPositionParams pms) : base(id) {
					this.pms = pms;
				}
			}

			//TODO:DOC
			internal sealed class Completion : Request {
				internal readonly TextDocumentPositionParams pms;
				internal Completion(uint id, TextDocumentPositionParams pms) : base(id) {
					this.pms = pms;
				}
			}

			/*
			https://github.com/Microsoft/language-server-protocol/blob/master/protocol.md#find-references-request
			{
				"jsonrpc": "2.0",
				"id": 2,
				"method": "textDocument/references",
				"params": {
					"textDocument": {
					"uri": "file:///home/andy/temp/SAMPLE/a.txt"
					},
					"position": {
					"line": 2,
					"character": 2
					},
					"context": {
					"includeDeclaration": true
					}
				}
			}
			*/
			internal sealed class FindAllReferences : Request {
				internal readonly TextDocumentPositionParams pms;
				internal readonly bool includeDeclaration;
				internal FindAllReferences(uint id, TextDocumentPositionParams pms, bool includeDeclaration) : base(id) {
					this.pms = pms;
					this.includeDeclaration = includeDeclaration;
				}
			}

			// https://github.com/Microsoft/language-server-protocol/blob/master/protocol.md#document-highlights-request
			internal sealed class DocumentHighlights : Request {
				internal readonly TextDocumentPositionParams pms;
				internal DocumentHighlights(uint id, TextDocumentPositionParams pms) : base(id) {
					this.pms = pms;
				}
			}

			internal sealed class SignatureHelp : Request {
				internal readonly TextDocumentPositionParams pms;
				internal SignatureHelp(uint id, TextDocumentPositionParams pms) : base(id) {
					this.pms = pms;
				}
			}

			internal sealed class Hover : Request {
				internal readonly TextDocumentPositionParams pms;
				internal Hover(uint id, TextDocumentPositionParams pms) : base(id) {
					this.pms = pms;
				}
			}
		}
	}

	abstract class Response : ToJson {
		private Response() {}
		public abstract void toJson(JsonWriter j);

		internal sealed class DefinitionResponse : Response {
			internal readonly string uri;
			internal readonly Range range;
			internal DefinitionResponse(string uri, Range range) {
				this.uri = uri;
				this.range = range;
			}

			public override void toJson(JsonWriter j) {
				j.writeDict("uri", uri, "range", range);
			}
		}

		internal sealed class FindAllReferencesResponse : Response {
			internal readonly Arr<Location> refs;
			internal FindAllReferencesResponse(Arr<Location> refs) { this.refs = refs; }

			public override void toJson(JsonWriter j) {
				j.writeArray(refs);
			}
		}

		internal sealed class DocumentHighlightsResponse : Response {
			internal readonly Arr<DocumentHighlight> highlights;
			internal DocumentHighlightsResponse(Arr<DocumentHighlight> highlights) { this.highlights = highlights; }

			public override void toJson(JsonWriter j) {
				j.writeArray(highlights);
			}
		}

		internal sealed class SignatureHelpResponse : Response {
			internal readonly Arr<SignatureInformation> signatures;
			internal readonly OpUint activeSignature;
			internal readonly OpUint activeParameter;
			internal SignatureHelpResponse(Arr<SignatureInformation> signatures, OpUint activeSignature, OpUint activeParameter) {
				this.signatures = signatures;
				this.activeSignature = activeSignature;
				this.activeParameter = activeParameter;
			}

			public override void toJson(JsonWriter j) {
				j.writeDictWithTwoOptionalValues("signatures", signatures, "activeSignature", activeSignature, "activeParameter", activeParameter);
			}
		}

		internal sealed class CompletionResponse : Response {
			internal readonly Arr<CompletionItem> completions;
			internal CompletionResponse(Arr<CompletionItem> completions) { this.completions = completions; }

			public override void toJson(JsonWriter j) {
				j.writeArray(completions);
			}
		}

		internal sealed class HoverResponse : Response {
			internal readonly string contents;
			internal HoverResponse(string contents) { this.contents = contents; }

			public override void toJson(JsonWriter j) {
				j.writeDict("contents", contents);
			}
		}
	}

	struct Initialize {
		internal readonly uint rqId;
		internal readonly uint processId;
		internal readonly Op<string> rootPath;
		internal readonly Op<string> rootUri;
		internal readonly string trace;
		internal Initialize(uint rqId, uint processId, Op<string> rootPath, Op<string> rootUri, string trace) {
			this.rqId = rqId;
			this.processId = processId;
			this.rootPath = rootPath;
			this.rootUri = rootUri;
			this.trace = trace;
		}
	}

	// 1 word
	// https://github.com/Microsoft/language-server-protocol/blob/master/protocol.md#position
	struct Position : ToJson {
		/** 0-indexed */
		internal readonly uint line;
		/** 0-indexed */
		internal readonly uint character;
		internal Position(uint line, uint character) { this.line = line; this.character = character; }

		void ToJson.toJson(JsonWriter j) {
			j.writeDict("line", line, "character", character);
		}
	}

	// 2 words
	// https://github.com/Microsoft/language-server-protocol/blob/master/protocol.md#range
	struct Range : ToJson {
		internal readonly Position start;
		internal readonly Position end;
		internal Range(Position start, Position end) { this.start = start; this.end = end; }

		void ToJson.toJson(JsonWriter j) {
			j.writeDict("start", start, "end", end);
		}
	}

	// 3 words
	struct Location : ToJson {
		internal readonly string uri;
		internal readonly Range range;
		internal Location(string uri, Range range) { this.uri = uri; this.range = range; }

		void ToJson.toJson(JsonWriter j) {
			j.writeDict("uri", uri, "range", range);
		}
	}

	/** Warning: 7 words in size. Pass by reference. */
	struct Diagnostic : ToJson {
		internal readonly Range range;
		internal readonly Op<Severity> severity; // Can't use Op as that would use default(SeveritY)
		internal readonly Op<string> code;
		internal readonly Op<string> source;
		internal readonly string message;
		internal Diagnostic(Range range, Op<Severity> severity, Op<string> code, Op<string> source, string message) {
			this.range = range;
			this.severity = severity;
			this.code = code;
			this.source = source;
			this.message = message;
		}

		void ToJson.toJson(JsonWriter j) {
			j.writeDictWithMiddleThreeOptionalValues(
				"range", range,
				"severity", severity.get(out var v) ? OpUint.Some((uint) v) : OpUint.None,
				"code", code,
				"source", source,
				"message", message);
		}

		internal enum Severity {
			Nil, // For use by Op
			Error = 1,
			Warning = 2,
			Information = 3,
			Hint = 4
		}
	}

	struct DocumentHighlight : ToJson {
		internal readonly Range range;
		internal readonly Kind kind;
		internal DocumentHighlight(Range range, Kind kind) { this.range = range; this.kind = kind; }

		internal enum Kind {
			Text = 1,
			Read = 2,
			Write = 3,
		}

		void ToJson.toJson(JsonWriter j) {
			j.writeDict("range", range, "kind", (uint) kind);
		}
	}

	struct TextDocumentPositionParams {
		internal readonly string textDocumentUri;
		internal readonly Position position;
		internal TextDocumentPositionParams(string textDocumentUri, Position position) {
			this.textDocumentUri = textDocumentUri;
			this.position = position;
		}
	}

	//TODO: more fields available (all optional)
	struct CompletionItem : ToJson {
		internal readonly string label;
		internal CompletionItem(string label) { this.label = label; }

		void ToJson.toJson(JsonWriter j) {
			j.writeDict("label", label);
		}
	}

	struct SignatureInformation : ToJson {
		internal readonly string label;
		internal readonly Op<string> documentation;
		internal readonly Op<Arr<ParameterInformation>> parameters;
		internal SignatureInformation(string label, Op<string> documentation, Op<Arr<ParameterInformation>> parameters) {
			this.label = label;
			this.documentation = documentation;
			this.parameters = parameters;
		}

		void ToJson.toJson(JsonWriter j) {
			j.writeDictWithTwoOptionalValues("label", label, "documentation", documentation, "parameters", parameters);
		}
	}

	struct ParameterInformation : ToJson {
		internal readonly string label;
		internal readonly Op<string> documentation;
		internal ParameterInformation(string label, Op<string> documentation) { this.label = label; this.documentation = documentation; }

		void ToJson.toJson(JsonWriter j) {
			j.writeDictWithOneOptionalValue("label", label, "documentation", documentation);
		}
	}

	//TODO:document
	struct PublishDiagnostics : ToJson {
		readonly string uri;
		readonly Arr<Diagnostic> diagnostics;
		internal PublishDiagnostics(string uri, Arr<Diagnostic> diagnostics) {
			this.uri = uri;
			this.diagnostics = diagnostics;
		}

		void ToJson.toJson(JsonWriter j) {
			j.writeDict<Diagnostic>("uri", uri, "diagnostics", diagnostics);
		}
	}

	//TODO:document
	struct InitResponse : ToJson {
		void ToJson.toJson(JsonWriter j) {
			j.writeDict(
				"textDocumentSync", 1,
				"hoverProvider", true,
				"completionProvider", new CompletionOptions(),
				"signatureHelpProvider", new SignatureHelpOptions(),
				"definitionProvider", true,
				"documentHighlightProvider", true,
				"referencesProvider", true);
		}

		struct CompletionOptions : ToJson {
			void ToJson.toJson(JsonWriter j) {
				j.writeDict("resolveProvider", false, "triggerCharacters", Arr.of("."));
			}
		}

		struct SignatureHelpOptions : ToJson {
			void ToJson.toJson(JsonWriter j) {
				j.writeDict("triggerCharacters", Arr.of(" "));
			}
		}
	}
}
