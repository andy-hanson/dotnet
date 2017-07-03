using System.Collections.Generic;
using System.IO;
using System.Linq;

static class FileUtils {
	internal static IEnumerable<string> listDirectoriesInDirectory(Path path) {
		var pathStr = path.toPathString();
		var pathSlash = $"{pathStr}/";
		return Directory.EnumerateDirectories(pathStr).Select(d => d.withoutStart(pathSlash));
	}

	internal static string readFile(Path path) =>
		File.ReadAllText(path.toPathString());

	internal static bool fileExists(Path path) =>
		File.Exists(path.toPathString());

	internal static void writeFile(Path path, string text) =>
		File.WriteAllText(path.toPathString(), text);

	internal static void writeFileAndEnsureDirectory(Path path, string text) {
		Directory.CreateDirectory(path.directory().toPathString());
		File.WriteAllText(path.toPathString(), text);
	}
}
