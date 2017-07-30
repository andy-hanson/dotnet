using System.IO;

interface FileInput {
	/** Name of the module at "/index.nz" */
	Sym rootName { get; }
	// Returns false if the file was not found.
	bool read(Path path, out string content);
}

sealed class NativeFileInput : FileInput {
	readonly Path rootDir;
	public NativeFileInput(Path rootDir) { this.rootDir = rootDir; }

	Sym FileInput.rootName => Sym.of(rootDir.last);

	bool FileInput.read(Path path, out string content) {
		var fullPath = Path.resolveWithRoot(rootDir, path).toPathString();
		if (File.Exists(fullPath)) {
			content = File.ReadAllText(fullPath);
			return true;
		} else {
			content = string.Empty;
			return false;
		}
	}
}
