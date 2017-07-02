using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.ExceptionServices;

using static Utils;

static class TestUtils {
	internal static void runTestSpecial(TestData t) =>
		t.jsTestRunner.runTestSpecial(t.testPath);

	internal static void runCsJsTests(TestData t) {
		var csmethod = t.emittedRoot.GetMethod("main");
		try {
			var csres = csmethod.Invoke(null, null);
			assert(csres == Builtins.Void.instance); // Test must return void
		} catch (TargetInvocationException e) {
			ExceptionDispatchInfo.Capture(e.InnerException).Throw();
			throw unreachable();
		}

		t.jsTestRunner.runTest(t.testPath);
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

	//mv
	internal static void assertEqual<T>(T expected, T actual) where T : ToData<T> {
		if (expected.deepEqual(actual))
			return;
		Console.WriteLine($"Expected: {CsonWriter.write(expected)}");
		Console.WriteLine($"Actual: {CsonWriter.write(actual)}");
		throw TODO();
	}
	internal static void assertEqual(Op<string> expected, Op<string> actual) {
		if (expected.deepEqual(actual))
			return;

		Console.WriteLine($"Expected: {expected}");
		Console.WriteLine($"Actual: {actual}");
		throw TODO();
	}
}
