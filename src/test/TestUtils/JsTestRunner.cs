using System;
using System.Diagnostics;

using static Utils;

struct JsTestRunner : IDisposable {
	readonly SynchronousProcess process;

	JsTestRunner(SynchronousProcess process) { this.process = process; }

	internal void runTest(Path testPath) {
		var response = process.sendAndReceive($"normal {testPath.toPathString()}");
		if (response != "OK") {
			if (response.StartsWith('"') && response.EndsWith('"'))
				//Response is a string with the error.
				response = response.slice(0, unsigned(response.Length - 1)).Replace("\\\"", "\"").Replace("\\n", "\n");
			throw new TestFailureException(response);
		}
	}

	internal void runTestSpecial(Path testPath) {
		var response = process.sendAndReceive($"special {testPath.toPathString()}");
		assert(response == "OK");
	}

	void IDisposable.Dispose() {
		IDisposable i = process;
		i.Dispose();
	}

	internal static JsTestRunner create() =>
		new JsTestRunner(SynchronousProcess.create());
}

struct SynchronousProcess : IDisposable {
	readonly Process process;

	SynchronousProcess(Process process) { this.process = process; }

	void start() {
		var si = process.StartInfo;
		si.RedirectStandardInput = true;
		si.RedirectStandardOutput = true;
		// Don't redirect stderr, the script shouldn't error

		process.Start();
	}

	internal string sendAndReceive(string testName) {
		process.StandardInput.WriteLine(testName);
		var response = process.StandardOutput.ReadLine();
		assert(response != null);
		return response;
	}

	internal static SynchronousProcess create() {
		var proc = new Process();
		var si = proc.StartInfo;
		si.FileName = "node";
		si.Arguments = "tests/js-test-runner/runFromCSharp.js";
		si.CreateNoWindow = true;

		var syncProc = new SynchronousProcess(proc);
		syncProc.start();
		return syncProc;
	}

	void IDisposable.Dispose() {
		process.Dispose();
	}
}

/*
sealed class Proc : IDisposable {
	readonly Process process;
	// Only one of these will be non-empty at once.
	readonly Queue<string> outputs = new Queue<string>();
	readonly Queue<TaskCompletionSource<string>> awaitingReads = new Queue<TaskCompletionSource<string>>();

	Proc(Process process) { this.process = process; }

	void start() {
		var si = process.StartInfo;
		si.RedirectStandardError = true;
		si.RedirectStandardInput = true;
		si.RedirectStandardOutput = true;
		process.EnableRaisingEvents = true;

		process.OutputDataReceived += (sender, e) => {
			if (e.Data == null)
				return;

			if (awaitingReads.TryDequeue(out var r))
				r.SetResult(e.Data);
			else
				outputs.Enqueue(e.Data);
		};

		process.ErrorDataReceived += (sender, e) => {
			if (e.Data != null)
				throw new Exception(e.Data);
		};

		process.Exited += (sender, args) => {
			foreach (var ar in awaitingReads) {
				ar.SetException(new Exception("Process exited."));
			}
			awaitingReads.Clear();
		};

		process.Start();
	}

	internal static Proc startJs(string scriptName) {
		var process = new Process();
		var si = process.StartInfo;
		si.FileName = "node";
		si.Arguments = scriptName;
		si.CreateNoWindow = true;

		var proc = new Proc(process);
		proc.start();
		return proc;
	}

	internal void send(string s) {
		process.StandardInput.WriteLine(s);
	}

	internal Task<string> read() {
		process.BeginOutputReadLine();
		process.BeginErrorReadLine();

		if (process.HasExited) {
			return Task.FromException<string>(new Exception("Process exited."));
		}
		else if (outputs.TryDequeue(out var s)) {
			return Task.FromResult(s);
		}
		else {
			var tcs = new TaskCompletionSource<string>();
			awaitingReads.Enqueue(tcs);
			return tcs.Task;
		}
	}

	void IDisposable.Dispose() {
		process.Dispose();
	}
}
*/
