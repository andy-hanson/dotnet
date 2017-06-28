using System.Reflection;
using static TestUtils;

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
		var impl = TestUtils.implementType(cls, new AbstractClassImpl());
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
		var jsImpl = Jint.Native.JsValue.FromObject(engine, TestUtils.foooo(engine, jscls, impl));

		var jsres = jscls.invokeMethod("main", jsImpl);
		assertEqual2(JsConvert.toJsValue(expected), jsres);
	}

	[TestFor(nameof(Impl))]
	static void Impl(TestData t) {
		runCsJsTests(t, new object[] { one }, one);
	}

	[TestFor(nameof(Slots))]
	static void Slots(TestData t) {
		runCsJsTests(t, new object[] { one }, one);
	}
}

// Must be public since it's used dynamically
public sealed class AbstractClassImpl {
	#pragma warning disable CC0091 // Can't make it static
	public Builtins.Int n() => Builtins.Int.of(1);
	#pragma warning restore
}
