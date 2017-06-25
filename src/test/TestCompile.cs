using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.ExceptionServices;

using static TestUtils;
using static Utils;

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
			var testFor = (TestFor)testForAttribute;
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

	static TestCompile() {
		AppDomain.CurrentDomain.FirstChanceException += handleFirstChanceException;
	}

	static void handleFirstChanceException(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e) {
		//https://stackoverflow.com/questions/15833498/how-to-not-breaking-on-an-exception
		if (!(e.Exception is System.Reflection.TargetInvocationException)) {
			System.Diagnostics.Debugger.Break();
		}
	}
}

[AttributeUsage(AttributeTargets.Method)]
sealed class TestFor : Attribute {
	internal readonly string testPath;
	internal TestFor(string testRootDir) { this.testPath = testRootDir; }
}

public sealed class T2Impl {
	#pragma warning disable CC0091 // Can't make it static
	public Builtins.Int n() => Builtins.Int.of(1);
	#pragma warning restore
}

static class Tests {
	static readonly Builtins.Int one = Builtins.Int.of(1);

	[TestFor(nameof(MainPass))]
	static void MainPass(TestData t) {
		runCsJsTests(t, new object[] {}, Builtins.Void.instance);
	}

	[TestFor(nameof(AbstractClass))]
	static void AbstractClass(TestData t) { //TODO:NEATER
		// We need to implement its abstract class.
		var cls = t.emittedRoot;
		var impl = implementType(cls, new T2Impl());
		object csres;
		try {
			csres = cls.GetMethod("main").Invoke(null, new object[] { impl });
		} catch (TargetInvocationException e) {
			throw e.InnerException;
		}
		var expected = one;
		assertEqual(expected, csres);

		//Also in JS
		var engine = new Jint.Engine();
		var jscls = JsRunner.evalScript(engine, t.indexJs);
		var jsImpl = Jint.Native.JsValue.FromObject(engine, foooo(engine, jscls, impl));

		var jsres = jscls.invokeMethod("main", jsImpl);
		assertEqual2(JsConvert.toJsValue(expected), jsres);
	}

	[TestFor(nameof(Impl))]
	static void Impl(TestData t) {
		var cls = t.emittedRoot;
		Console.WriteLine("!");
		throw TODO();
	}

	[TestFor(nameof(Slots))]
	static void Slots(TestData t) {
		runCsJsTests(t, new object[] { one }, one);
	}

	//TODO:KILL
	/*internal static JsValue toJs(Jint.Engine e, Jint.Native.JsValue abstractClass, object o) {
		/* //Jint.Object

		//new object
		var ctr = abstractClass.TryCast<Jint.Native.IConstructor>();
		var instance = ctr.Construct(Array.Empty<Jint.Native.JsValue>());

		//For each public methon on "o", make a function instance wrapping it.


		Jint.Native.Function.BindFunctionInstance

		instance.Put("n", )* /

		//For each public property in o, assign to the instance.



		//new Jint.Runtime.ExpressionInterpreter(e).EvaluateNewExpression()


		//e.Object.Create(e.Object, abstractClass.AsObject().Get("prototype"));

		//abstractClass.Invoke();

		//e.Object.
		//instantiate the class, then copy properties.
		//Jint.Native.Object.ObjectConstructor.
	}*/

	static Jint.Native.JsValue foooo(Jint.Engine engine, Jint.Native.JsValue clsToImplement, object o) {
		var x = clsToImplement.As<Jint.Native.Function.FunctionInstance>();
		var clsToImplementName = Jint.Runtime.TypeConverter.ToString(x.Get("name"));

		var wrapper = wrapMethodsInJsValueConversion(clsToImplementName, o);
		var instance = Jint.Native.JsValue.FromObject(engine, wrapper).AsObject();
		instance.Prototype = x.Get("prototype").AsObject();

		return instance;
	}

	static object wrapMethodsInJsValueConversion(string clsToImplementName, object o) {
		//Creates a type that wraps o's methods and converts things.
		//For example, if a parameter is of type Builtins.Bool, we will change it to a function taking a Jint.Native.JsValue that's expected to be a Bool.

		//duplicate code in implementType
		var implementerName = $"implement_{clsToImplementName}";
		var assemblyName = new AssemblyName(implementerName);
		var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndCollect);
		var moduleBuilder = assemblyBuilder.DefineDynamicModule(implementerName);
		var implementer = moduleBuilder.DefineType(implementerName, TypeAttributes.Public | TypeAttributes.Sealed);

		var oType = o.GetType();
		assert(oType.IsSealed);

		var field = implementer.DefineField("implementation", oType, FieldAttributes.Public);

		var overrides = oType.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance);
		foreach (var implementationMethod in overrides) {
			var name = implementationMethod.Name;
			var implementationParams = implementationMethod.GetParameters();
			var convertedParameters = implementationParams.mapToArray(p => JsConvert.mpType(p.ParameterType));
			var convertedReturnType = JsConvert.mpType(implementationMethod.ReturnType);
			var mb = implementer.DefineMethod(
				name,
				MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final,
				convertedReturnType,
				convertedParameters);

			var il = mb.GetILGenerator();

			implementationParams.each((param, idx) => {
				il.Emit(ILWriter.ldargOperation(idx, isStatic: false));
				var convertedParameterType = convertedParameters[idx];
				// Might have to convert it.
				if (JsConvert.converterFromJs(param.ParameterType, out var mi)) {
					var miParams = mi.GetParameters();
					assert(miParams.Length == 1);
					assert(miParams[0].ParameterType == convertedParameterType);
					assert(mi.ReturnType == param.ParameterType);

					il.Emit(OpCodes.Call, mi);
				} else {
					assert(param.ParameterType == convertedParameterType);
				}
			});

			//var nParameters = unsigned(methodToOverride.GetParameters().Length);
			//doTimes(nParameters, idx => { il.Emit(ILWriter.ldargOperation(idx, isStatic: false)); });

			//Ld field
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Ldfld, field);

			//Call its method.
			il.Emit(OpCodes.Call, implementationMethod);

			// Might have to convert the return value.
			if (JsConvert.converterToJs(implementationMethod.ReturnType, out var miCnv)) {
				var miParams = miCnv.GetParameters();
				assert(miParams.Length == 1);
				assert(miParams[0].ParameterType == implementationMethod.ReturnType);
				assert(miCnv.ReturnType == convertedReturnType);
				il.Emit(OpCodes.Call, miCnv);
			} else {
				assert(implementationMethod.ReturnType == convertedReturnType);
			}

			il.Emit(OpCodes.Ret);
		}

		//dup
		var ctrBuilder = implementer.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new Type[] { oType });
		ctrBuilder.DefineParameter(0, ParameterAttributes.None, nameof(field));
		var ctrIl = ctrBuilder.GetILGenerator();
		//Set the field
		ctrIl.Emit(OpCodes.Ldarg_0);
		ctrIl.Emit(OpCodes.Ldarg_1);
		ctrIl.Emit(OpCodes.Stfld, field);
		ctrIl.Emit(OpCodes.Ret);

		var type = implementer.CreateType();
		return Activator.CreateInstance(type, o);
	}

	//mv
	//Stores a reference to 'o' and redirects all calls to it.
	//'o' must match the signatures of the abstract methods on the type we're implementing.
	static object implementType(Type typeToImplement, object o) {
		assert(typeToImplement.IsAbstract);

		var implementerName = $"implement_{typeToImplement.Name}";
		var assemblyName = new AssemblyName(implementerName);
		var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndCollect);
		var moduleBuilder = assemblyBuilder.DefineDynamicModule(implementerName);
		var implementer = moduleBuilder.DefineType(implementerName, TypeAttributes.Public | TypeAttributes.Sealed, typeToImplement);

		var oType = o.GetType();
		assert(oType.IsSealed);

		var field = implementer.DefineField("implementation", oType, FieldAttributes.Public);

		var overrides = oType.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance);
		foreach (var implementationMethod in overrides) {
			var name = implementationMethod.Name;
			var methodToOverride = typeToImplement.GetMethod(name);
			assert(methodToOverride != null);
			assert(methodToOverride.IsAbstract);

			assert(implementationMethod.ReturnType == methodToOverride.ReturnType);
			var paramTypes = implementationMethod.GetParameters().zip(methodToOverride.GetParameters(), (imParam, oParam) => {
				assert(imParam.ParameterType == oParam.ParameterType);
				return oParam.ParameterType;
			});

			//Override the method!
			var mb = implementer.DefineMethod(
				name,
				// Virtual and final? Yes. Virtual means: overrides something. Final means: Can't be overridden itself.
				MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final,
				methodToOverride.ReturnType,
				paramTypes);

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
		ctrBuilder.DefineParameter(0, ParameterAttributes.None, nameof(field));
		var ctrIl = ctrBuilder.GetILGenerator();
		//Set the field
		ctrIl.Emit(OpCodes.Ldarg_0);
		ctrIl.Emit(OpCodes.Ldarg_1);
		ctrIl.Emit(OpCodes.Stfld, field);
		ctrIl.Emit(OpCodes.Ret);

		var type = implementer.CreateType();
		return Activator.CreateInstance(type, o);
	}
}

//NOT public, treat as private to JsConvert
public static class Converters {
	public static Jint.Native.JsValue __voidToJs(Builtins.Void v) {
		unused(v);
		return Jint.Native.JsValue.Undefined;
	}
	public static Jint.Native.JsValue __boolToJs(Builtins.Bool b) =>
		b.value ? Jint.Native.JsValue.True : Jint.Native.JsValue.False;
	public static Jint.Native.JsValue __intToJs(Builtins.Int i) =>
		new Jint.Native.JsValue(i.value);
	public static Jint.Native.JsValue __floatToJs(Builtins.Float f) =>
		new Jint.Native.JsValue(f.value);
	public static Jint.Native.JsValue __strToJs(Builtins.Str s) =>
		new Jint.Native.JsValue(s.value);

	public static Builtins.Void __voidFromJs(Jint.Native.JsValue j) {
		assert(j == Jint.Native.JsValue.Undefined);
		return Builtins.Void.instance;
	}
	public static Builtins.Bool __boolFromJs(Jint.Native.JsValue j) =>
		Builtins.Bool.of(j.AsBoolean());
	public static Builtins.Int __intFromJs(Jint.Native.JsValue j) {
		var n = j.AsNumber();
		assert(n % 1 == 0);
		return Builtins.Int.of((int)n);
	}
	public static Builtins.Float __floatFromJs(Jint.Native.JsValue j) =>
		Builtins.Float.of(j.AsNumber());
	public static Builtins.Str __strFromJs(Jint.Native.JsValue j) =>
		Builtins.Str.of(j.AsString());
}

//mv
static class JsConvert {
	internal static Type mpType(Type t) =>
		convertersToJs.ContainsKey(t) ? typeof(Jint.Native.JsValue) : t;

	internal static Jint.Native.JsValue toJsValue(object o) {
		switch (o) {
			case Builtins.Void v:
				return Converters.__voidToJs(v);
			case Builtins.Bool b:
				return Converters.__boolToJs(b);
			case Builtins.Int i:
				return Converters.__intToJs(i);
			case Builtins.Float f:
				return Converters.__floatToJs(f);
			case Builtins.Str s:
				return Converters.__strToJs(s);
			default:
				throw TODO();
		}
	}

	static Dictionary<Type, MethodInfo> convertersToJs = new Dictionary<Type, string> {
		{ typeof(Builtins.Void), nameof(Converters.__voidToJs) },
		{ typeof(Builtins.Bool), nameof(Converters.__boolToJs) },
		{ typeof(Builtins.Int), nameof(Converters.__intToJs) },
		{ typeof(Builtins.Float), nameof(Converters.__floatToJs) },
		{ typeof(Builtins.Str), nameof(Converters.__strToJs) }
	}.mapValuesToDictionary(typeof(Converters).GetMethod);

	internal static bool converterToJs(Type t, out MethodInfo m) => convertersToJs.TryGetValue(t, out m);

	static Dictionary<Type, MethodInfo> convertersFromJs = new Dictionary<Type, string> {
		{ typeof(Builtins.Void), nameof(Converters.__voidFromJs) },
		{ typeof(Builtins.Bool), nameof(Converters.__boolFromJs) },
		{ typeof(Builtins.Int), nameof(Converters.__intFromJs) },
		{ typeof(Builtins.Float), nameof(Converters.__floatFromJs) },
		{ typeof(Builtins.Str), nameof(Converters.__strFromJs) }
	}.mapValuesToDictionary(typeof(Converters).GetMethod);

	internal static bool converterFromJs(Type t, out MethodInfo m) => convertersFromJs.TryGetValue(t, out m);
}

static class TestUtils {
	internal static void runCsJsTests<T>(TestData t, object[] arguments, T expected) where T : ToData<T> {
		var csmethod = t.emittedRoot.GetMethod("main");
		object csres;
		try {
			csres = csmethod.Invoke(null, arguments);
		} catch (TargetInvocationException e) {
			ExceptionDispatchInfo.Capture(e.InnerException).Throw();
		}
		assertEqual(expected, csres);

		var engine = new Jint.Engine();
		var jscls = JsRunner.evalScript(engine, t.indexJs);
		var jsres = jscls.invokeMethod("main", arguments.mapToArray(JsConvert.toJsValue));
		assertEqual2(JsConvert.toJsValue(expected), jsres);
	}

	internal static void assertEqual2(Jint.Native.JsValue expected, Jint.Native.JsValue actual) {
		if (!expected.Equals(actual)) {
			Console.WriteLine($"Expected: {expected.GetType()} {expected}");
			Console.WriteLine($"Actual: {actual.GetType()} {actual}");
			throw TODO();
		}
	}

	//mv
	internal static void assertEqual<T>(T expected, object actual) where T : ToData<T> {
		var act = (T)actual;
		if (!expected.deepEqual(act)) {
			Console.WriteLine($"Expected: {CsonWriter.write(expected)}");
			Console.WriteLine($"Actual: {CsonWriter.write(act)}");
			throw TODO();
		}
	}
}

sealed class TestData {
	internal readonly CompiledProgram compiledProgram;
	internal readonly Model.Module rootModule;
	internal readonly Path indexJs;
	internal readonly ILEmitter emitter; // Will always be emitted before running custom test.
	internal readonly Type emittedRoot;

	internal TestData(CompiledProgram compiledProgram, Model.Module rootModule, Path indexJs) {
		this.compiledProgram = compiledProgram;
		this.rootModule = rootModule;
		this.indexJs = indexJs;
		this.emitter = new ILEmitter();
		this.emittedRoot = emitter.emitModule(rootModule);
	}
}
