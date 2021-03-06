using System.Collections.Generic;
using System.IO;
using System.Linq;

static class FileUtils {
	internal static IEnumerable<string> listDirectoriesInDirectory(Path path) {
		var pathStr = path.toPathString();
		var pathSlash = pathStr + '/';
		return Directory.EnumerateDirectories(pathStr).Select(d => d.withoutStart(pathSlash));
	}

	internal static IEnumerable<(Path, string)> readFilesInDirectoryRecursiveIfExists(Path directoryPath) =>
		directoryExists(directoryPath)
			? readFilesInDirectoryRecursive(directoryPath)
			: Enumerable.Empty<(Path, string)>();

	internal static IEnumerable<(Path, string)> readFilesInDirectoryRecursive(Path directoryPath) {
		var pathStr = directoryPath.toPathString();
		var pathSlash = pathStr + '/';
		foreach (var x in Directory.EnumerateFileSystemEntries(pathStr, "*", SearchOption.AllDirectories)) {
			var content = File.ReadAllText(x);
			var path = Path.fromString(x.withoutStart(pathSlash));
			yield return (path, content);
		}
	}

	internal static string readFile(Path path) =>
		File.ReadAllText(path.toPathString());

	internal static bool directoryExists(Path path) =>
		Directory.Exists(path.toPathString());

	internal static bool fileExists(Path path) =>
		File.Exists(path.toPathString());

	internal static void writeFile(Path path, string text) =>
		File.WriteAllText(path.toPathString(), text);

	internal static void writeFileAndEnsureDirectory(Path path, string text) {
		Directory.CreateDirectory(path.directory().toPathString());
		File.WriteAllText(path.toPathString(), text);
	}

	internal static bool isDirectory(Path path) {
		var attr = File.GetAttributes(path.toPathString());
		return (attr & FileAttributes.Directory) != 0;
	}
}
