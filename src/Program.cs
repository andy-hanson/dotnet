using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.ExceptionServices;

using Lsp;
using static Utils;

[AttributeUsage(AttributeTargets.Method)]
sealed class TestAttribute : Attribute {}

static class Program {
	static void Main() {
		//JsTestRunner.create().runTest(Path.fromParts("Impl"));

		var tc = new TestCompile(updateBaselines: true);
		tc.runTestNamed("Impl");

		//doTestIl();
	}

	static void doTestIl() {
		var t = testIl();
		object res;
		try {
			res = t.GetMethod("Test").Invoke(null, null);
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
		writeIl(il);
		il.ret();

		return tb.CreateType();
	}

	static void writeIl(ILWriter w) {
		w.constInt(1);
	}
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
