using System.Reflection;

using static FileUtils;
using static Utils;

namespace Cli {
	static class Run {
		static readonly Sym symMain = Sym.of("main");

		public static void run(Path path) {
			var op = isDirectory(path) ? Compiler.compileDir(path) : Compiler.compileFile(path);
			if (!op.get(out var success)) {
				throw TODO();
			}

			var module = (Model.Module)success.root; //TODO: handle failure

			//TODO: if there are compile errors, stop.
			var emitted = ILEmitter.emit(module);

			var main = emitted.GetMethod("main");
			if (main == null) throw TODO("missing main");

			if (main.ReturnType != typeof(Builtins.Void))
				throw TODO("bad return type");

			var args = main.GetParameters().mapToArray<ParameterInfo, object>(p => {
				var t = p.ParameterType;
				if (t == typeof(Builtins.Console_App)) {
					var d = path.directory();
					//TODO: installationDirectory and currentWorkingDirectory will differ for a program in PATH.
					return new BuiltinImpls.ConsoleApp(installationDirectory: d, currentWorkingDirectory: d);
				}
				throw TODO("bad parameter type"); // unexpected type
			});

			main.Invoke(null, args);
			//var main = emitted.GetMethod("main");
		}
	}
}
