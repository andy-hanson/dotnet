using System;

using Model;
using static Utils;

/**
Stores the state of a previously-compiled program.
This is 100% immutable.
*/
struct CompiledProgram : ToData<CompiledProgram> {
	[NotData] internal readonly DocumentProvider documentProvider;
	internal readonly Dict<Path, Module> modules;

	internal CompiledProgram(DocumentProvider documentProvider, Dict<Path, Module> modules) {
		this.documentProvider = documentProvider;
		this.modules = modules;
	}

	public bool deepEqual(CompiledProgram c) =>
		object.ReferenceEquals(documentProvider, c.documentProvider) &&
		modules.deepEqual(c.modules);
	public Dat toDat() =>
		Dat.of(this, nameof(modules), Dat.dict(modules.mapKeys(p => p.toPathString())));
}

/*
Design notes:

We assume that there may be file system events that we don't know about.
Therefore, we will always ask the CompileHost for the latest versions of all files.
The CompilerHost may implement caching. (In a command-line scenario this should not be necessary.)
Whether a document may be reused is indicated by its version vs the version the compiler used.
*/
sealed class Compiler {
	readonly DocumentProvider documentProvider;
	readonly Op<CompiledProgram> oldProgram;
	// Keys are logical paths.
	readonly Dict.Builder<Path, ModuleState> modules = Dict.builder<Path, ModuleState>();

	Compiler(DocumentProvider documentProvider, Op<CompiledProgram> oldProgram) { this.documentProvider = documentProvider; this.oldProgram = oldProgram; }

	internal static (CompiledProgram, Module root) compile(Path path, DocumentProvider documentProvider, Op<CompiledProgram> oldProgram) {
		if (oldProgram.get(out var old))
			assert(old.documentProvider == documentProvider);
		var c = new Compiler(documentProvider, oldProgram);
		var rootModule = c.compileSingle(Op<PathLoc>.None, path).Item1;
		var newProgram = new CompiledProgram(documentProvider, c.modules.mapValues(o => o.module));
		return (newProgram, rootModule);
	}

	internal static (CompiledProgram, Module root) compile(Path path, DocumentProvider documentProvider) =>
		compile(path, documentProvider, Op<CompiledProgram>.None);

	internal static (CompiledProgram, Module root) compileDir(Path dir) =>
		compile(Path.empty, DocumentProviders.CommandLine(dir));

	internal static (CompiledProgram, Module root) compileFile(Path path) {
		var fileName = path.last.withoutEndIfEndsWith(".nz");
		var filePath = fileName == "index" ? Path.empty : Path.fromParts(fileName);
		return compile(filePath, DocumentProviders.CommandLine(path.directory()));
	}

	/**
	isReused: true if this is the same module from the old program.
	*/
	(Module, bool isReused) compileSingle(Op<PathLoc> from, Path logicalPath) {
		if (modules.get(logicalPath, out var alreadyCompiled)) {
			switch (alreadyCompiled.kind) {
				case ModuleState.Kind.Compiling:
					//TODO: attach an error to the Module.
					//raiseWithPath(logicalPath, fromLoc ?? Loc.zero, Err.CircularDependency(logicalPath));
					throw TODO($"Circular dependency around {logicalPath}");

				case ModuleState.Kind.CompiledFresh:
				case ModuleState.Kind.CompiledReused:
					// Already compiled in the new program.
					// This can happen if the same module is a dependency of two other modules.
					return (alreadyCompiled.module, isReused: alreadyCompiled.kind == ModuleState.Kind.CompiledReused);

				default:
					throw unreachable();
			}
		} else {
			//Must make a mark in modules *before* compiling dependencies!
			modules.add(logicalPath, ModuleState.compiling);
			var (module, isReused) = doCompileSingle(from, logicalPath);
			modules.change(logicalPath, ModuleState.compiled(module, isReused));
			return (module, isReused);
		}
	}

	(Module, bool isReused) doCompileSingle(Op<PathLoc> from, Path logicalPath) {
		if (!ModuleResolver.getDocumentFromLogicalPath(documentProvider, logicalPath, out var fullPath, out var isIndex, out var documentInfo)) {
			unused(from); //Use this to report errors
			throw TODO(); //TODO: return Either<Module, CompileError> ?
		}

		if (documentInfo.parseResult.isRight) {
			Console.WriteLine(Test.CsonWriter.write(documentInfo.parseResult.right));
			throw TODO("Bad Parse");
		}

		var ast = documentInfo.parseResult.force(); // TODO: don't force

		var allDependenciesReused = true;
		var imports = ast.imports.map<Imported>(import => {
			var imported = importPath(fullPath, import);
			if (imported.isLeft) {
				return imported.left;
			} else {
				var (importedModule, isImportReused) = compileSingle(Op.Some(new PathLoc(fullPath, import.loc)), imported.right);
				allDependenciesReused = allDependenciesReused && isImportReused;
				return importedModule;
			}
		});

		// We will only bother looking at the old module if all of our dependencies were safely reused.
		// If oldModule doesn't exactly match, we'll ignore it completely.
		if (allDependenciesReused && oldProgram.get(out var op) && op.modules.get(logicalPath, out var oldModule)) {
			return (oldModule, isReused: true);
		}

		var module = new Module(logicalPath, isIndex, documentInfo, imports);
		var name = logicalPath.opLast.get(out var nameText) ? Sym.of(nameText) : documentProvider.rootName;
		var (klass, diagnostics) = Checker.checkClass(module, imports, ast.klass, name);
		module.klass = klass;
		module.diagnostics = diagnostics;
		return (module, isReused: false);
	}

	static Either<BuiltinClass, Path> importPath(Path importerPath, Ast.Import import) {
		switch (import) {
			case Ast.Import.Global g: {
				var (loc, path) = g;
				return Either<BuiltinClass, Path>.Left(resolveGlobalImport(loc, path));
			}

			case Ast.Import.Relative rel: {
				var (loc, path) = rel;
				unused(loc); //TODO: error message should use loc
				return Either<BuiltinClass, Path>.Right(importerPath.resolve(path));
			}

			default:
				throw unreachable();
		}
	}

	static BuiltinClass resolveGlobalImport(Loc loc, Path path) {
		if (BuiltinClass.tryImportBuiltin(path, out var cls))
			return cls;

		unused(loc); //TODO: error message should use loc
		throw TODO();
	}

	struct ModuleState {
		internal readonly Op<Module> _module;
		internal readonly Kind kind;

		ModuleState(Op<Module> module, Kind kind) { this._module = module; this.kind = kind; }

		internal Module module => _module.force;

		internal enum Kind {
			CompiledReused,
			CompiledFresh,
			Compiling,
			//Failed
		}

		internal static readonly ModuleState compiling = new ModuleState(Op<Module>.None, Kind.Compiling);
		internal static ModuleState compiled(Module module, bool isReused) => new ModuleState(Op.Some(module), isReused ? Kind.CompiledReused : Kind.CompiledFresh);
	}
}
