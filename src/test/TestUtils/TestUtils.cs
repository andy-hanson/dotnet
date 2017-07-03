using System;
using System.Reflection;
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
