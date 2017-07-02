using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.ExceptionServices;

using static Utils;

static class TestUtils {
	internal static void runCsJsTests<T>(TestData t, object[] arguments, T expected) where T : ToData<T> {
		var csmethod = t.emittedRoot.GetMethod("main");
		object csres;
		try {
			csres = csmethod.Invoke(null, arguments);
		} catch (TargetInvocationException e) {
			ExceptionDispatchInfo.Capture(e.InnerException).Throw();
			throw unreachable();
		}
		assertEqual(expected, csres);

		var engine = new Jint.Engine();
		var jscls = JsRunner.evalScript(engine, t.indexJs);
		var jsres = jscls.invokeMethod("main", arguments.mapToArray(JsConvert.toJsValue));
		assertEqual2(JsConvert.toJsValue(expected), jsres);
	}

	internal static Jint.Native.JsValue foooo(Jint.Engine engine, Jint.Native.JsValue clsToImplement, object o) {
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
				//Mark implementer 'virtual' because the thing it's implementing is.
				MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final,
				convertedReturnType,
				convertedParameters);

			var il = new ILWriter(mb);

			implementationParams.each((param, idx) => {
				il.getParameter(idx);
				var convertedParameterType = convertedParameters[idx];
				// Might have to convert it.
				if (JsConvert.converterFromJs(param.ParameterType, out var mi)) {
					var miParams = mi.GetParameters();
					assert(miParams.Length == 1);
					assert(miParams[0].ParameterType == convertedParameterType);
					assert(mi.ReturnType == param.ParameterType);
					il.callStaticMethod(mi);
				} else {
					assert(param.ParameterType == convertedParameterType);
				}
			});

			//var nParameters = unsigned(methodToOverride.GetParameters().Length);
			//doTimes(nParameters, idx => { il.Emit(ILWriter.ldargOperation(idx, isStatic: false)); });

			//Ld field
			il.getThis();
			il.getField(field);

			//Call its method.
			il.callInstanceMethod(implementationMethod, isVirtual: false);

			// Might have to convert the return value.
			if (JsConvert.converterToJs(implementationMethod.ReturnType, out var miCnv)) {
				var miParams = miCnv.GetParameters();
				assert(miParams.Length == 1);
				assert(miParams[0].ParameterType == implementationMethod.ReturnType);
				assert(miCnv.ReturnType == convertedReturnType);
				il.callStaticMethod(miCnv);
			} else {
				assert(implementationMethod.ReturnType == convertedReturnType);
			}

			il.ret();
		}

		//dup
		var ctrBuilder = implementer.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new Type[] { oType });
		ctrBuilder.DefineParameter(0, ParameterAttributes.None, nameof(field));
		var ctrIl = new ILWriter(ctrBuilder);
		//Set the field
		ctrIl.getThis();
		ctrIl.getParameter(0);
		ctrIl.setField(field);
		ctrIl.ret();

		var type = implementer.CreateType();
		return Activator.CreateInstance(type, o);
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

			var il = new ILWriter(mb);
			var nParameters = unsigned(methodToOverride.GetParameters().Length);
			doTimes(nParameters, il.getParameter);

			il.getThis();
			il.getField(field);
			il.callInstanceMethod(implementationMethod, isVirtual: false);
			il.ret();
		}

		var ctrBuilder = implementer.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new Type[] { oType });
		ctrBuilder.DefineParameter(0, ParameterAttributes.None, nameof(field));
		var ctrIl = new ILWriter(ctrBuilder);
		//Set the field
		ctrIl.getThis();
		ctrIl.getParameter(0);
		ctrIl.setField(field);
		ctrIl.ret();

		var type = implementer.CreateType();
		return Activator.CreateInstance(type, o);
	}

	internal static void assertEqual2(Jint.Native.JsValue expected, Jint.Native.JsValue actual) {
		if (!jsValueEqual(expected, actual)) {
			Console.WriteLine($"Expected: {expected.GetType()} {expected}");
			Console.WriteLine($"Actual: {actual.GetType()} {actual}");
			throw TODO();
		}
	}

	static bool jsValueEqual(Jint.Native.JsValue a, Jint.Native.JsValue b) {
		if (a.IsBoolean() || a.IsNumber())
			// Works for these.
			return a.Equals(b);
		if (a.IsString())
			return b.IsString() && a.AsString() == b.AsString();

		throw TODO();
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
