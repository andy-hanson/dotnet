using System;
using System.Collections.Generic;
using System.IO;

interface FileInput {
	/** Name of the module at "/index.nz" */
	Sym rootName { get; }
	// Null if the file was not found.
	Op<string> read(Path path);
}

sealed class NativeFileInput : FileInput {
	readonly Path rootDir;
	public NativeFileInput(Path rootDir) { this.rootDir = rootDir; }

	Sym FileInput.rootName => Sym.of(rootDir.last);

	Op<string> FileInput.read(Path path) {
		var fullPath = Path.resolveWithRoot(rootDir, path).ToString();
		if (File.Exists(fullPath)) {
			return Op.Some(File.ReadAllText(fullPath));
		}
		return Op<string>.None;
	}
}

struct DocumentInfo : ToData<DocumentInfo> {
	internal readonly string text;
	internal readonly Either<Ast.Module, CompileError> parseResult;
	internal readonly uint version;

	internal static DocumentInfo parse(string text, uint version) =>
		new DocumentInfo(text, version, Parser.parse(text));

	DocumentInfo(string text, uint version, Either<Ast.Module, CompileError> parseResult) {
		this.text = text;
		this.version = version;
		this.parseResult = parseResult;
	}

	public override bool Equals(object o) => throw new NotImplementedException();
	public override int GetHashCode() => throw new NotImplementedException();
	public bool deepEqual(DocumentInfo d) =>
		text == d.text &&
		parseResult.deepEq(d.parseResult) &&
		version == d.version;
	public Dat toDat() => Dat.of(this, nameof(text), Dat.str(text), nameof(parseResult), Dat.either(parseResult), nameof(version), Dat.num(version));
}

interface DocumentProvider {
	/** Name of the module at "/index.nz" */
	Sym rootName { get; }
	Op<DocumentInfo> getDocument(Path path);
}

static class DocumentProviders {
	internal static DocumentProvider CommandLine(Path rootDir) => new FileLoadingDocumentProvider(new NativeFileInput(rootDir));
}

/**
Loads documents from disk.
Does not cache anything.
*/
sealed class FileLoadingDocumentProvider : DocumentProvider {
	readonly FileInput fileInput;
	internal FileLoadingDocumentProvider(FileInput fileInput) { this.fileInput = fileInput; }

	Sym DocumentProvider.rootName => fileInput.rootName;

	Op<DocumentInfo> DocumentProvider.getDocument(Path path) =>
		fileInput.read(path).map(text => DocumentInfo.parse(text, version: 0));
}


/**
This is designed to allow an editor to send us info about unsafed open documents.
If a document is not open we will use the one on disk.
*/
sealed class DocumentsCache : DocumentProvider {
	readonly DocumentProvider fallback;
	readonly Dictionary<Path, DocumentInfo> pathToText = new Dictionary<Path, DocumentInfo>();

	internal DocumentsCache(DocumentProvider fallback) { this.fallback = fallback; }

	//TODO: maybe these should be the same method...
	internal DocumentInfo open(Path path, string text, uint version) =>
		pathToText.getOrUpdate(path, () => getInfo(path, text, version));

	internal DocumentInfo change(Path path, string text, uint version) {
		var info = getInfo(path, text, version);
		pathToText[path] = info;
		return info;
	}

	Op<DocumentInfo> DocumentProvider.getDocument(Path path) {
		if (pathToText.TryGetValue(path, out var cached))
			return Op.Some(cached);

		var x = this.fallback.getDocument(path);
		x.each(v => pathToText.Add(path, v));
		return x;
	}

	Sym DocumentProvider.rootName => fallback.rootName;

	DocumentInfo getInfo(Path path, string text, uint version) => DocumentInfo.parse(text, version);
}
