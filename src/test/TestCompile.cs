using System;
using System.Linq;
using System.Reflection;

using static FileUtils;
using static Utils;

namespace Test {
	sealed class TestFailureException : Exception {
		internal TestFailureException(string message) : base(message) {}
	}

	class TestCompile : IDisposable {
		const string testsDir = "tests";
		static readonly Path casesRootDir = Path.fromParts(testsDir, "cases");
		static readonly Path baselinesRootDir = Path.fromParts(testsDir, "baselines");

		readonly bool updateBaselines;
		readonly JsTestRunner jsTestRunner;

		internal TestCompile(bool updateBaselines) {
			this.updateBaselines = updateBaselines;
			jsTestRunner = JsTestRunner.create();
		}

		void IDisposable.Dispose() {
			IDisposable i = jsTestRunner;
			i.Dispose();
			GC.SuppressFinalize(this);
		}

		internal void runTestNamed(string name) {
			if (!getTestMethods().get(name, out var method))
				throw TODO($"No test method for {name}");
			runSingle(name, method);
		}

		internal void runAllTests() {
			testNzlib();
			runAllCompilerTests();
		}

		void testNzlib() {
			var nzlibDataJson = Json.JsonWriter.write(JsBuiltins.nzlibData());
			var res = Test.SynchronousProcess.run(Path.fromParts("tests", "js-test-runner", "testNzlib.js"), nzlibDataJson);
			if (res != "OK\n")
				throw new Test.TestFailureException(res);
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

		static void checkNoExtraBaselines(Arr<string> allTests) {
			var allBaselines = listDirectoriesInDirectory(baselinesRootDir).toArr();
			if (allBaselines.length == allTests.length) return;

			var extraBaselines = Set.setDifference(allBaselines, allTests.toSet());
			if (extraBaselines.Any())
				throw new TestFailureException($"Baselines have no associated tests: {string.Join(", ", extraBaselines)}");
		}

		static Dict<string, MethodInfo> getTestMethods() {
			const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
			return new Arr<MethodInfo>(typeof(Tests).GetMethods(flags)).mapDefinedToDict(method => {
				assert(method.IsStatic);
				var testFor = method.GetCustomAttribute<TestForAttribute>();
				return testFor != null ? Op.Some((testFor.testPath, method)) : Op<(string, MethodInfo)>.None;
			});
		}

		void runSingle(string testName, MethodInfo method) {
			var testData = runCompilerTest(Path.fromParts(testName));
			method.Invoke(null, new object[] { testData });
		}

		TestData runCompilerTest(Path testPath) {
			var testDirectory = casesRootDir.resolve(testPath.asRel);
			var (program, indexModule) = Compiler.compileDir(testDirectory);
			var baselinesDirectory = baselinesRootDir.resolve(testPath.asRel);

			var (emittedRoot, emitLogs) = ILEmitter.emitWithLogs(indexModule);

			foreach (var (_, module) in program.modules) {
				var modulePath = module.fullPath().withoutExtension(ModuleResolver.extension);

				assertBaseline(baselinesDirectory, modulePath, ".ast", Dat.either(module.document.parseResult));
				assertBaseline(baselinesDirectory, modulePath, ".model", module.klass.toDat());
				assertBaseline(baselinesDirectory, modulePath, ".js", JsEmitter.emitToString(module));
				assertBaseline(baselinesDirectory, modulePath, ".il", emitLogs[module]);
			}

			return new TestData(testPath, program, indexModule, emittedRoot, jsTestRunner);
		}

		void assertBaseline(Path testDirectory, Path modulePath, string extension, Dat actualDat) =>
			assertBaseline(testDirectory, modulePath, extension, CsonWriter.write(actualDat) + "\n");

		void assertBaseline(Path testDirectory, Path modulePath, string extension, string actual) {
			var fullModulePath = testDirectory.resolve(modulePath.asRel);
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
		internal readonly Path testPath;
		internal readonly CompiledProgram compiledProgram;
		internal readonly Model.Module rootModule;
		internal readonly Type emittedRoot;
		internal readonly JsTestRunner jsTestRunner;

		internal TestData(Path testPath, CompiledProgram compiledProgram, Model.Module rootModule, Type emittedRoot, JsTestRunner jsTestRunner) {
			this.testPath = testPath;
			this.compiledProgram = compiledProgram;
			this.rootModule = rootModule;
			this.emittedRoot = emittedRoot;
			this.jsTestRunner = jsTestRunner;
		}
	}
}
