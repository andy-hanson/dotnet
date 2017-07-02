using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;

using Lsp;
using static Utils;

[AttributeUsage(AttributeTargets.Method)]
sealed class TestAttribute : Attribute {}

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

static class Program {
	static void Main() {
		//TestProc().Wait();

		var tc = new TestCompile(updateBaselines: true);
		tc.runTestNamed("Impl");
	}

	static async Task TestProc() {
		//await Task.FromException(new Exception("!"));
		Console.WriteLine("starting...");
		var p = Proc.startJs("tests/js-test-runner/index.js");
		p.send("AbstractClass");
		var x = await p.read();
		Console.WriteLine(x);
	}



	static (string stdout, string stderr) execJs(string scriptName) {
		using (var proc = new Process()) {
			var si = proc.StartInfo;
			si.FileName = "node";
			si.Arguments = scriptName;
			si.CreateNoWindow = true;

			si.RedirectStandardError = true;
			si.RedirectStandardInput = true;
			si.RedirectStandardOutput = true;

			var stdout = new StringBuilder();
			var stderr = new StringBuilder();

			proc.OutputDataReceived += (sender, args) =>
				stdout.Append(args.Data);
			proc.ErrorDataReceived += (sender, args) =>
				stderr.Append(args.Data);

			proc.Start();
			proc.BeginOutputReadLine(); // Needed?
			proc.BeginErrorReadLine();

			proc.WaitForExit();

			proc.Dispose();

			return (stdout: stdout.ToString(), stderr: stderr.ToString());
		}
	}




	static void doTestIl() {
		var t = testIl();
		object res;
		try {
			res = t.GetMethod("Test").Invoke(null, new object[] {});
		} catch (TargetInvocationException e) {
			ExceptionDispatchInfo.Capture(e.InnerException).Throw();
			throw unreachable();
		}
		Console.WriteLine(res);
	}

	static Type testIl() {
		var assemblyName = new AssemblyName("name");
		var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndCollect);
		var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);
		var tb = moduleBuilder.DefineType("Test");
		var mb = tb.DefineMethod(
			"Test",
			MethodAttributes.Public | MethodAttributes.Static,
			typeof(int),
			new Type[] {});
		var il = new ILWriter(mb);
		il.constInt(1);
		il.ret();

		return tb.CreateType();
	}
}

class MyException {

}

sealed class ConsoleLogger : Lsp.Server.Logger {
	public void received(string s) {
		Console.WriteLine($"Received: {s}");
	}

	public void sent(string s) {
		Console.WriteLine($"Sent: {s}");
	}
}

//kill
class DumbSmartness : LspImplementation {
	readonly Range dummyRange = new Range(new Position(1, 1), new Position(2, 2));

	Arr<Diagnostic> LspImplementation.diagnostics(string uri) =>
		Arr.of(new Diagnostic(dummyRange, Op.Some(Diagnostic.Severity.Error), Op.Some("code"), Op.Some("source"), "message"));

	void LspImplementation.textDocumentDidChange(string uri, uint version, string text) {
		Console.WriteLine("text document did change");
	}

	void LspImplementation.textDocumentDidSave(string uri, uint version) {
		Console.WriteLine($"Saved {uri}");
	}

	void LspImplementation.textDocumentDidOpen(string uri, string languageId, uint version, string text) {
		Console.WriteLine($"Opened ${uri}");
	}

	void LspImplementation.goToDefinition(TextDocumentPositionParams pms, out string uri, out Range range) {
		uri = pms.textDocumentUri;
		range = new Range(new Position(1, 1), new Position(1, 2));
	}

	Arr<CompletionItem> LspImplementation.getCompletion(TextDocumentPositionParams pms) =>
		Arr.of(new CompletionItem("myLabel"));

	string LspImplementation.getHover(TextDocumentPositionParams pms) => "myHover";

	Arr<DocumentHighlight> LspImplementation.getDocumentHighlights(TextDocumentPositionParams pms) =>
		Arr.of(new DocumentHighlight(dummyRange, DocumentHighlight.Kind.Read));

	Arr<Location> LspImplementation.findAllReferences(TextDocumentPositionParams pms, bool includeDeclaration) =>
		Arr.of(new Location(pms.textDocumentUri, dummyRange));

	Response.SignatureHelpResponse LspImplementation.signatureHelp(TextDocumentPositionParams pms) {
		var parameter = new ParameterInformation("myParam", Op.Some("paramDocs"));
		var signature = new SignatureInformation("mySignature", Op.Some("documentation"), Op.Some(Arr.of(parameter)));
		return new Response.SignatureHelpResponse(Arr.of(signature), activeSignature: OpUint.Some(0), activeParameter: OpUint.Some(0));
	}
}
