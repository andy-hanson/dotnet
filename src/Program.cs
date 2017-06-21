using System;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
//dotnet add package System.Runtime
//dotnet add package System.Runtime.Loader
//dotnet add package System.Reflection.Emit

using Lsp;

[AttributeUsage(AttributeTargets.Method)]
class TestAttribute : Attribute {}

class Program {
	static void Main(string[] _) {
		var engine = new Jint.Engine();
		try {
			var x = JsRunner.evalScript(engine, Path.from("tests", "dummy.js"));
			Console.WriteLine(x);
		} catch (Jint.Runtime.JavaScriptException e) {
			var line = e.LineNumber;
			var col = e.Column;
			Console.WriteLine($"Error at {line}:{col}: {e.Message}\n{e.StackTrace}");
		}

		//testNil();
		//testJint2();

		//TestCompile.runCompilerTest(Path.from("1"));
		//TestCompile.runAllCompilerTests();

		//var (stdout, stderr) = execJs("test.js");
		//Console.WriteLine($"stdout: {stdout}");
		//Console.WriteLine($"stderr: {stderr}");
	}

	static void testNil() {
		var context = new NiL.JS.Core.Context();
		context.Eval(@"() => 1");
	}

	static void testJurassic() {
		var engine = new Jurassic.ScriptEngine();
		// Crash! Method not found: 'System.Reflection.Emit.AssemblyBuilder System.AppDomain.DefineDynamicAssembly...
		Console.WriteLine(engine.Evaluate("5 * 10 + 2"));
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

	static void testService() {
		uint port = 8124;
		var logger = Op<Lsp.Server.Logger>.None;
		using (var s = new Lsp.Server.LspServer(port, logger, new DumbSmartness())) {
			s.loop();
		}

		//Lsp.Server.TryTryAgain.go();
		//var svc = new Lsp.Server.MyService();
	}

	static void testCompiler() {
		//var text = File.ReadAllText("sample/A.nz");
		//Parser.parse(Sym.of("A"), text);
		var rootDir = Path.empty;
		var host = DocumentProviders.CommandLine(rootDir);
		var (program, m) = Compiler.compile(Path.from("sample", "A"), host, Op<CompiledProgram>.None);

		//var cmp = //new CompilerDaemon(DocumentProviders.CommandLine(rootDir));
		//var m = cmp.compile(Path.from("sample", "A"));

		var e = new ILEmitter();
		Type t = e.emitModule(m);

		var me = t.GetMethod("f");
		Console.WriteLine(me);

		me.Invoke(null, new object[] {});

		//Func<bool, object> mm = (bool b) => me.Invoke(null, new object[] { Builtins.Bool.of(b) });
		//Console.WriteLine(mm(true));
		//Console.WriteLine(mm(false));


		//Emit.Emit.writeBytecode(mb, x.klass, x.lineColumnGetter);

	}

	static void goof() {
		var aName = new AssemblyName("Example");
		var ab = AssemblyBuilder.DefineDynamicAssembly(aName, AssemblyBuilderAccess.RunAndCollect);
		var mb = ab.DefineDynamicModule(aName.Name);

		Console.WriteLine("main");
	}


	static void AddCtr(TypeBuilder tb, FieldBuilder fbNumber) {
		Type[] parameterTypes = { typeof(int) };
		var ctor1 = tb.DefineConstructor(
			MethodAttributes.Public,
			CallingConventions.Standard,
			parameterTypes);
		var ctor1IL = ctor1.GetILGenerator();
		ctor1IL.Emit(OpCodes.Ldarg_0);
		ctor1IL.Emit(OpCodes.Call,
			typeof(object).GetConstructor(Type.EmptyTypes));
		ctor1IL.Emit(OpCodes.Ldarg_0);
		ctor1IL.Emit(OpCodes.Ldarg_1);
		ctor1IL.Emit(OpCodes.Stfld, fbNumber);
		ctor1IL.Emit(OpCodes.Ret);

		var ctor0 = tb.DefineConstructor(
			MethodAttributes.Public,
			CallingConventions.Standard,
			Type.EmptyTypes);

		ILGenerator ctor0IL = ctor0.GetILGenerator();
		// For a constructor, argument zero is a reference to the new
		// instance. Push it on the stack before pushing the default
		// value on the stack, then call constructor ctor1.
		ctor0IL.Emit(OpCodes.Ldarg_0);
		ctor0IL.Emit(OpCodes.Ldc_I4_S, 42);
		ctor0IL.Emit(OpCodes.Call, ctor1);
		ctor0IL.Emit(OpCodes.Ret);
	}

	static void testAssemblyStuff() {
		var aName = new AssemblyName("Example");
		var ab = AssemblyBuilder.DefineDynamicAssembly(aName, AssemblyBuilderAccess.RunAndCollect);
		var mb = ab.DefineDynamicModule(aName.Name);
		var tb = mb.DefineType("MyType", TypeAttributes.Public);
		var fbNumber = tb.DefineField("num", typeof(int), FieldAttributes.Private);

		AddCtr(tb, fbNumber);

		var pbNumber = tb.DefineProperty(
			"Number",
			PropertyAttributes.HasDefault,
			typeof(int),
			null);

		var getSetAttr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
		var mbNumberGetAccessor = tb.DefineMethod(
			"get_Number",
			getSetAttr,
			typeof(int),
			Type.EmptyTypes);

		ILGenerator numberGetIL = mbNumberGetAccessor.GetILGenerator();
		// For an instance property, argument zero is the instance. Load the
		// instance, then load the private field and return, leaving the
		// field value on the stack.
		numberGetIL.Emit(OpCodes.Ldarg_0);
		numberGetIL.Emit(OpCodes.Ldfld, fbNumber);
		numberGetIL.Emit(OpCodes.Ret);

		// Define the "set" accessor method for Number, which has no return
		// type and takes one argument of type int (Int32).
		MethodBuilder mbNumberSetAccessor = tb.DefineMethod(
			"set_Number",
			getSetAttr,
			null,
			new Type[] { typeof(int) });

		ILGenerator numberSetIL = mbNumberSetAccessor.GetILGenerator();
		// Load the instance and then the numeric argument, then store the
		// argument in the field.
		numberSetIL.Emit(OpCodes.Ldarg_0);
		numberSetIL.Emit(OpCodes.Ldarg_1);
		numberSetIL.Emit(OpCodes.Stfld, fbNumber);
		numberSetIL.Emit(OpCodes.Ret);

		// Last, map the "get" and "set" accessor methods to the
		// PropertyBuilder. The property is now complete.
		pbNumber.SetGetMethod(mbNumberGetAccessor);
		pbNumber.SetSetMethod(mbNumberSetAccessor);

		// Define a method that accepts an integer argument and returns
		// the product of that integer and the private field m_number. This
		// time, the array of parameter types is created on the fly.
		MethodBuilder meth = tb.DefineMethod(
			"MyMethod",
			MethodAttributes.Public,
			typeof(int),
			new Type[] { typeof(int) });

		ILGenerator methIL = meth.GetILGenerator();
		// To retrieve the private instance field, load the instance it
		// belongs to (argument zero). After loading the field, load the
		// argument one and then multiply. Return from the method with
		// the return value (the product of the two numbers) on the
		// execution stack.
		methIL.Emit(OpCodes.Ldarg_0);
		methIL.Emit(OpCodes.Ldfld, fbNumber);
		methIL.Emit(OpCodes.Ldarg_1);
		methIL.Emit(OpCodes.Mul);
		methIL.Emit(OpCodes.Ret);

		var t = tb.CreateTypeInfo();

		//ab.Save(aName.Name + ".dll");

		MethodInfo mi = t.GetMethod("MyMethod");
		PropertyInfo pi = t.GetProperty("Number");

		object o1 = Activator.CreateInstance(tb.CreateTypeInfo());

		Console.WriteLine(pi.GetValue(o1, null));

		//AssemblyLoadContext c = AssemblyLoadContext.Default;
		//Assembly asm = c.LoadFromStream(ms);
		//Type t = asm.GetType("MyType");
	}
}

//[assembly:InternalsVisibleTo("Cli")]
//[assembly:InternalsVisibleTo("Test")]

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
