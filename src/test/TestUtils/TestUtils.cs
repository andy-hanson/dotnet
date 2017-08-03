using System;

using static Utils;

namespace Test {
	static class TestUtils {
		internal static void runTestSpecial(TestData t) =>
			t.jsTestRunner.runTestSpecial(t.testPath);

		internal static void runCsJsTests(TestData t) {
			foreach (var module in t.compiledProgram.modules.values)
				verifyModule((Model.Module)module);

			var csres = t.emittedRoot.invokeStatic("main");
			assert(csres == Builtins.Void.instance); // Test must return void
			t.jsTestRunner.runTest(t.testPath);
		}

		static void verifyModule(Model.Module module) {
			foreach (var method in module.klass.methods) {
				if (method is Model.MethodWithBody m)
					verifyExpr(m, m.body);
			}
		}

		static void verifyExpr(Model.MethodOrImplOrExpr parent, Model.Expr expr) {
			assert(object.ReferenceEquals(parent, expr.parent));
			foreach (var child in expr.children())
				verifyExpr(expr, child);
		}

		internal static void assertEqual<T>(T expected, T actual) where T : ToData<T> {
			if (expected.deepEqual(actual))
				return;
			Console.WriteLine("Expected: " + CsonWriter.write(expected));
			Console.WriteLine("Actual: " + CsonWriter.write(actual));
			throw TODO();
		}
	}
}
