using System;
using System.Diagnostics;

using static Utils;

namespace Test {
	struct JsTestRunner : IDisposable {
		readonly SynchronousProcess process;

		JsTestRunner(SynchronousProcess process) { this.process = process; }

		internal void runTest(Path testPath) {
			var response = process.sendAndReceive(StringMaker.create().add("normal ").add(testPath).finish());
			if (response != "OK") {
				if (response.StartsWith('"') && response.EndsWith('"'))
					//Response is a string with the error.
					response = response.slice(0, unsigned(response.Length - 1)).Replace("\\\"", "\"").Replace("\\n", "\n");
				throw new TestFailureException(response);
			}
		}

		internal void runTestSpecial(Path testPath) {
			var response = process.sendAndReceive(StringMaker.create().add("special ").add(testPath).finish());
			assert(response == "OK");
		}

		void IDisposable.Dispose() {
			IDisposable i = process;
			i.Dispose();
		}

		internal static JsTestRunner create() =>
			new JsTestRunner(SynchronousProcess.create(Path.fromParts("tests", "js-test-runner", "runFromCSharp.js")));
	}

	struct SynchronousProcess : IDisposable {
		readonly Process process;

		SynchronousProcess(Process process) { this.process = process; }

		void start() {
			var si = process.StartInfo;
			si.RedirectStandardInput = true;
			si.RedirectStandardOutput = true;

			process.Start();
		}

		internal string sendAndReceive(string testName) {
			process.StandardInput.WriteLine(testName);
			var response = process.StandardOutput.ReadLine();
			assert(response != null);
			return response;
		}

		internal static string run(Path path, string input) {
			using (var p = create(path)) {
				p.process.StandardInput.WriteLine(input);
				var response = p.process.StandardOutput.ReadToEnd();
				assert(response != null);
				return response;
			}
		}

		internal static SynchronousProcess create(Path path) {
			var proc = new Process();
			var si = proc.StartInfo;
			si.FileName = "node";
			si.Arguments = "--harmony_tailcalls " + path.toPathString();
			si.CreateNoWindow = true;

			var syncProc = new SynchronousProcess(proc);
			syncProc.start();
			return syncProc;
		}

		void IDisposable.Dispose() {
			process.Dispose();
		}
	}
}
