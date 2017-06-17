static class ModuleResolver {
	internal static bool getDocumentFromLogicalPath(DocumentProvider dp, Op<PathLoc> from, Path logicalPath, out Path fullPath, out bool isMain, out DocumentInfo di) {
		isMain = false;
		fullPath = regularPath(logicalPath);
		if (dp.getDocument(fullPath).get(out di))
			return true;

		isMain = true;
		fullPath = mainPath(logicalPath);
		if (dp.getDocument(fullPath).get(out di))
			return true;

		return false;
	}

	/*internal static Sym nameFromPath(Path path) {
		var last = path.last;
		assert(last.str.EndsWith(extension));
		return last == mainNz ? path.nameOfContainingDirectory : last;
	}*/

	internal static Arr<Path> attemptedPaths(Path logicalPath) =>
		Arr.of(regularPath(logicalPath), mainPath(logicalPath));

	internal static Path fullPath(Path logicalPath, bool isMain) =>
		isMain ? mainPath(logicalPath) : regularPath(logicalPath);

	static Path regularPath(Path logicalPath) =>
		logicalPath.addExtension(extension);

	static Path mainPath(Path logicalPath) =>
		logicalPath.add(mainNz);

	const string extension = ".nz";
	static Sym mainNz = Sym.of($"main{extension}");
}