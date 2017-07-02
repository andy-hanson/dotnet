using System;
using System.Reflection;

using static FileUtils;
using static Utils;

sealed class TestFailureException : Exception {
	internal TestFailureException(string message) : base(message) {}
}

class TestCompile {
	const string testsDir = "tests";
	static readonly Path casesRootDir = Path.fromParts(testsDir, "cases");
	static readonly Path baselinesRootDir = Path.fromParts(testsDir, "baselines");

	readonly bool updateBaselines;
	internal TestCompile(bool updateBaselines) {
		this.updateBaselines = updateBaselines;
	}

	internal void runTestNamed(string name) {
		if (!getTestMethods().get(name, out var method))
			throw TODO($"No test method for {name}");
		runSingle(name, method);
	}

	internal void runAllCompilerTests() {
		var allTests = listDirectoriesInDirectory(casesRootDir).toArr();
		checkNoExtraBaselines(allTests);
		var methods = getTestMethods();

		foreach (var test in allTests) {
			var method = methods[test];
			runSingle(test, method);
		}
	}

	void checkNoExtraBaselines(Arr<string> allTests) {
		var allBaselines = listDirectoriesInDirectory(baselinesRootDir).toArr();
		if (allBaselines.length == allTests.length) return;

		var extraBaselines = Set.setDifference(allBaselines, allTests.toSet());

		throw new TestFailureException($"Baselines have no associated tests: {string.Join(", ", extraBaselines)}");
	}

	static Dict<string, MethodInfo> getTestMethods() {
		var flags = BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
		return new Arr<MethodInfo>(typeof(Tests).GetMethods(flags)).mapDefinedToDict(method => {
			assert(method.IsStatic);
			var testForAttribute = method.GetCustomAttribute(typeof(TestForAttribute));
			if (testForAttribute == null)
				return Op<(string, MethodInfo)>.None;
			var testFor = (TestForAttribute)testForAttribute;
			return Op.Some((testFor.testPath, method));
		});
	}

	void runSingle(string testName, MethodInfo method) {
		var testData = runCompilerTest(Path.fromParts(testName));
		method.Invoke(null, new object[] { testData });
	}

	TestData runCompilerTest(Path testPath) {
		var testDirectory = casesRootDir.resolve(testPath.asRel);
		var host = DocumentProviders.CommandLine(testDirectory);
		var (program, indexModule) = Compiler.compile(Path.empty, host, Op<CompiledProgram>.None);
		var baselinesDirectory = baselinesRootDir.resolve(testPath.asRel);

		var emitter = new ILEmitter(shouldLog: true);
		var emittedRoot = emitter.emitModule(indexModule);

		foreach (var (_, module) in program.modules) {
			var modulePath = module.fullPath().withoutExtension(ModuleResolver.extension);

			var logs = emitter.getLogs(module);

			assertBaseline(baselinesDirectory, modulePath, ".ast", Dat.either(module.document.parseResult));
			assertBaseline(baselinesDirectory, modulePath, ".model", module.klass.toDat());
			assertBaseline(baselinesDirectory, modulePath, ".js", JsEmitter.emitToString(module));
			assertBaseline(baselinesDirectory, modulePath, ".il", emitter.getLogs(module));
		}

		return new TestData(program, indexModule, baselinesDirectory.add("index.js"), emittedRoot);
	}

	void assertBaseline(Path testDirectory, Path modulePath, string extension, Dat actualDat) =>
		assertBaseline(testDirectory, modulePath, extension, CsonWriter.write(actualDat) + "\n");

	void assertBaseline(Path testDirectory, Path modulePath, string extension, string actual) {
		var fullModulePath = testDirectory.resolve(modulePath.asRel);
		var baselineDirectory = fullModulePath.directory().toPathString();
		var baselinePath = fullModulePath.addExtension(extension);

		// If it doesn't exist, create it.
		if (!fileExists(baselinePath)) {
			writeFileAndEnsureDirectory(baselinePath, actual);
			return;
		}

		var expected = readFile(baselinePath);
		if (actual == expected)
			return;

		if (updateBaselines) {
			writeFile(baselinePath, actual);
		} else {
			throw new TestFailureException($"Unexpected output!\nExpected: {expected}\nActual: {actual}");
		}
	}
}

[AttributeUsage(AttributeTargets.Method)]
sealed class TestForAttribute : Attribute {
	internal readonly string testPath;
	internal TestForAttribute(string testRootDir) { this.testPath = testRootDir; }
}

sealed class TestData {
	internal readonly CompiledProgram compiledProgram;
	internal readonly Model.Module rootModule;
	internal readonly Path indexJs;
	internal readonly Type emittedRoot;

	internal TestData(CompiledProgram compiledProgram, Model.Module rootModule, Path indexJs, Type emittedRoot) {
		this.compiledProgram = compiledProgram;
		this.rootModule = rootModule;
		this.indexJs = indexJs;
		this.emittedRoot = emittedRoot;
	}
}
