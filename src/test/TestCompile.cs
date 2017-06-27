using System;
using System.IO;
using System.Reflection;

using static Utils;

static class TestCompile {
	static readonly Path testRootDir = Path.from("tests");

	internal static void runAllCompilerTests() {
		foreach (var m in methods())
			foo(m);
		//runCompilerTests(rootDir);
	}

	static Arr<(MethodInfo, TestFor)> methods() =>
		new Arr<MethodInfo>(typeof(Tests).GetMethods(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
		.mapDefined(method => {
			assert(method.IsStatic);
			var testForAttribute = method.GetCustomAttribute(typeof(TestFor));
			if (testForAttribute == null)
				return Op<(MethodInfo, TestFor)>.None;
			var testFor = (TestFor)testForAttribute;
			return Op.Some((method, testFor));
		});

	internal static void runSingle(string name) {
		if (!methods().find(out var found, mtf => mtf.Item2.testPath.ToString() == name))
			throw new Exception($"No such test {name}");

		foo(found);
	}

	static void foo((MethodInfo, TestFor) m) {
		var (method, testFor) = m;
		var testData = runCompilerTest(Path.from(testFor.testPath));
		method.Invoke(null, new object[] { testData });
	}

	static TestData runCompilerTest(Path testPath) {
		var rootDir = testRootDir.resolve(testPath.asRel);
		var host = DocumentProviders.CommandLine(rootDir);
		var (program, m) = Compiler.compile(Path.empty, host, Op<CompiledProgram>.None);

		foreach (var pair in program.modules) {
			var module = pair.Value;
			var path = module.fullPath().removeExtension(ModuleResolver.extension);

			// TODO: break out if there was an error
			assertSomething(rootDir, path, ".ast", Dat.either(module.document.parseResult));

			assertSomething(rootDir, path, ".model", module.klass.toDat());

			assertSomething(rootDir, path, ".js", JsEmitter.emitToString(module));

			//module.imports; We'll just not test this then...
		}

		return new TestData(program, m, rootDir.add("index.js"));

		//var x = CsonWriter.write(program);
		//var moduleAst = new Ast.Module()
		//var document = new DocumentInfo("static\n\nfun Void foo()\n\tpass\n", 1, Either<Module, CompileError>.Left(moduleAst))
		//var moduleA = new Module(Path.from("A"), isIndex: false, document: document, klass: klass);
		//var expected = new CompiledProgram(host, Dict.of(Sym.of("A"), moduleA));

		//Console.WriteLine(x);
	}

	static void assertSomething(Path rootDir, Path path, string extension, Dat actualDat) =>
		assertSomething(rootDir, path, extension, CsonWriter.write(actualDat) + "\n");

	static void assertSomething(Path rootDir, Path path, string extension, string actual) {
		var fullPath = rootDir.resolve(path.asRel).addExtension(extension).ToString();

		// If it doesn't exist, create it.
		string expected;
		try {
			expected = File.ReadAllText(fullPath);
		} catch (FileNotFoundException) {
			// Write the new result.
			File.WriteAllText(fullPath, actual);
			return;
		}

		if (actual == expected) {
			// Great!
			return;
		}

		Console.WriteLine("Unexpected output!");
		Console.WriteLine($"Expected: {expected}");
		Console.WriteLine($"Actual: {actual}");
		//TODO: put under --accept option
		File.WriteAllText(fullPath, actual);

		return;
	}

	static TestCompile() {
		AppDomain.CurrentDomain.FirstChanceException += handleFirstChanceException;
	}

	static void handleFirstChanceException(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e) {
		//https://stackoverflow.com/questions/15833498/how-to-not-breaking-on-an-exception
		if (!(e.Exception is System.Reflection.TargetInvocationException)) {
			System.Diagnostics.Debugger.Break();
		}
	}
}

[AttributeUsage(AttributeTargets.Method)]
sealed class TestFor : Attribute {
	internal readonly string testPath;
	internal TestFor(string testRootDir) { this.testPath = testRootDir; }
}

sealed class TestData {
	internal readonly CompiledProgram compiledProgram;
	internal readonly Model.Module rootModule;
	internal readonly Path indexJs;
	internal readonly ILEmitter emitter; // Will always be emitted before running custom test.
	internal readonly Type emittedRoot;

	internal TestData(CompiledProgram compiledProgram, Model.Module rootModule, Path indexJs) {
		this.compiledProgram = compiledProgram;
		this.rootModule = rootModule;
		this.indexJs = indexJs;
		this.emitter = new ILEmitter();
		this.emittedRoot = emitter.emitModule(rootModule);
	}
}
