﻿using System;
using System.Reflection;
using System.Reflection.Emit;
//dotnet add package System.Runtime
//dotnet add package System.Runtime.Loader
//dotnet add package System.Reflection.Emit
//dotnet add package System.Collections.Immutable
//dotnet add package AustinHarris.JsonRpc
	//TODO: remove that.
using Lsp;
using Json;

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

class Program {
	static void Main(string[] args) {
		//testJsonParse();
		//testService();
		testCompiler();
	}

	static void testJsonParse() {
		//TODO: move to a real unit test!!!
		var source = @"{
			'jsonrpc': '2.0',
			'id': 0,
			'method': 'initialize',
			'params': {
			'processId': 27882,
			'rootPath': null,
			'rootUri': null,
			'capabilities': {
				'workspace': {
				'applyEdit': true,
				'didChangeConfiguration': {
					'dynamicRegistration': false
				},
				'didChangeWatchedFiles': {
					'dynamicRegistration': false
				},
				'symbol': {
					'dynamicRegistration': true
				},
				'executeCommand': {
					'dynamicRegistration': true
				}
				},
				'textDocument': {
				'synchronization': {
					'dynamicRegistration': true,
					'willSave': true,
					'willSaveWaitUntil': true,
					'didSave': true
				},
				'completion': {
					'dynamicRegistration': true,
					'completionItem': {
					'snippetSupport': true
					}
				},
				'hover': {
					'dynamicRegistration': true
				},
				'signatureHelp': {
					'dynamicRegistration': true
				},
				'references': {
					'dynamicRegistration': true
				},
				'documentHighlight': {
					'dynamicRegistration': true
				},
				'documentSymbol': {
					'dynamicRegistration': true
				},
				'formatting': {
					'dynamicRegistration': true
				},
				'rangeFormatting': {
					'dynamicRegistration': true
				},
				'onTypeFormatting': {
					'dynamicRegistration': true
				},
				'definition': {
					'dynamicRegistration': true
				},
				'codeAction': {
					'dynamicRegistration': true
				},
				'codeLens': {
					'dynamicRegistration': true
				},
				'documentLink': {
					'dynamicRegistration': true
				},
				'rename': {
					'dynamicRegistration': true
				}
				}
			},
			'trace': 'off'
			}
		}".Replace('\'', '"');
		JsonParser.parseInitialize(source);
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
		var m = Compiler.compile(Path.from("sample", "A"), host, Op<CompiledProgram>.None, out var newProgram);

		//var cmp = //new CompilerDaemon(DocumentProviders.CommandLine(rootDir));
		//var m = cmp.compile(Path.from("sample", "A"));

		var e = new Emitter();
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

		object o1 = Activator.CreateInstance(tb.CreateType());

		Console.WriteLine(pi.GetValue(o1, null));

		//AssemblyLoadContext c = AssemblyLoadContext.Default;
		//Assembly asm = c.LoadFromStream(ms);
		//Type t = asm.GetType("MyType");
	}
}
