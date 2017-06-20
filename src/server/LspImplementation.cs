namespace Lsp {
	interface LspImplementation {
		//TODO: This isn't a request!!! So handle it a different way.
		//This should presumably be done after textDocumentDidOpen and textDocumentDidChange.
		//https://github.com/Microsoft/language-server-protocol/blob/master/protocol.md#publishdiagnostics-notification
		Arr<Diagnostic> diagnostics(string uri);

		/**
		https://github.com/Microsoft/language-server-protocol/blob/master/protocol.md#textDocument_didOpen
		*/
		void textDocumentDidOpen(string uri, string languageId, uint version, string text);
		/**
		https://github.com/Microsoft/language-server-protocol/blob/master/protocol.md#textDocument_didChange
		*/
		void textDocumentDidChange(string uri, uint version, string text);
		/**
		https://github.com/Microsoft/language-server-protocol/blob/master/protocol.md#textDocument_didSave
		*/
		void textDocumentDidSave(string uri, uint version);

		void goToDefinition(TextDocumentPositionParams pms, out string uri, out Range range);
		Arr<CompletionItem> getCompletion(TextDocumentPositionParams pms);
		string getHover(TextDocumentPositionParams pms);
		Arr<DocumentHighlight> getDocumentHighlights(TextDocumentPositionParams pms);
		Arr<Location> findAllReferences(TextDocumentPositionParams pms, bool includeDeclaration);
		Response.SignatureHelpResponse signatureHelp(TextDocumentPositionParams pms);
	}
}
