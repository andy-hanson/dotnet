module.exports = cls => {
	class Impl extends cls {
		s() { return "s"; }
	}
	return cls.main(new Impl());
};

/*
// We need to implement its abstract class.
var cls = t.emittedRoot;
var impl = TestUtils.implementType(cls, new AbstractClassImpl());
object csres;
try {
	csres = cls.GetMethod("main").Invoke(null, new object[] { impl });
} catch (TargetInvocationException e) {
	ExceptionDispatchInfo.Capture(e.InnerException).Throw();
	throw unreachable();
}
var expected = one;
assertEqual(expected, csres);

//Also in JS
var engine = new Jint.Engine(options => {
	options.DebugMode(true);
	options.Strict(true);
});
var jscls = JsRunner.evalScript(engine, t.indexJs);
var jsImpl = Jint.Native.JsValue.FromObject(engine, TestUtils.foooo(engine, jscls, impl));

var jsres = jscls.invokeMethod("main", jsImpl);
assertEqual2(JsConvert.toJsValue(expected), jsres);
*/
