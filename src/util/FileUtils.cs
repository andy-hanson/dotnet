using System.Collections.Generic;
using System.IO;
using System.Linq;

static class FileUtils {
	internal static IEnumerable<string> listDirectoriesInDirectory(Path path) {
		var pathStr = path.show();
		var pathSlash = pathStr + '/';
		return Directory.EnumerateDirectories(pathStr).Select(d => d.withoutStart(pathSlash));
	}

	internal static string readFile(Path path) =>
		File.ReadAllText(path.show());

	internal static bool fileExists(Path path) =>
		File.Exists(path.show());

	internal static void writeFile(Path path, string text) =>
		File.WriteAllText(path.show(), text);

	internal static void writeFileAndEnsureDirectory(Path path, string text) {
		Directory.CreateDirectory(path.directory().show());
		File.WriteAllText(path.show(), text);
	}

	internal static bool isDirectory(Path path) {
		var attr = File.GetAttributes(path.show());
		return (attr & FileAttributes.Directory) != 0;
	}
}
