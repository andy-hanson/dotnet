using System;
using System.IO;
//using System.Text.RegularExpressions;

using static Utils;

static class JsValueUtils {
	internal static Jint.Native.JsValue invokeMethod(this Jint.Native.JsValue instance, string methodName, params Jint.Native.JsValue[] args) {
		var property = instance.AsObject().Get(methodName);
		var method = property.TryCast<Jint.Native.Function.FunctionInstance>(_ => {
			Console.WriteLine($"Expected a method, got: {property}");
			throw TODO();
		});
		return method.Call(instance, args);
	}
}

static class JsRunner {
	internal static Jint.Native.JsValue evalScript(Jint.Engine engine, Path path) {
		try {
			return evalScriptAtFile(engine, path);
		} catch (Jint.Runtime.JavaScriptException e) {
			var line = e.LineNumber;
			var col = e.Column;
			throw new Exception($"Error at {line}:{col}: {e.Message}\n{e.StackTrace}");
		}
	}

	// Note: This is not a generic commonjs loader.
	// It assumes that the last line of each file is `module.exports = foo;`.
	// And it assumes that there are no circular dependencies.
	static Jint.Native.JsValue evalScriptAtFile(Jint.Engine engine, Path path) {
		var text = File.ReadAllText(path.ToString());
		// Since this is the last line, we'll just get the completion value.
		text = text.Replace("module.exports = ", "");

		Func<string, Jint.Native.JsValue> requireDelegate = required => evalScriptAtFile(engine, resolveRequirePath(path, required));
		var require = new Jint.Runtime.Interop.DelegateWrapper(engine, new Func<string, Jint.Native.JsValue>(requireDelegate));

		//Jint.Runtime.Environments.EnvironmentRecord
		var r = new Jint.Runtime.Environments.DeclarativeEnvironmentRecord(engine);
		r.CreateImmutableBinding("require"); //needed?
		r.InitializeImmutableBinding("require", require);

		var env = new Jint.Runtime.Environments.LexicalEnvironment(r, /*outer*/ null);

		engine.EnterExecutionContext(/*lexicalEnvironment*/ env, /*variableEnvironment*/ env, /*this*/ Jint.Native.JsValue.Null);
		engine.Execute(text);
		var result = engine.GetCompletionValue();
		engine.LeaveExecutionContext();

		return result;
	}

	static Path resolveRequirePath(Path requiredFrom, string required) {
		var requiredFromDir = requiredFrom.directory();
		var plain = requiredFromDir.add($"{required}.js");
		if (File.Exists(plain.ToString()))
			return plain;
		var index = requiredFromDir.add(required, "index.js");
		if (File.Exists(index.ToString()))
			return index;
		throw new Exception($"Could not find it at {plain} or at {index}");
	}

	//kill
	static void testJint2() {
		var engine = new Jint.Engine()
			.SetValue("log", new Action<object>(Console.WriteLine));

		var src = @"
		function Foo(x) {
			this.x = x;
		}
		Foo.foo = function(x) {
			return x + 1;
		}
		Foo.prototype.xIncr = function() {
			return this.x + 1;
		}
		Foo;
		";

		engine.Execute(src);

		var cmp = engine.GetCompletionValue();

		var foo = cmp.AsObject().Get("foo");
		var one = engine.Number.Construct(1.0);
		var two = foo.Invoke(one);
		Console.WriteLine(two);

		var cmpCtr = cmp.TryCast<Jint.Native.IConstructor>(_ => throw TODO());

		var instance = cmpCtr.Construct(new Jint.Native.JsValue[] { one });

		var xIncr = instance.Get("xIncr").TryCast<Jint.Native.Function.FunctionInstance>(_ => throw TODO());
		var two2 = xIncr.Call(instance, new Jint.Native.JsValue[] { one });
		Console.WriteLine(two2);
	}

	static void testJint() {
		var engine = new Jint.Engine()
			.SetValue("log", new Action<object>(Console.WriteLine));

		var src = @"
		(function(x) {
			return x + 1;
		});
		";

		engine.Execute(src);

		var cmp = engine.GetCompletionValue();
		var x = engine.Number.Construct(1.0);
		var xPlusOne = cmp.Invoke(x);
		Console.WriteLine(xPlusOne);
	}
}

