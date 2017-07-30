using System;

using static Utils;

namespace Cli {
	static class Cli {
		internal static int go(string[] args) =>
			(int)work(args);

		static ExitCode work(string[] args) {
			if (args.Length == 0)
				return fail(usage);

			var command = args[0];
			var rest = args.slice(1);
			switch (command) {
				case nameof(run):
					return run(rest);
				case nameof(compile):
					return compile(rest);
				default:
					return fail(usage);
			}
		}

		const string usage = "Usage: noze run|compile path";

		static ExitCode fail(string str) {
			Console.Error.WriteLine(str);
			return ExitCode.Fail;
		}

		static ExitCode run(Arr<string> args) {
			if (args.length != 1)
				return fail(usage);

			var toRun = args[0]; // name of the file to be run.

			var path = Path.fromString(toRun);
			return Run.run(path);
		}

		static ExitCode compile(Arr<string> args) {
			unused(args);
			throw TODO();
		}
	}

	enum ExitCode { Success = 0, Fail = 1 }
}
