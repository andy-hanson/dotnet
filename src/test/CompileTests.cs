using static Test.TestUtils;

#pragma warning disable CC0068 // Private methods not used

namespace Test {
	static class Tests {
		static readonly Builtins.Int one = Builtins.Int.of(1);

		[TestFor("Main-Pass")]
		static void MainPass(TestData t) => runCsJsTests(t);

		[TestFor("Abstract-Class")]
		static void AbstractClass(TestData t) { //TODO:NEATER
			// We need to implement its abstract class.
			var cls = t.emittedRoot;
			var impl = DynamicImplementUtils.implementType(cls, new AbstractClassImpl());
			cls.invokeStatic("main", impl);
			t.jsTestRunner.runTestSpecial(t.testPath);
		}

		[TestFor(nameof(Impl))]
		static void Impl(TestData t) => runCsJsTests(t);

		[TestFor(nameof(Slots))]
		static void Slots(TestData t) => runCsJsTests(t);

		[TestFor(nameof(Assert))]
		static void Assert(TestData t) => runCsJsTests(t);

		[TestFor(nameof(Try))]
		static void Try(TestData t) => runCsJsTests(t);

		[TestFor("Multiple-Inheritance")]
		static void MultipleInheritance(TestData t) => runCsJsTests(t);

		[TestFor("Console-App-User")]
		static void ConsoleAppUser(TestData t) {
			var cls = t.emittedRoot;
			cls.invokeStatic("main", new BuiltinImpls.ConsoleApp(installationDirectory: t.testPath, currentWorkingDirectory: t.testPath));
		}

		[TestFor(nameof(Recur))]
		static void Recur(TestData t) => runCsJsTests(t);

		[TestFor("Operator-Parsing")]
		static void OperatorParsing(TestData t) => runCsJsTests(t);

		[TestFor(nameof(Literals))]
		static void Literals(TestData t) => runCsJsTests(t);
	}

	// Must be public since it's used dynamically
	public sealed class AbstractClassImpl {
		#pragma warning disable CC0091 // Can't make it static
		public Builtins.String s() => Builtins.String.of(nameof(s));
		#pragma warning restore
	}
}
