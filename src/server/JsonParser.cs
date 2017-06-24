using System;

using Lsp;
using static Utils;

namespace Json {
	class JsonParser : JsonScanner {
		internal JsonParser(string source) : base(source) {}

		internal static Initialize parseInitialize(string source) => parseAny<Initialize>(source, p => p.parseInitialize());
		Initialize parseInitialize() {
			parseHeader();
			var id = readDictUintEntry("id");
			var method = readDictStrEntry("method");
			assert(method == "initialize");
			readDictKey("params");
			var res = parseInitialize(id);
			readDictEnd();
			return res;
		}

		internal static LspMethod parseMessage(string source) => parseAny(source, p => p.parseMessage());
		LspMethod parseMessage() {
			parseHeader();
			//Methods must have id.
			mayReadDictUintThenString("id", "method", last: false, intValue: out var id, strValue: out var methodName);
			readDictKey("params");
			var res = parseMethod(id, methodName);
			readDictEnd(); // End of whole message
			return res;
		}
		LspMethod parseMethod(OpUint id, string methodName) {
			switch (methodName) {
				case "initialized":
					// Nothing in params
					readEmptyDict();
					return LspMethod.Notification.Initialized;
				case "$/setTraceNotification":
				case "$/logTraceNotification":
				case "$/cancelRequest":
					// https://github.com/Microsoft/language-server-protocol/issues/109
					skipDict();
					return LspMethod.Notification.Ignore;
				case "workspace/didChangeConfiguration":
					readDictStart();
					readDictSkipDict("settings", last: true);
					return LspMethod.Notification.DidChangeConfiguration;
				case "workspace/didChangeWatchedFiles":
					//This is just for when the set of watched files changes. I want https://github.com/Microsoft/language-server-protocol/blob/master/protocol.md#didchangetextdocument-notification
					skipDict();
					return LspMethod.Notification.Ignore;
				case "textDocument/definition":
					return new LspMethod.Request.Definition(id.force, parseTextDocumentPositionParams());
				case "textDocument/didOpen":
					return parseTextDocumentDidOpen();
				case "textDocument/didChange":
					return parseTextDocumentDidChange();
				case "textDocument/didSave":
					return parseTextDocumentDidSave();
				case "textDocument/documentHighlight":
					return new LspMethod.Request.DocumentHighlights(id.force, parseTextDocumentPositionParams());
				case "textDocument/references":
					return parseFindAllReferences(id.force);
				case "textDocument/completion":
					return new LspMethod.Request.Completion(id.force, parseTextDocumentPositionParams());
				case "textDocument/hover":
					return new LspMethod.Request.Hover(id.force, parseTextDocumentPositionParams());
				case "textDocument/signatureHelp":
					return new LspMethod.Request.SignatureHelp(id.force, parseTextDocumentPositionParams());
				case "shutdown":
					readNull();
					return new LspMethod.Shutdown(id.force);
				default:
					throw TODO($"Unrecognized method: {methodName}");
			}
		}

		LspMethod.Notification.TextDocumentDidOpen parseTextDocumentDidOpen() {
			readDictStart();

			readDictKey("textDocument");
			readDictStart();
			var uri = readDictStrEntry("uri");
			var languageId = readDictStrEntry("languageId");
			var version = readDictUintEntry("version");
			var text = readDictStrEntry("text", last: true); // Closes "textDocument"

			readDictEnd(); // Closes "params"

			return new LspMethod.Notification.TextDocumentDidOpen(uri, languageId, version, text);
		}

		// TODO: this is so simple because we request to get the entire text each time.
		LspMethod.Notification.TextDocumentDidChange parseTextDocumentDidChange() {
			readDictStart();

			readDictKey("textDocument");
			readDictStart();
			var uri = readDictStrEntry("uri");
			var version = readDictUintEntry("version", last: true); // closes "textDocument"

			readComma();
			readDictKey("contentChanges");

			readArrayStart();

			readDictStart();
			var text = readDictStrEntry("text", last: true);

			readArrayEnd();

			readDictEnd(); // Closes "params"

			return new LspMethod.Notification.TextDocumentDidChange(uri, version, text);
		}

		LspMethod.Notification.TextDocumentDidSave parseTextDocumentDidSave() {
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
			readDictStart();

			readDictKey("textDocument");
			readDictStart();
			var uri = readDictStrEntry("uri");
			var version = readDictUintEntry("version", last: true); // closes "textDocument"

			readDictEnd(); // closes "params"

			return new LspMethod.Notification.TextDocumentDidSave(uri, version);
		}

		LspMethod.Request.FindAllReferences parseFindAllReferences(uint id) {
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
			var pms = parseTextDocumentPositionParamsWithoutEnd();
			readComma();

			readDictKey("context");
			readDictStart();
			readDictKey("includeDeclaration");
			var includeDeclaration = readBoolean();
			readDictEnd(); // "context"

			readDictEnd(); // "params"

			return new LspMethod.Request.FindAllReferences(id, pms, includeDeclaration);
		}

		TextDocumentPositionParams parseTextDocumentPositionParams() {
			var res = parseTextDocumentPositionParamsWithoutEnd();
			readDictEnd();
			return res;
		}

		string parseTextDocumentIdentifier() {
			readDictKey("textDocument");
			readDictStart();
			var uri = readDictStrEntry("uri", last: true);
			readComma();
			return uri;
		}

		TextDocumentPositionParams parseTextDocumentPositionParamsWithoutEnd() {
			readDictStart();
			var uri = parseTextDocumentIdentifier();
			var position = readPositionEntry();
			return new TextDocumentPositionParams(uri, position);
		}

		void parseHeader() {
			var str = readDictStrEntry("jsonrpc");
			assert(str == "2.0");
		}

		Initialize parseInitialize(uint rqId) {
			readDictStart();

			var processId = readDictUintEntry("processId");
			var rootPath = readDictStrOrNullEntry("rootPath");
			var rootUri = readDictStrOrNullEntry("rootUri");

			readDictSkipDict("capabilities");

			var trace = readDictStrEntry("trace", last: true);

			return new Initialize(rqId, processId, rootPath, rootUri, trace);
		}

		Position readPositionEntry() {
			readDictKey("position");
			readDictStart();
			var line = readDictUintEntry("line");
			var character = readDictUintEntry("character", last: true);
			return new Position(line, character);
		}

		static T parseAny<T>(string source, Func<JsonParser, T> parse) {
			var p = new JsonParser(source);
			p.readDictStart();
			var res = parse(p);
			p.over();
			return res;
		}
	}
}
