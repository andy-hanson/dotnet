using System.Reflection;

using static FileUtils;
using static Utils;

namespace Cli {
	static class Run {
		static readonly Sym symMain = Sym.of("main");

		public static void run(Path path) {
			var (_, module) = isDirectory(path) ? Compiler.compileDir(path) : Compiler.compileFile(path);

			//TODO: if there are compile errors, stop.
			var emitted = ILEmitter.emit(module);

			var main = emitted.GetMethod("main");
			if (main == null) throw TODO("missing main");

			if (main.ReturnType != typeof(Builtins.Void))
				throw TODO("bad return type");

			var args = main.GetParameters().mapToArray<ParameterInfo, object>(p => {
				var t = p.ParameterType;
				if (t == typeof(Builtins.Console))
					return new BuiltinImpls.Console();
				throw TODO("bad parameter type"); // unexpected type
			});

			main.Invoke(null, args);
			//var main = emitted.GetMethod("main");
		}
	}
}
