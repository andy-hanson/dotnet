using System;
using System.Reflection;
using System.Reflection.Emit;

using Lsp;

#pragma warning disable CC0068 // Allow unused methods in this file
#pragma warning disable CC0057 // Allow unused arguments in this file

static class Program {
	static void Main(string[] args) {
		//args = new[] { "run", "tests/cases/ConsoleApp/index.nz" };
		//return Cli.Cli.go(args);

		//doTestIl();

		using (var tc = new Test.TestCompile(updateBaselines: true)) {
			tc.runAllTests();
			//tc.runAllCompilerTests();
			//tc.runTestNamed("Generic");
		}
	}

	static void doTestIl() {
		var t = testIl();
		var res = t.invokeStatic("main", 10u); //t.invokeStaticGeneric<int>("stat", 10);
		Console.WriteLine(res);

		//var instance = Activator.CreateInstance(t);
		//Console.WriteLine(t.invokeInstance(instance, "foo"));
	}

	static Type testIl() {
		var assemblyName = new AssemblyName("TestIL");
		var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndCollect);
		var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);

		var tb = moduleBuilder.DefineType("TestIL", TypeAttributes.Sealed); //, typeof(object), new[] { iface });


		var mbStat = tb.DefineMethod("stat", MethodAttributes.Public | MethodAttributes.Static);
		var typeParameters = mbStat.DefineGenericParameters(new[] { "T" });
		mbStat.SetParameters(typeParameters);
		mbStat.SetReturnType(typeParameters[0]);

		var il = new ILWriter(mbStat);
		writeStat(ref il);

		var mbMain = tb.DefineMethod(
			"main",
			MethodAttributes.Public | MethodAttributes.Static,
			typeof(uint),
			new Type[] { typeof(uint) });
		var ilmain = new ILWriter(mbMain);
		writeMain(ref ilmain, mbStat);

		return tb.CreateType();
	}

	static void writeStat(ref ILWriter il) {
		il.getParameter(0);
		il.ret();
	}

	static void writeMain(ref ILWriter il, MethodInfo stat) {
		il.getParameter(0);
		il.callNonVirtual(stat.MakeGenericMethod(typeof(uint)));
		// Call "stat"
		il.ret();
	}
}

sealed class ConsoleLogger : Lsp.Server.Logger {
	public void received(string s) {
		Console.WriteLine("Received: " + s);
	}

	public void sent(string s) {
		Console.WriteLine("Sent: " + s);
	}
}

//kill
class DumbSmartness : LspImplementation {
	readonly Range dummyRange = new Range(new Position(1, 1), new Position(2, 2));

	Arr<Lsp.Diagnostic> LspImplementation.diagnostics(string uri) =>
		Arr.of(new Lsp.Diagnostic(dummyRange, Op.Some(Lsp.Diagnostic.Severity.Error), Op.Some("code"), Op.Some("source"), "message"));

	void LspImplementation.textDocumentDidChange(string uri, uint version, string text) {
		Console.WriteLine("text document did change");
	}

	void LspImplementation.textDocumentDidSave(string uri, uint version) {
		Console.WriteLine("Saved " + uri);
	}

	void LspImplementation.textDocumentDidOpen(string uri, string languageId, uint version, string text) {
		Console.WriteLine("Opened " + uri);
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
