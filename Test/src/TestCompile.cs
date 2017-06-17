using System;
using System.IO;

using static Utils;

//using Model;

class TestCompile {
	[Test] static void s() {
		var rootDir = Path.from("tests", "1");
		var host = DocumentProviders.CommandLine(rootDir);
		var (program, m) = Compiler.compile(Path.from("A"), host, Op<CompiledProgram>.None);

		foreach (var pair in program.modules) {
			var path = pair.Key;
			var module = pair.Value;
			assert(module.logicalPath.Equals(path));
			assertSomething(rootDir, path, ".ast", Dat.either(module.document.parseResult));
			assertSomething(rootDir, path, ".model", module.klass.toDat());
			//module.imports; We'll just not test this then...
		}

		//var x = CsonWriter.write(program);
		//var moduleAst = new Ast.Module()
		//var document = new DocumentInfo("static\n\nfun Void foo()\n\tpass\n", 1, Either<Module, CompileError>.Left(moduleAst))
		//var moduleA = new Module(Path.from("A"), isMain: false, document: document, klass: klass);
		//var expected = new CompiledProgram(host, Dict.of(Sym.of("A"), moduleA));

		//Console.WriteLine(x);
	}

	static void assertSomething(Path rootDir, Path path, string extension, Dat actualDat) {
		var actual = CsonWriter.write(actualDat) + "\n";
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
