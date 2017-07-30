interface DocumentProvider {
	/** Name of the module at "/index.nz" */
	Sym rootName { get; }
	bool getDocument(Path path, out DocumentInfo di);
}

static class DocumentProviders {
	/**
	Loads documents from disk.
	Does not cache anything.
	*/
	internal static DocumentProvider fileSystemDocumentProvider(Path rootDir) => new FileLoadingDocumentProvider(new NativeFileInput(rootDir));

	sealed class FileLoadingDocumentProvider : DocumentProvider {
		readonly FileInput fileInput;
		internal FileLoadingDocumentProvider(FileInput fileInput) { this.fileInput = fileInput; }

		Sym DocumentProvider.rootName => fileInput.rootName;

		bool DocumentProvider.getDocument(Path path, out DocumentInfo di) {
			if (!fileInput.read(path, out var content)) {
				di = default(DocumentInfo);
				return false;
			}

			di = DocumentInfo.parse(content, version: 0);
			return true;
		}
	}
}
