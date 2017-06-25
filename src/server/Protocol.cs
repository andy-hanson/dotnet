using System;

using Json;
using static Utils;

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

	abstract class Response : ToData<Response> {
		private Response() {}
		public override bool Equals(object o) => throw new NotImplementedException();
		public override int GetHashCode() => throw new NotImplementedException();
		public abstract bool deepEqual(Response r);
		public abstract Dat toDat();

		internal sealed class DefinitionResponse : Response, ToData<DefinitionResponse> {
			internal readonly string uri;
			internal readonly Range range;
			internal DefinitionResponse(string uri, Range range) {
				this.uri = uri;
				this.range = range;
			}

			public override bool deepEqual(Response r) => r is DefinitionResponse d && deepEqual(d);
			public bool deepEqual(DefinitionResponse d) => uri == d.uri && range.deepEqual(d.range);
			public override Dat toDat() => Dat.of(this, nameof(uri), Dat.str(uri), nameof(range), range);
		}

		internal sealed class FindAllReferencesResponse : Response, ToData<FindAllReferencesResponse> {
			internal readonly Arr<Location> refs;
			internal FindAllReferencesResponse(Arr<Location> refs) { this.refs = refs; }

			public override bool deepEqual(Response r) => r is FindAllReferencesResponse f && deepEqual(f);
			public bool deepEqual(FindAllReferencesResponse f) => refs.deepEqual(f.refs);
			public override Dat toDat() => Dat.of(this, nameof(refs), Dat.arr(refs));
		}

		internal sealed class DocumentHighlightsResponse : Response, ToData<DocumentHighlightsResponse> {
			internal readonly Arr<DocumentHighlight> highlights;
			internal DocumentHighlightsResponse(Arr<DocumentHighlight> highlights) { this.highlights = highlights; }

			public override bool deepEqual(Response r) => r is DocumentHighlightsResponse && deepEqual(r);
			public bool deepEqual(DocumentHighlightsResponse d) => highlights.deepEqual(d.highlights);
			public override Dat toDat() => Dat.of(this, nameof(highlights), Dat.arr(highlights));
		}

		internal sealed class SignatureHelpResponse : Response, ToData<SignatureHelpResponse>, ToJsonSpecial {
			internal readonly Arr<SignatureInformation> signatures;
			internal readonly OpUint activeSignature;
			internal readonly OpUint activeParameter;
			internal SignatureHelpResponse(Arr<SignatureInformation> signatures, OpUint activeSignature, OpUint activeParameter) {
				this.signatures = signatures;
				this.activeSignature = activeSignature;
				this.activeParameter = activeParameter;
			}

			public override bool deepEqual(Response r) => r is SignatureHelpResponse s && deepEqual(s);
			public bool deepEqual(SignatureHelpResponse s) =>
				signatures.deepEqual(s.signatures) && activeSignature.deepEqual(s.activeSignature) && activeParameter.deepEqual(s.activeParameter);
			public override Dat toDat() => Dat.of(this, nameof(signatures), Dat.arr(signatures), nameof(activeSignature), activeSignature, nameof(activeParameter), activeParameter);
			void ToJsonSpecial.toJsonSpecial(JsonWriter j) =>
				j.writeDictWithTwoOptionalValues(
					nameof(signatures), signatures,
					nameof(activeSignature), activeSignature,
					nameof(activeParameter), activeParameter);
		}

		// TODO: need this class? Just use the array directly?
		internal sealed class CompletionResponse : Response, ToData<CompletionResponse> {
			internal readonly Arr<CompletionItem> completions;
			internal CompletionResponse(Arr<CompletionItem> completions) { this.completions = completions; }

			public override bool deepEqual(Response r) => r is CompletionResponse c && deepEqual(c);
			public bool deepEqual(CompletionResponse c) => completions.deepEqual(c.completions);
			public override Dat toDat() => Dat.arr(completions);
			//void ToJsonSpecial.toJsonSpecial(JsonWriter j) =>
			//	j.writeArray(completions);
		}

		internal sealed class HoverResponse : Response, ToData<HoverResponse> {
			internal readonly string contents;
			internal HoverResponse(string contents) { this.contents = contents; }

			public override bool deepEqual(Response r) => r is HoverResponse h && deepEqual(h);
			public bool deepEqual(HoverResponse h) => contents == h.contents;
			public override Dat toDat() => Dat.of(this, nameof(contents), Dat.str(contents));
		}
	}

	struct Initialize : ToData<Initialize> {
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

		public bool deepEqual(Initialize other) =>
			rqId == other.rqId &&
			processId == other.processId &&
			rootPath.deepEqual(other.rootPath) &&
			rootUri.deepEqual(other.rootUri) &&
			trace == other.trace;
		public Dat toDat() => Dat.of(this,
			nameof(rqId),
			Dat.num(rqId),
			nameof(processId), Dat.num(processId),
			nameof(rootPath), Dat.op(rootPath),
			nameof(rootUri), Dat.op(rootUri),
			nameof(trace), Dat.str(trace));
	}

	// 1 word
	// https://github.com/Microsoft/language-server-protocol/blob/master/protocol.md#position
	struct Position : ToData<Position> {
		/** 0-indexed */
		internal readonly uint line;
		/** 0-indexed */
		internal readonly uint character;
		internal Position(uint line, uint character) { this.line = line; this.character = character; }

		public bool deepEqual(Position p) => line == p.line && character == p.character;
		public Dat toDat() => Dat.of(this, nameof(line), Dat.num(line), nameof(character), Dat.num(character));
	}

	// 2 words
	// https://github.com/Microsoft/language-server-protocol/blob/master/protocol.md#range
	struct Range : ToData<Range> {
		internal readonly Position start;
		internal readonly Position end;
		internal Range(Position start, Position end) { this.start = start; this.end = end; }

		public bool deepEqual(Range r) => start.deepEqual(r.start) && end.deepEqual(r.end);
		public Dat toDat() => Dat.of(this, nameof(start), start, nameof(end), end);
	}

	// 3 words
	struct Location : ToData<Location> {
		internal readonly string uri;
		internal readonly Range range;
		internal Location(string uri, Range range) { this.uri = uri; this.range = range; }

		public bool deepEqual(Location l) => uri == l.uri && range.deepEqual(l.range);
		public Dat toDat() => Dat.of(this, nameof(uri), Dat.str(uri), nameof(range), range);
	}

	/** Warning: 7 words in size. Pass by reference. */
	struct Diagnostic : ToData<Diagnostic>, ToJsonSpecial {
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

		public bool deepEqual(Diagnostic d) =>
			range.deepEqual(d.range) &&
			severity.equalsRaw(d.severity) &&
			code.deepEqual(d.code) &&
			source.deepEqual(d.source) &&
			message == d.message;

		public Dat toDat() => Dat.of(this,
			nameof(range), range,
			nameof(severity), Dat.op(severity.mapToUint(s => (uint)s)),
			nameof(code), Dat.op(code),
			nameof(source), Dat.op(source),
			nameof(message), Dat.str(message));

		void ToJsonSpecial.toJsonSpecial(JsonWriter j) {
			j.writeDictWithMiddleThreeOptionalValues(
				nameof(range), range,
				nameof(severity), severity.get(out var v) ? OpUint.Some((uint)v) : OpUint.None,
				nameof(code), code,
				nameof(source), source,
				nameof(message), message);
		}

		internal enum Severity {
			Nil, // For use by Op
			Error = 1,
			Warning = 2,
			Information = 3,
			Hint = 4
		}
	}

	struct DocumentHighlight : ToData<DocumentHighlight> {
		internal readonly Range range;
		internal readonly Kind kind;
		internal DocumentHighlight(Range range, Kind kind) { this.range = range; this.kind = kind; }

		internal enum Kind {
			Text = 1,
			Read = 2,
			Write = 3,
		}

		public bool deepEqual(DocumentHighlight d) => range.deepEqual(d.range) && kind == d.kind;
		public Dat toDat() => Dat.of(this, nameof(range), range, nameof(kind), Dat.num((uint)kind));
	}

	struct TextDocumentPositionParams : ToData<TextDocumentPositionParams> {
		internal readonly string textDocumentUri;
		internal readonly Position position;
		internal TextDocumentPositionParams(string textDocumentUri, Position position) {
			this.textDocumentUri = textDocumentUri;
			this.position = position;
		}

		public bool deepEqual(TextDocumentPositionParams t) => textDocumentUri == t.textDocumentUri && position.deepEqual(position);
		public Dat toDat() => Dat.of(this, nameof(textDocumentUri), Dat.str(textDocumentUri), nameof(position), position);
	}

	//TODO: more fields available (all optional)
	struct CompletionItem : ToData<CompletionItem> {
		internal readonly string label;
		internal CompletionItem(string label) { this.label = label; }

		public bool deepEqual(CompletionItem c) => label == c.label;
		public Dat toDat() => Dat.of(this, nameof(label), Dat.str(label));
	}

	struct SignatureInformation : ToData<SignatureInformation>, ToJsonSpecial {
		internal readonly string label;
		internal readonly Op<string> documentation;
		internal readonly Op<Arr<ParameterInformation>> parameters;
		internal SignatureInformation(string label, Op<string> documentation, Op<Arr<ParameterInformation>> parameters) {
			this.label = label;
			this.documentation = documentation;
			this.parameters = parameters;
		}

		public bool deepEqual(SignatureInformation s) =>
			label == s.label &&
			documentation.deepEqual(s.documentation) &&
			parameters.deepEqual(s.parameters, Arr.deepEqual);
		public Dat toDat() => Dat.of(this,
			nameof(label), Dat.str(label),
			nameof(documentation), Dat.op(documentation),
			nameof(parameters), Dat.op(parameters.map(Dat.arr)));
		void ToJsonSpecial.toJsonSpecial(JsonWriter j) =>
			j.writeDictWithTwoOptionalValues(
				nameof(label), label,
				nameof(documentation), documentation,
				nameof(parameters), parameters);
	}

	struct ParameterInformation : ToData<ParameterInformation>, ToJsonSpecial {
		internal readonly string label;
		internal readonly Op<string> documentation;
		internal ParameterInformation(string label, Op<string> documentation) { this.label = label; this.documentation = documentation; }

		public bool deepEqual(ParameterInformation p) => label == p.label && documentation.deepEqual(p.documentation);
		public Dat toDat() =>
			Dat.of(this, nameof(label), Dat.str(label), nameof(documentation), Dat.op(documentation));

		void ToJsonSpecial.toJsonSpecial(JsonWriter j) =>
			j.writeDictWithOneOptionalValue(
				nameof(label), label,
				nameof(documentation), documentation);
	}

	//TODO:document
	struct PublishDiagnostics : ToData<PublishDiagnostics> {
		readonly string uri;
		readonly Arr<Diagnostic> diagnostics;
		internal PublishDiagnostics(string uri, Arr<Diagnostic> diagnostics) {
			this.uri = uri;
			this.diagnostics = diagnostics;
		}

		public bool deepEqual(PublishDiagnostics p) => uri == p.uri && diagnostics.deepEqual(p.diagnostics);
		public Dat toDat() => Dat.of(this, nameof(uri), Dat.str(uri), nameof(diagnostics), Dat.arr(diagnostics));
	}

	//TODO:document
	struct InitResponse : ToData<InitResponse> {
		internal readonly uint textDocumentSync;
		internal readonly bool hoverProvider;
		internal readonly CompletionOptions completionProvider;
		internal readonly SignatureHelpOptions signatureHelpProvider;
		internal readonly bool definitionProvider;
		internal readonly bool documentHighlightProvider;
		internal readonly bool referencesProvider;

		internal InitResponse(bool dummy) {
			unused(dummy);
			textDocumentSync = 1;
			hoverProvider = true;
			completionProvider = new CompletionOptions(true);
			signatureHelpProvider = new SignatureHelpOptions(true);
			definitionProvider = true;
			documentHighlightProvider = true;
			referencesProvider = true;
		}

		public bool deepEqual(InitResponse i) => throw new NotImplementedException();
		public Dat toDat() => Dat.of(this,
			nameof(textDocumentSync), Dat.num(textDocumentSync),
			nameof(hoverProvider), Dat.boolean(hoverProvider),
			nameof(completionProvider), completionProvider,
			nameof(signatureHelpProvider), signatureHelpProvider,
			nameof(definitionProvider), Dat.boolean(definitionProvider),
			nameof(documentHighlightProvider), Dat.boolean(documentHighlightProvider),
			nameof(referencesProvider), Dat.boolean(referencesProvider));

		internal struct CompletionOptions : ToData<CompletionOptions> {
			internal bool resolveProvider;
			internal Arr<char> triggerCharacters;

			internal CompletionOptions(bool dummy) {
				unused(dummy);
				resolveProvider = false;
				triggerCharacters = Arr.of('.');
			}

			public bool deepEqual(CompletionOptions c) => throw new NotImplementedException();
			public Dat toDat() => Dat.of(this,
				nameof(resolveProvider), Dat.boolean(resolveProvider),
				nameof(triggerCharacters), Dat.arr(triggerCharacters));
		}

		internal struct SignatureHelpOptions : ToData<SignatureHelpOptions> {
			internal Arr<char> triggerCharacters;

			internal SignatureHelpOptions(bool dummy) {
				unused(dummy);
				triggerCharacters = Arr.of(' ');
			}

			public bool deepEqual(SignatureHelpOptions c) => throw new NotImplementedException();
			public Dat toDat() => Dat.of(this, nameof(triggerCharacters), Dat.arr(triggerCharacters));
		}
	}
}
