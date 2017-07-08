using System;
using System.Reflection;
using System.Reflection.Emit;

using Lsp;

[AttributeUsage(AttributeTargets.Method)]
sealed class TestAttribute : Attribute {}

#pragma warning disable CC0068 // Allow unused methods in this file

static class Program {
	static void Main() {
		//doTestIl();

		var tc = new Test.TestCompile(updateBaselines: true);
		tc.runAllCompilerTests();
		//tc.runTestNamed("Impl");
	}

	static void doTestIl() {
		var (iface, t) = testIl();
		var inst = Activator.CreateInstance(t);
		Console.WriteLine(iface.invokeStatic("stat", inst));

		//var instance = Activator.CreateInstance(t);
		//Console.WriteLine(t.invokeInstance(instance, "foo"));
	}

	static (Type, Type) testIl() {
		var assemblyName = new AssemblyName("TestIL");
		var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndCollect);
		var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);

		var ifaceBuilder = moduleBuilder.DefineType("TestIface", TypeAttributes.Interface | TypeAttributes.Abstract);
		var ifaceMethod = ifaceBuilder.DefineMethod("foo", MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual, typeof(int), new Type[] { });

		var st = ifaceBuilder.DefineMethod("stat", MethodAttributes.Static | MethodAttributes.Public, typeof(int), new Type[] { ifaceBuilder });
		var ilst = new ILWriter(st);
		ilst.getParameter(0);
		ilst.callVirtual(ifaceMethod);
		ilst.ret();

		var iface = ifaceBuilder.CreateType();

		var tb = moduleBuilder.DefineType("TestIL", TypeAttributes.Sealed, typeof(object), new[] { iface });

		var mb = tb.DefineMethod(
			"foo",
			MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final,
			typeof(int),
			new Type[] {});
		var il = new ILWriter(mb);
		writeIl(ref il);
		il.ret();

		return (iface, tb.CreateType());
	}

	static void writeIl(ref ILWriter w) {
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
