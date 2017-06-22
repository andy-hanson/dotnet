using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;

using Module = Model.Module;
using static Utils;

using static TestUtils;

static class TestCompile {
	static readonly Path testRootDir = Path.from("tests");

	internal static void runAllCompilerTests() {
		foreach (var m in methods())
			foo(m);
		//runCompilerTests(rootDir);
	}

	static Arr<(MethodInfo, TestFor)> methods() =>
		new Arr<MethodInfo>(typeof(Tests).GetMethods(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
		.mapDefined(method => {
			assert(method.IsStatic);
			var testForAttribute = method.GetCustomAttribute(typeof(TestFor));
			if (testForAttribute == null)
				return Op<(MethodInfo, TestFor)>.None;
			var testFor = (TestFor) testForAttribute;
			return Op.Some((method, testFor));
		});

	internal static void runSingle(string name) {
		if (!methods().find(out var found, mtf => mtf.Item2.testPath.ToString() == name))
			throw new Exception($"No such test {name}");

		foo(found);
	}

	static void foo((MethodInfo, TestFor) m) {
		var (method, testFor) = m;
		var testData = runCompilerTest(Path.from(testFor.testPath));
		method.Invoke(null, new object[] { testData });
	}

	static TestData runCompilerTest(Path testPath) {
		var rootDir = testRootDir.resolve(testPath.asRel);
		var host = DocumentProviders.CommandLine(rootDir);
		var (program, m) = Compiler.compile(Path.empty, host, Op<CompiledProgram>.None);

		foreach (var pair in program.modules) {
			var module = pair.Value;
			var path = module.fullPath().removeExtension(ModuleResolver.extension);

			// TODO: break out if there was an error
			assertSomething(rootDir, path, ".ast", Dat.either(module.document.parseResult));

			assertSomething(rootDir, path, ".model", module.klass.toDat());

			assertSomething(rootDir, path, ".js", JsEmitter.emitToString(module));

			//module.imports; We'll just not test this then...
		}

		return new TestData(program, m, rootDir.add("index.js"));

		//var x = CsonWriter.write(program);
		//var moduleAst = new Ast.Module()
		//var document = new DocumentInfo("static\n\nfun Void foo()\n\tpass\n", 1, Either<Module, CompileError>.Left(moduleAst))
		//var moduleA = new Module(Path.from("A"), isIndex: false, document: document, klass: klass);
		//var expected = new CompiledProgram(host, Dict.of(Sym.of("A"), moduleA));

		//Console.WriteLine(x);
	}

	static void assertSomething(Path rootDir, Path path, string extension, Dat actualDat) =>
		assertSomething(rootDir, path, extension, CsonWriter.write(actualDat) + "\n");

	static void assertSomething(Path rootDir, Path path, string extension, string actual) {
		var fullPath = rootDir.resolve(path.asRel).addExtension(extension).ToString();

		// If it doesn't exist, create it.
		string expected;
		try {
			expected = File.ReadAllText(fullPath);
		} catch (FileNotFoundException) {
			// Write the new result.
			File.WriteAllText(fullPath, actual);
			return;
		}

		if (actual == expected) {
			// Great!
			return;
		}

		Console.WriteLine("Unexpected output!");
		Console.WriteLine($"Expected: {expected}");
		Console.WriteLine($"Actual: {actual}");
		//TODO: put under --accept option
		File.WriteAllText(fullPath, actual);

		return;
	}

	//mv
	static void TestCompiler(Path path) {
		// We will include output in the directory.
	}
}


[AttributeUsage(AttributeTargets.Method)]
sealed class TestFor : Attribute {
	internal readonly string testPath;
	internal TestFor(string testRootDir) { this.testPath = testRootDir; }
}

static class Tests {
	[TestFor("Main")]
	static void T1(TestData t) {
		runCsJsTests(t, new object[] {}, Builtins.Void.instance);
	}

	[TestFor("AbstractClass")]
	static void T2(TestData t) {
		// We need to implement its abstract class.
		var cls = t.emittedRoot;
		var impl = implementType(cls, new T2Impl());
		var csres = cls.GetMethod("main").Invoke(null, new object[] { impl });
		assertEqual(Builtins.Int.of(2), csres);
	}

	sealed class T2Impl {
		public Builtins.Int n() {
			return Builtins.Int.of(1);
		}
	}

	//mv
	//Stores a reference to 'o' and redirects all calls to it.
	//'o' must match the signatures of the abstract methods on the type we're implementing.
	internal static object implementType(Type typeToImplement, object o) {
		assert(typeToImplement.IsAbstract);

		var implementerName = $"implement_{typeToImplement.Name}";
		var assemblyName = new AssemblyName(implementerName);
		var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndCollect);
		var moduleBuilder = assemblyBuilder.DefineDynamicModule(implementerName);
		var implementer = moduleBuilder.DefineType(implementerName, TypeAttributes.Public | TypeAttributes.Sealed, typeToImplement);

		var oType = o.GetType();
		assert(oType.IsSealed);

		//It will have a field of type 'o'.
		var field = implementer.DefineField("implementation", oType, FieldAttributes.Public);

		var overrides = oType.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance);
		foreach (var implementationMethod in overrides) {
			var name = implementationMethod.Name;
			var methodToOverride = typeToImplement.GetMethod(name);
			assert(methodToOverride != null);
			assert(methodToOverride.IsAbstract);

			//Override the method!
			var mb = implementer.DefineMethod(
				name,
				MethodAttributes.Public,
				methodToOverride.ReturnType,
				methodToOverride.GetParameters().mapToArray(p => p.ParameterType));

			var il = mb.GetILGenerator();
			var nParameters = unsigned(methodToOverride.GetParameters().Length);
			doTimes(nParameters, idx => { il.Emit(ILWriter.ldargOperation(idx, isStatic: false)); });

			//Ld field
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Ldfld, field);

			//Call its method.
			il.Emit(OpCodes.Call, implementationMethod);
			il.Emit(OpCodes.Ret);
		}

		var ctrBuilder = implementer.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new Type[] { oType });
		ctrBuilder.DefineParameter(0, ParameterAttributes.None, "field");
		var ctrIl = ctrBuilder.GetILGenerator();
		//Set the field
		ctrIl.Emit(OpCodes.Ldarg_0);
		ctrIl.Emit(OpCodes.Ldarg_1);
		ctrIl.Emit(OpCodes.Stfld, field);
		ctrIl.Emit(OpCodes.Ret);

		var type = implementer.CreateType();
		//Instantiate it

		var instance = Activator.CreateInstance(type, o);
		return instance;
	}
}

static class TestUtils {
	internal static void runCsJsTests<T>(TestData t, object[] arguments, T expected) where T : ToData<T> {
		var csmethod = t.emittedRoot.GetMethod("main");
		var csres = csmethod.Invoke(null, arguments);
		assertEqual(expected, csres);

		var jscls = JsRunner.evalScript(t.indexJs);
		var jsres = jscls.invokeMethod("main", arguments.mapToArray(toJsValue));
		assertEqual2(toJsValue(expected), jsres);
	}

	//mv
	internal static Jint.Native.JsValue toJsValue(object o) {
		switch (o) {
			case Builtins.Void v:
				return Jint.Native.JsValue.Undefined;
			case Builtins.Bool b:
				return b.value ? Jint.Native.JsValue.True : Jint.Native.JsValue.False;
			case Builtins.Int i:
				//TODO: this gives me a primitive, right?
				return new Jint.Native.JsValue(i.value);
			case Builtins.Float f:
				return new Jint.Native.JsValue(f.value);
			case Builtins.Str s:
				return new Jint.Native.JsValue(s.value);
			default:
				throw TODO();
		}
	}

	internal static void assertEqual2(Jint.Native.JsValue expected, Jint.Native.JsValue actual) {
		if (!expected.Equals(actual)) {
			Console.WriteLine(expected);
			Console.WriteLine($"Expected: {expected}");
			Console.WriteLine($"Actual: {actual}");
			throw TODO();
		}
	}

	//mv
	internal static void assertEqual<T>(T expected, object actual) where T : ToData<T> {
		var act = (T) actual;
		if (!expected.deepEqual(act)) {
			Console.WriteLine($"Expected: {CsonWriter.write(expected)}");
			Console.WriteLine($"Actual: {CsonWriter.write(act)}");
			throw TODO();
		}
	}
}


//Code side of a test
sealed class TestData {
	internal readonly CompiledProgram compiledProgram;
	internal readonly Module rootModule;
	internal readonly Path indexJs;
	internal readonly ILEmitter emitter; // Will always be emitted before running custom test.
	internal readonly Type emittedRoot;

	internal TestData(CompiledProgram compiledProgram, Module rootModule, Path indexJs) {
		this.compiledProgram = compiledProgram;
		this.rootModule = rootModule;
		this.indexJs = indexJs;
		this.emitter = new ILEmitter();
		this.emittedRoot = emitter.emitModule(rootModule);
	}
}
