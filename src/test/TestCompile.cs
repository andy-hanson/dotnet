using System;
using System.IO;
using System.Reflection;

using Module = Model.Module;
using static Utils;

static class TestCompile {
	static readonly Path testRootDir = Path.from("tests");

	internal static void runAllCompilerTests() {
		var methods = typeof(Tests).GetMethods(
			BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
		foreach (var method in methods) {
			assert(method.IsStatic);
			var testForAttribute = method.GetCustomAttribute(typeof(TestFor));
			if (testForAttribute == null)
				continue;
			var testFor = (TestFor) testForAttribute;

			var testData = runCompilerTest(Path.from(testFor.testPath));
			method.Invoke(null, new object[] { testData });
		}


		//runCompilerTests(rootDir);
	}

	internal static TestData runCompilerTest(Path testPath) {
		var rootDir = testRootDir.resolve(testPath.asRel);
		var host = DocumentProviders.CommandLine(rootDir);
		var (program, m) = Compiler.compile(Path.empty, host, Op<CompiledProgram>.None);

		foreach (var pair in program.modules) {
			var module = pair.Value;
			var path = module.fullPath().removeExtension(ModuleResolver.extension);
			assertSomething(rootDir, path, ".ast", Dat.either(module.document.parseResult));
			assertSomething(rootDir, path, ".model", module.klass.toDat());
			assertSomething(rootDir, path, ".js", JsEmitter.emitToString(module));
			//module.imports; We'll just not test this then...
		}

		return new TestData(program, m);

		//var x = CsonWriter.write(program);
		//var moduleAst = new Ast.Module()
		//var document = new DocumentInfo("static\n\nfun Void foo()\n\tpass\n", 1, Either<Module, CompileError>.Left(moduleAst))
		//var moduleA = new Module(Path.from("A"), isMain: false, document: document, klass: klass);
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
	}

	//mv
	static void TestCompiler(Path path) {
		// We will include output in the directory.
	}
}


[AttributeUsage(AttributeTargets.Method)]
sealed class TestFor : Attribute {
	internal readonly string testPath;
	internal TestFor(string testRootDir) { this.testPath = testRootDir; }
}

static class Tests {
	[TestFor("1")]
	static void T1(TestData t) {
		t.emittedRoot.GetMethod("foo").Invoke(null, new object[] {});
	}
}


//Code side of a test
sealed class TestData {
	internal readonly CompiledProgram compiledProgram;
	internal readonly Module rootModule;
	internal readonly ILEmitter emitter; // Will always be emitted before running custom test.
	internal readonly Type emittedRoot;

	internal TestData(CompiledProgram compiledProgram, Module rootModule) {
		this.compiledProgram = compiledProgram;
		this.rootModule = rootModule;
		this.emitter = new ILEmitter();
		this.emittedRoot = emitter.emitModule(rootModule);
	}
}
