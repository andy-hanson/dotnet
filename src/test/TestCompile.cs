using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Diag;
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

		internal void runTestNamed(string testName) =>
			runSingle(testName, getTestMethods());

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
			foreach (var test in allTests)
				runSingle(test, methods);
		}

		// Checking of baseline *files* is done in `runCompilerTest`.
		static void checkNoExtraBaselineDirectories(Arr<string> allTests) {
			var allBaselines = listDirectoriesInDirectory(baselinesRootDir).toArr();
			if (allBaselines.length == allTests.length) return;

			var extraBaselines = Set.setDifference(allBaselines, allTests.toSet());
			if (extraBaselines.Any())
				throw TestFailureException.create(s => s.add("Baselines have no associated tests: ").join(extraBaselines));
		}

		const BindingFlags testMethodFlags =
			BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
		static Dict<string, MethodInfo> getTestMethods() =>
			new Arr<MethodInfo>(typeof(Tests).GetMethods(testMethodFlags)).mapDefinedToDict(method => {
				assert(method.IsStatic);
				var testFor = method.GetCustomAttribute<TestForAttribute>();
				return testFor != null ? Op.Some((testFor.testPath, method)) : Op<(string, MethodInfo)>.None;
			});

		void runSingle(string testName, Dict<string, MethodInfo> methods) {
			var method = methods.getOp(testName);
			var testData = runCompilerTest(Path.fromParts(testName));
			if (method.get(out var m))
				m.Invoke(null, new object[] { testData.force }); // Custom tests should only run on non-error tests.
			else if (testData.get(out var td))
				TestUtils.runCsJsTests(td);
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

			var expectedDiagnostics = documentProvider.getExpectedDiagnostics();

			var expectedBaselines = readFilesInDirectoryRecursiveIfExists(baselinesDirectory).toMutableDict();

			Op<TestData> res;
			switch (indexModuleOrFail) {
				case FailModule f:
					testWithDiagnostics(baselinesDirectory, program, expectedBaselines, expectedDiagnostics);
					res = Op<TestData>.None;
					break;
				case Model.Module m:
					if (anyDiagnostics(m)) {
						testWithDiagnostics(baselinesDirectory, program, expectedBaselines, expectedDiagnostics);
						res = Op<TestData>.None;
					} else {
						if (expectedDiagnostics.any())
							throw new TestFailureException(StringMaker.create()
								.add("Compiled without diagnostics, but expected diagnostics in: ")
								.join(expectedDiagnostics.Keys)
								.finish());
						res = Op.Some(testWithNoDiagnostics(testPath, baselinesDirectory, program, m, expectedBaselines));
					}
					break;
				default:
					throw unreachable();
			}

			if (expectedBaselines.any())
				throw TestFailureException.create(s =>
					s.add("Extra baselines: ").join(expectedBaselines.Keys.Select(k => testPath.resolve(k.asRel))));

			return res;
		}

		void testWithDiagnostics(
			Path baselinesDirectory,
			CompiledProgram program,
			Dictionary<Path, string> expectedBaselines,
			Dictionary<Path, Arr<(LineAndColumnLoc, string)>> expectedDiagnosticsByPath) {
			foreach (var moduleOrFail in program.modules.values) {
				var moduleFullPath = moduleOrFail.fullPath();
				var modulePath = moduleFullPath.withoutExtension(ModuleResolver.extension);
				var document = moduleOrFail.document;

				if (document.parseResult.isLeft)
					assertBaseline(baselinesDirectory, modulePath, ".ast", document.parseResult.left.toDat(), expectedBaselines, updateBaselines);
				// If document.parseResult.isRight, that will be the module's diagnostics.

				// Can only do a '.model' baseline if it's a Module
				if (moduleOrFail is Model.Module module) {
					assertBaseline(baselinesDirectory, modulePath, ".model", module.klass.toDat(), expectedBaselines, updateBaselines);
				}

				var text = document.text;
				var actualDiagnostics = moduleOrFail.diagnostics;
				var diagnosticsOK = expectedDiagnosticsByPath.tryDelete(moduleFullPath, out var expectedDiagnostics)
					// Expected some diagnostics, see if actual diagnostics match.
					? diagnosticsMatch(text, actualDiagnostics, expectedDiagnostics)
					// Did not expect any diagnostics.
					: !actualDiagnostics.any;
				if (!diagnosticsOK)
					throw showDiagnosticsMismatchError(moduleFullPath, text, actualDiagnostics);
			}

			assert(expectedDiagnosticsByPath.Count == 0); // Shouldn't have any entries for modules that don't actually exist.
		}

		static TestFailureException showDiagnosticsMismatchError(Path moduleFullpath, string text, Arr<Diagnostic> actualDiagnostics) {
			var s = StringMaker.create();
			s.add("Unexpected diagnostics for ").add(moduleFullpath).add(':').nl();
			Foo.generateExpectedDiagnosticsText(text, s, actualDiagnostics);
			return new TestFailureException(s.finish());
		}

		//mv
		static bool diagnosticsMatch(string moduleText, Arr<Diagnostic> actual, Arr<(LineAndColumnLoc loc, string diag)> expected) {
			var lc = new LineColumnGetter(moduleText);
			if (actual.length != expected.length)
				return false;

			var sortedActual = actual.sort(compareBy<Diagnostic>(a => a.loc.start.index));
			assert(expected.isSorted(compareBy<(LineAndColumnLoc loc, string)>(a => a.loc.start.line, a => a.loc.start.column)));

			return sortedActual.eachCorresponds(expected, (a, e) =>
				lc.lineAndColumnAtLoc(a.loc).deepEqual(e.loc) && StringMaker.stringify(a.data) == e.diag);
		}

		TestData testWithNoDiagnostics(Path testPath, Path baselinesDirectory, CompiledProgram program, Model.Module indexModule, Dictionary<Path, string> expectedBaselines) {
			// Program compiled with no diagnostics, so safe to emit.
			var (emittedRoot, emitLogs) = ILEmitter.emitWithLogs(indexModule);

			foreach (var moduleOrFail in program.modules.values) {
				var module = (Model.Module)moduleOrFail; // Can't be a FailModule or we would call testWithDiagnostics
				var modulePath = module.fullPath().withoutExtension(ModuleResolver.extension);

				assertBaseline(baselinesDirectory, modulePath, ".ast", Dat.either(module.document.parseResult), expectedBaselines, updateBaselines);
				assertBaseline(baselinesDirectory, modulePath, ".model", module.klass.toDat(), expectedBaselines, updateBaselines);
				assertBaseline(baselinesDirectory, modulePath, ".js", JsEmitter.emitToString(module), expectedBaselines, updateBaselines);
				assertBaseline(baselinesDirectory, modulePath, ".il", emitLogs[module], expectedBaselines, updateBaselines);
			}

			return new TestData(testPath, program, indexModule, emittedRoot, jsTestRunner);
		}

		static void assertBaseline(Path testDirectory, Path modulePath, string extension, Dat actualDat, Dictionary<Path, string> baselines, bool updateBaselines) =>
			assertBaseline(testDirectory, modulePath, extension, CsonWriter.write(actualDat) + "\n", baselines, updateBaselines);

		static void assertBaseline(Path testDirectory, Path modulePath, string extension, string actual, Dictionary<Path, string> baselines, bool updateBaselines) {
			var baselinePath = modulePath.addExtension(extension);

			if (!baselines.tryDelete(baselinePath, out var oldBaseline)) {
				var fbp = fullBaselinePath(testDirectory, baselinePath);
				// This baseline didn't exist before.
				if (updateBaselines)
					writeFileAndEnsureDirectory(fbp, actual);
				else
					throw TestFailureException.create(s => s.add("No such baseline ").add(fbp));
			} else if (actual != oldBaseline) {
				if (updateBaselines)
					writeFile(fullBaselinePath(testDirectory, baselinePath), actual);
				else
					throw TestFailureException.create(s => s.add("Unexpected output!\nExpected: ").add(oldBaseline).add("\nActual: ").add(actual));
			}
		}

		static Path fullBaselinePath(Path testDirectory, Path baselinePath) =>
			testDirectory.resolve(baselinePath.asRel);
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

//mv
static class Foo {
	// Adds diagnostics with `~`
	internal static string generateExpectedDiagnosticsText(string text, StringMaker s, Arr<Diag.Diagnostic> actualDiagnostics) {
		assert(actualDiagnostics.any);

		var lineToDiagnostics = getLineToDiagnostics(text, actualDiagnostics);
		uint countShownDiagnostics = 0;

		uint lineNumber = 0;
		for (uint textIndex = 0; ; textIndex++) {
			var ch = text.at(textIndex);
			s.add(ch);
			if (ch == '\n' || textIndex == text.Length - 1) {
				if (lineToDiagnostics.get(lineNumber, out var diags)) {
					foreach (var (startCol, endCol, diag) in diags) {
						countShownDiagnostics++;
						showDiagnostic(s, startCol, endCol, diag);
					}
				}

				if (textIndex == text.Length - 1)
					break;
				lineNumber++;
			}
		}

		assert(countShownDiagnostics == actualDiagnostics.length);
		return s.finish();
	}

	static Dict<uint, Arr<(uint colStart, uint colEnd, DiagnosticData)>> getLineToDiagnostics(string text, Arr<Diag.Diagnostic> actualDiagnostics) {
		var lc = new LineColumnGetter(text);
		var lineToDiagnostics = Dict.builder<uint, Arr.Builder<(uint colStart, uint colEnd, DiagnosticData)>>();
		foreach (var diag in actualDiagnostics) {
			var (loc, data) = diag;
			var ((startLine, startColumn), (endLine, endColumn)) = lc.lineAndColumnAtLoc(loc);
			assert(startLine == endLine, "Diagnostics should take a single line.");
			lineToDiagnostics.multiMapAdd(startLine, (startColumn, endColumn, data));
		}
		return lineToDiagnostics.mapValues(v => v.finish());
	}

	static void showDiagnostic(StringMaker s, uint startColumn, uint endColumn, DiagnosticData diag) {
		s.nl();
		uint i = 0;
		for (; i < startColumn; i++)
			s.add(' ');
		for (; i < endColumn; i++)
			s.add('~');
		new IndentedShower<StringMaker>(s, "! ").nl().add(diag);
	}
}
