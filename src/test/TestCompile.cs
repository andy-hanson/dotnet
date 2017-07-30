using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using static FileUtils;
using static Utils;

namespace Test {
	sealed class TestFailureException : Exception {
		internal TestFailureException(string message) : base(message) {}
		internal static TestFailureException create(Action<StringMaker> message) {
			var s = StringMaker.create();
			message(s);
			return new TestFailureException(s.finish());
		}
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
				throw TODO("No test method for " + name);
			runSingle(name, method);
		}

		internal void runAllTests() {
			testNzlib();
			runAllCompilerTests();
		}

		static void testNzlib() {
			var nzlibDataJson = Json.JsonWriter.write(JsBuiltins.nzlibData());
			var res = Test.SynchronousProcess.run(Path.fromParts("tests", "js-test-runner", "testNzlib.js"), nzlibDataJson);
			if (res != "OK\n")
				throw new Test.TestFailureException(res);
		}

		internal void runAllCompilerTests() {
			var allTests = listDirectoriesInDirectory(casesRootDir).toArr();
			checkNoExtraBaselineDirectories(allTests);
			var methods = getTestMethods();

			foreach (var test in allTests) {
				var method = methods[test];
				runSingle(test, method);
			}
		}

		// Checking of baseline *files* is done in `runCompilerTest`.
		static void checkNoExtraBaselineDirectories(Arr<string> allTests) {
			var allBaselines = listDirectoriesInDirectory(baselinesRootDir).toArr();
			if (allBaselines.length == allTests.length) return;

			var extraBaselines = Set.setDifference(allBaselines, allTests.toSet());
			if (extraBaselines.Any())
				throw TestFailureException.create(s => s.add("Baselines have no associated tests: ").join(extraBaselines));
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
			method.Invoke(null, new object[] { testData.force }); //TODO!!!
		}

		static bool anyDiagnostics(ModuleOrFail mf) {
			switch (mf) {
				case FailModule _:
					return true;
				case Model.Module m:
					return anyDiagnostics(m);
				default:
					throw unreachable();
			}
		}

		static bool anyDiagnostics(Model.Module module) =>
			module.diagnostics.any || module.imports.some(import => {
				switch (import) {
					case Model.BuiltinClass b:
						return false;
					case Model.Module m:
						return anyDiagnostics(m);
					default:
						throw unreachable();
				}
			});

		Op<TestData> runCompilerTest(Path testPath) {
			var testDirectory = casesRootDir.resolve(testPath.asRel);

			var documentProvider = new TestDocumentProvider(testDirectory);
			var opCompileResult = Compiler.compile(Path.empty, documentProvider);
			if (!opCompileResult.get(out var compileResult))
				throw new TestFailureException(StringMaker.create().add("Test ").add(testPath).add(" must contain 'index.nz'").finish());

			var (program, indexModuleOrFail) = compileResult;
			var baselinesDirectory = baselinesRootDir.resolve(testPath.asRel);

			var expectedDiagnostics = documentProvider.getDiagnostics();

			var baselines = readFilesInDirectoryRecursive(baselinesDirectory).toMutableDict();

			Op<TestData> res;
			switch (indexModuleOrFail) {
				case FailModule f:
					testWithDiagnostics(baselinesDirectory, program, baselines, expectedDiagnostics);
					res = Op<TestData>.None;
					break;
				case Model.Module m:
					if (anyDiagnostics(m)) {
						testWithDiagnostics(baselinesDirectory, program, baselines, expectedDiagnostics);
						res = Op<TestData>.None;
					} else
						res = Op.Some(testWithNoDiagnostics(testPath, baselinesDirectory, program, m, baselines));
					break;
				default:
					throw unreachable();
			}

			if (baselines.any())
				throw TestFailureException.create(s =>
					s.add("Extra baselines: ").join(baselines.Keys.Select(k => testPath.resolve(k.asRel))));

			return res;
		}

		void testWithDiagnostics(
			Path baselinesDirectory,
			CompiledProgram program,
			Dictionary<Path, string> baselines,
			Dictionary<Path, Arr<(LineAndColumnLoc, string)>> expectedDiagnosticsByPath) {
			foreach (var moduleOrFail in program.modules.values) {
				var modulePath = moduleOrFail.fullPath().withoutExtension(ModuleResolver.extension);
				assertBaseline(baselinesDirectory, modulePath, ".ast", Dat.either(moduleOrFail.document.parseResult), baselines, updateBaselines);

				// Can only do a '.model' baseline if it's a Module
				if (moduleOrFail is Model.Module module) {
					assertBaseline(baselinesDirectory, modulePath, ".model", module.klass.toDat(), baselines, updateBaselines);
				}

				var diags = moduleOrFail.diagnostics;
				if (expectedDiagnosticsByPath.tryDelete(modulePath, out var expectedDiagnostics)) {
					throw TODO(); //TODO!!! Need to compare actual w/ expected diagnostics.
				} else if (diags.any) {
					throw TODO(); //TODO!!! Did not expect these diagnostics
				}
			}

			assert(expectedDiagnosticsByPath.Count == 0); // Shouldn't have any entries for modules that dont' actually exist.
		}

		TestData testWithNoDiagnostics(Path testPath, Path baselinesDirectory, CompiledProgram program, Model.Module indexModule, Dictionary<Path, string> baselines) {
			// Program compiled with no diagnostics, so safe to emit.
			var (emittedRoot, emitLogs) = ILEmitter.emitWithLogs(indexModule);

			foreach (var moduleOrFail in program.modules.values) {
				var module = (Model.Module)moduleOrFail; // Can't be a FailModule or we would call testWithDiagnostics
				var modulePath = module.fullPath().withoutExtension(ModuleResolver.extension);

				assertBaseline(baselinesDirectory, modulePath, ".ast", Dat.either(module.document.parseResult), baselines, updateBaselines);
				assertBaseline(baselinesDirectory, modulePath, ".model", module.klass.toDat(), baselines, updateBaselines);
				assertBaseline(baselinesDirectory, modulePath, ".js", JsEmitter.emitToString(module), baselines, updateBaselines);
				assertBaseline(baselinesDirectory, modulePath, ".il", emitLogs[module], baselines, updateBaselines);
			}

			return new TestData(testPath, program, indexModule, emittedRoot, jsTestRunner);
		}

		static void assertBaseline(Path testDirectory, Path modulePath, string extension, Dat actualDat, Dictionary<Path, string> baselines, bool updateBaselines) =>
			assertBaseline(testDirectory, modulePath, extension, CsonWriter.write(actualDat) + "\n", baselines, updateBaselines);

		static void assertBaseline(Path testDirectory, Path modulePath, string extension, string actual, Dictionary<Path, string> baselines, bool updateBaselines) {
			var baselinePath = modulePath.addExtension(extension);

			var fullBaselinePath = testDirectory.resolve(baselinePath.asRel); //TODO: compute lazily

			if (!baselines.tryDelete(baselinePath, out var oldBaseline)) {
				// This baseline didn't exist before.
				if (updateBaselines) {
					writeFileAndEnsureDirectory(fullBaselinePath, actual);
				} else {
					throw TestFailureException.create(s => s.add("No such baseline ").add(fullBaselinePath));
				}
			} else if (actual != oldBaseline) {
				if (updateBaselines)
					writeFile(fullBaselinePath, actual);
				else
					throw TestFailureException.create(s => s.add("Unexpected output!\nExpected: ").add(oldBaseline).add("\nActual: ").add(actual));
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
