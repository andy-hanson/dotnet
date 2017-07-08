using static Test.TestUtils;

namespace Test {
	static class Tests {
		static readonly Builtins.Int one = Builtins.Int.of(1);

		[TestFor(nameof(MainPass))]
		static void MainPass(TestData t) => runCsJsTests(t);

		[TestFor(nameof(AbstractClass))]
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

		[TestFor(nameof(MultipleInheritance))]
		static void MultipleInheritance(TestData t) => runCsJsTests(t);
	}

	// Must be public since it's used dynamically
	public sealed class AbstractClassImpl {
		#pragma warning disable CC0091 // Can't make it static
		public Builtins.String s() => Builtins.String.of(nameof(s));
		#pragma warning restore
	}
}
