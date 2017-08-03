#pragma warning disable CC0068 // Private methods not used

namespace Test {
	static class Tests {
		static readonly Builtins.Int one = Builtins.Int.of(1);

		[TestFor("Abstract-Class")]
		static void AbstractClass(TestData t) { //TODO:NEATER
			// We need to implement its abstract class.
			var cls = t.emittedRoot;
			var impl = DynamicImplementUtils.implementType(cls, new AbstractClassImpl());
			cls.invokeStatic("main", impl);
			t.jsTestRunner.runTestSpecial(t.testPath);
		}

		[TestFor("Console-App-User")]
		static void ConsoleAppUser(TestData t) {
			var cls = t.emittedRoot;
			cls.invokeStatic("main", new BuiltinImpls.ConsoleApp(installationDirectory: t.testPath, currentWorkingDirectory: t.testPath));
		}
	}

	// Must be public since it's used dynamically
	public sealed class AbstractClassImpl {
		#pragma warning disable CC0091 // Can't make it static
		public Builtins.String s() => Builtins.String.of(nameof(s));
		#pragma warning restore
	}
}
