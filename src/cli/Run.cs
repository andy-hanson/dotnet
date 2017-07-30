using System;

using Diag.ModuleDiag;
using Model;

using static FileUtils;
using static Utils;

namespace Cli {
	static class Run {
		static readonly Sym symMain = Sym.of("main");

		public static ExitCode run(Path cliInvokedPath) {
			var op = isDirectory(cliInvokedPath) ? Compiler.compileDir(cliInvokedPath) : Compiler.compileFile(cliInvokedPath);
			if (!op.get(out var success)) {
				var err = new CantFindLocalModule(Path.empty, cliInvokedPath.asRel);
				Console.Error.WriteLine(StringMaker.stringify(err));
				return ExitCode.Fail;
			}

			var diags = CollectDiagnostics.collect(success.root);
			if (diags.any) {
				ShowDiagnostics.show(diags);
				return ExitCode.Fail;
			}

			return invoke(cliInvokedPath, (Model.Module)success.root); // If there were no diagnostics, root can not be a FailModule.
		}

		static ExitCode invoke(Path cliInvokedPath, Model.Module root) {
			var emitted = ILEmitter.emit(root);

			var main = emitted.GetMethod("main");
			if (main == null) {
				Console.Error.WriteLine(StringMaker.create().add("Module '").add(root.fullPath()).add("' has no 'main' function.").finish());
				return ExitCode.Fail;
			}

			if (main.ReturnType != typeof(Builtins.Void)) {
				Console.Error.WriteLine(StringMaker.create().add("Module '").add(root.fullPath()).add("' must return 'Void' from 'main'."));
				return ExitCode.Fail;
			}

			var args = main.paramz().mapOrMapFailure<object, Type>(p => {
				var t = p.ParameterType;
				if (t == typeof(Builtins.Console_App)) {
					var d = cliInvokedPath.directory();
					//TODO: installationDirectory and currentWorkingDirectory will differ for a program in PATH.
					return Either<object, Type>.Left(new BuiltinImpls.ConsoleApp(installationDirectory: d, currentWorkingDirectory: d));
				}
				return Either<object, Type>.Right(t);
			});

			if (args.isRight) {
				var msg = StringMaker.create()
					.add("Environment does not provide an implementation for the following types: ")
					.join(args.right, t => t.Name)
					.finish();
				Console.Error.WriteLine(msg);
				return ExitCode.Fail;
			}

			try {
				main.Invoke(null, args.left.toArray);
				return ExitCode.Success;
			} catch (Exception e) {
				unused(e);
				throw TODO();
			}
		}
	}
}

static class ShowDiagnostics {
	internal static void show(Arr<ModuleOrFail> modulesWithDiagnostics) {
		foreach (var module in modulesWithDiagnostics) {
			Console.ForegroundColor = ConsoleColor.Green;
			Console.Error.WriteLine(StringMaker.create().add(module.fullPath()).add(':'));
			Console.ForegroundColor = ConsoleColor.Red;
			var lc = new LineColumnGetter(module.document.text);
			foreach (var diag in module.diagnostics) {
				var lcLoc = lc.lineAndColumnAtLoc(diag.loc);
				Console.Error.WriteLine(StringMaker.create().add("  ").add(lcLoc).add(' ').add(diag.data).finish());
			}
		}
	}
}

sealed class CollectDiagnostics {
	readonly Arr.Builder<ModuleOrFail> modulesWithDiagnostics = Arr.builder<ModuleOrFail>();
	CollectDiagnostics() {}

	internal static Arr<ModuleOrFail> collect(ModuleOrFail root) {
		var s = new CollectDiagnostics();
		s.doEither(root);
		return s.modulesWithDiagnostics.finish();
	}

	void doEither(ModuleOrFail root) {
		switch (root) {
			case FailModule f:
				doFailModule(f);
				break;
			case Model.Module m:
				doModule(m);
				break;
			default:
				throw unreachable();
		}
	}

	void doFailModule(FailModule f) {
		modulesWithDiagnostics.add(f);
		foreach (var i in f.imports) {
			if (i.isLeft)
				doImported(i.left);
			else
				doFailModule(i.right);
		}
	}

	void doModule(Module m) {
		modulesWithDiagnostics.add(m);
		foreach (var i in m.imports)
			doImported(i);
	}

	void doImported(Imported i) {
		switch (i) {
			case BuiltinClass b:
				break;
			case Module m:
				doModule(m);
				break;
			default:
				throw unreachable();
		}
	}
}
