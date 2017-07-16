#pragma warning disable CC0022 // We need to wrap a stream in a StreamWriter without closing the stream.

static class BuiltinImpls {
	internal sealed class ConsoleApp : Builtins.Console_App {
		readonly global::Path installationDirectory;
		readonly global::Path currentWorkingDirectory;
		internal ConsoleApp(global::Path installationDirectory, global::Path currentWorkingDirectory) {
			this.installationDirectory = installationDirectory;
			this.currentWorkingDirectory = currentWorkingDirectory;
		}

		Builtins.Read_Stream Builtins.Console_App.stdin() =>
			new ReadStreamWrapper(System.Console.OpenStandardInput());

		Builtins.Write_Stream Builtins.Console_App.stdout() =>
			new WriteStreamWrapper(System.Console.OpenStandardOutput());

		Builtins.Write_Stream Builtins.Console_App.stderr() =>
			new WriteStreamWrapper(System.Console.OpenStandardError());

		Builtins.File_System Builtins.Console_App.installation_directory() =>
			new FileSystem(installationDirectory);

		Builtins.File_System Builtins.Console_App.current_working_directory() =>
			new FileSystem(currentWorkingDirectory);
	}

	sealed class ReadStreamWrapper : Builtins.Read_Stream {
		readonly System.IO.Stream inner;

		internal ReadStreamWrapper(System.IO.Stream inner) { this.inner = inner; }

		Builtins.String Builtins.Read_Stream.read_all() {
			var r = new System.IO.StreamReader(inner, System.Text.Encoding.UTF8);
			var s = r.ReadToEnd();
			inner.Dispose();
			return Builtins.String.of(s);
		}

		Builtins.Void Builtins.Read_Stream.close() {
			inner.Dispose();
			return Builtins.Void.instance;
		}

		Builtins.Void Builtins.Read_Stream.write_all_to(Builtins.Write_Stream w) {
			var all = this.upcast<Builtins.Read_Stream>().read_all();
			return w.write_all(all);
		}
	}

	sealed class WriteStreamWrapper : Builtins.Write_Stream {
		readonly System.IO.Stream inner;

		internal WriteStreamWrapper(System.IO.Stream inner) { this.inner = inner; }

		Builtins.Void Builtins.Write_Stream.write_all(Builtins.String s) {
			this.upcast<Builtins.Write_Stream>().write(s);
			this.upcast<Builtins.Write_Stream>().close();
			return Builtins.Void.instance;
		}

		Builtins.Void Builtins.Write_Stream.write(Builtins.String s) {
			var sw = new System.IO.StreamWriter(inner);
			sw.Write(s.value);
			return Builtins.Void.instance;
		}

		Builtins.Void Builtins.Write_Stream.write_line(Builtins.String s) {
			var sw = new System.IO.StreamWriter(inner);
			sw.WriteLine(s.value);
			sw.Flush();
			return Builtins.Void.instance;
		}

		Builtins.Void Builtins.Write_Stream.close() {
			inner.Dispose();
			return Builtins.Void.instance;
		}
	}

	sealed class FileSystem : Builtins.File_System {
		readonly global::Path rootPath;

		internal FileSystem(global::Path rootPath) { this.rootPath = rootPath; }

		Builtins.String Builtins.File_System.read(Builtins.Path p) =>
			Builtins.String.of(System.IO.File.ReadAllText(pathStr(p), System.Text.Encoding.UTF8));

		Builtins.Void Builtins.File_System.write(Builtins.Path p, Builtins.String content) {
			System.IO.File.WriteAllText(pathStr(p), content.value, System.Text.Encoding.UTF8);
			return Builtins.Void.instance;
		}

		Builtins.Read_Stream Builtins.File_System.open_read(Builtins.Path p) =>
			new ReadStreamWrapper(System.IO.File.OpenRead(pathStr(p)));

		Builtins.Write_Stream Builtins.File_System.open_write(Builtins.Path p) =>
			new WriteStreamWrapper(System.IO.File.OpenWrite(pathStr(p)));

		static string pathStr(Builtins.Path p) =>
			Builtins.Path.to_string(p).value;
	}
}
