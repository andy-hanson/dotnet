using System;
using System.IO;
//using System.Text.RegularExpressions;

using static Utils;

static class JsRunner {
	internal static Jint.Native.JsValue evalScript(Jint.Engine engine, Path path) {
		var text = File.ReadAllText(path.ToString());
		// Since this is the last line, we'll just get the completion value.
		text = text.Replace("module.exports = ", "");

		Func<string, Jint.Native.JsValue> requireDelegate = required => evalScript(engine, resolveRequirePath(path, required));
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

	static readonly Sym symIndexJs = Sym.of("index.js");
	static Path resolveRequirePath(Path requiredFrom, string required) {
		var requiredFromDir = requiredFrom.directory();
		var plain = requiredFromDir.add(Sym.of($"{required}.js"));
		if (File.Exists(plain.ToString()))
			return plain;
		var index = requiredFromDir.add(Sym.of(required), symIndexJs);
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

