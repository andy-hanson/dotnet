using System;
using System.Diagnostics;
using System.IO;

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
			throw fail($"Error at {line}:{col}: {e.Message}\n{e.StackTrace}");
		}
	}

	// Note: This is not a generic commonjs loader.
	// It assumes that the last line of each file is `module.exports = foo;`.
	// And it assumes that there are no circular dependencies.
	static Jint.Native.JsValue evalScriptAtFile(Jint.Engine engine, Path path) {
		var text = File.ReadAllText(path.toPathString());
		// Since this is the last line, we'll just get the completion value.
		text = text.Replace("module.exports = ", string.Empty);

		Func<string, Jint.Native.JsValue> requireDelegate = required =>
			evalScriptAtFile(engine, resolveRequirePath(path, required));
		var require = new Jint.Runtime.Interop.DelegateWrapper(engine, requireDelegate);

		var envRecord = new Jint.Runtime.Environments.DeclarativeEnvironmentRecord(engine);
		envRecord.CreateImmutableBinding(nameof(require));
		envRecord.InitializeImmutableBinding(nameof(require), require);
		var env = new Jint.Runtime.Environments.LexicalEnvironment(record: envRecord, outer: engine.GlobalEnvironment);

		engine.EnterExecutionContext(/*lexicalEnvironment*/ env, /*variableEnvironment*/ env, /*this*/ Jint.Native.JsValue.Null);
		try {
			engine.Execute(text);
		} catch (Jint.Runtime.JavaScriptException e) {
			unused(e);
			Debugger.Break();
			throw;
		}

		var result = engine.GetCompletionValue();
		engine.LeaveExecutionContext();

		return result;
	}

	const string nzlib = "nzlib";
	static readonly Path nzlibPath = Path.fromParts(nzlib, "index.js");

	static Path resolveRequirePath(Path requiredFrom, string required) {
		if (required == nzlib)
			return nzlibPath;

		var requiredFromDir = requiredFrom.directory();
		var plain = requiredFromDir.add($"{required}.js");
		if (File.Exists(plain.toPathString()))
			return plain;

		var index = requiredFromDir.add(required, "index.js");
		if (File.Exists(index.toPathString()))
			return index;

		throw fail($"Could not find JS module at {plain.toPathString()} or at {index.toPathString()}");
	}
}
