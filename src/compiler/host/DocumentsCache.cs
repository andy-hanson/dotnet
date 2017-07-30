using System.Collections.Generic;

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

	bool DocumentProvider.getDocument(Path path, out DocumentInfo di) {
		if (pathToText.TryGetValue(path, out di))
			return true;

		var fallbackFoundIt = this.fallback.getDocument(path, out di);
		if (fallbackFoundIt)
			pathToText.Add(path, di);
		return fallbackFoundIt;
	}

	Sym DocumentProvider.rootName => fallback.rootName;

	static DocumentInfo getInfo(Path path, string text, uint version) => DocumentInfo.parse(text, version);
}
