using System;

using static Utils;

namespace Test {
	static class TestUtils {
		internal static void runTestSpecial(TestData t) =>
			t.jsTestRunner.runTestSpecial(t.testPath);

		internal static void runCsJsTests(TestData t) {
			var csres = t.emittedRoot.invokeStatic("main");
			assert(csres == Builtins.Void.instance); // Test must return void
			t.jsTestRunner.runTest(t.testPath);
		}

		internal static void assertEqual<T>(T expected, T actual) where T : ToData<T> {
			if (expected.deepEqual(actual))
				return;
			Console.WriteLine($"Expected: {CsonWriter.write(expected)}");
			Console.WriteLine($"Actual: {CsonWriter.write(actual)}");
			throw TODO();
		}
	}
}
