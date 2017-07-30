static class ModuleResolver {
	internal static bool getDocumentFromLogicalPath(DocumentProvider dp, Path logicalPath, out Path fullPath, out bool isIndex, out DocumentInfo di) {
		isIndex = false;
		fullPath = regularPath(logicalPath);
		if (dp.getDocument(fullPath).get(out di))
			return true;

		isIndex = true;
		fullPath = indexPath(logicalPath);
		if (dp.getDocument(fullPath).get(out di))
			return true;

		return false;
	}

	internal static Arr<Path> attemptedPaths(Path importerPath, RelPath importedPath) {
		var logicalPath = importerPath.resolve(importedPath);
		return Arr.of(regularPath(logicalPath), indexPath(logicalPath));
	}

	internal static Path fullPath(Path logicalPath, bool isIndex) =>
		isIndex ? indexPath(logicalPath) : regularPath(logicalPath);

	static Path regularPath(Path logicalPath) =>
		logicalPath.addExtension(extension);

	static Path indexPath(Path logicalPath) =>
		logicalPath.add(indexNz);

	internal const string extension = ".nz";
	const string indexNz = "index.nz";
}
