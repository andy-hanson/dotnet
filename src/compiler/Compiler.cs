using Diag;
using Diag.ModuleDiag;
using Model;
using static Utils;

/**
Stores the state of a previously-compiled program.
This is 100% immutable.
*/
struct CompiledProgram : ToData<CompiledProgram> {
	[NotData] internal readonly DocumentProvider documentProvider;
	internal readonly Dict<Path, ModuleOrFail> modules;

	internal CompiledProgram(DocumentProvider documentProvider, Dict<Path, ModuleOrFail> modules) {
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

	/**
	Returns None iff the root module could not be found.
	(If some other module can not be found, its parent will be a FailModule with a diagnostic for that.)
	*/
	internal static Op<(CompiledProgram, ModuleOrFail root)> compile(Path path, DocumentProvider documentProvider, Op<CompiledProgram> oldProgram) {
		if (oldProgram.get(out var old))
			assert(old.documentProvider == documentProvider);
		var compiler = new Compiler(documentProvider, oldProgram);
		var rootModuleResult = compiler.compileSingle(path).result; // Don't care about isReused at this level
		switch (rootModuleResult.kind) {
			case CompileSingleResult.Kind.Found:
				var newProgram = new CompiledProgram(documentProvider, compiler.modules.mapValues(o => o.module));
				return Op.Some((newProgram, rootModuleResult.found));

			case CompileSingleResult.Kind.Missing:
				return Op<(CompiledProgram, ModuleOrFail)>.None;

			case CompileSingleResult.Kind.Circular: // Should be impossible for the very first import to already form a circle.
			default:
				throw unreachable();
		}
	}

	internal static Op<(CompiledProgram, ModuleOrFail root)> compile(Path path, DocumentProvider documentProvider) =>
		compile(path, documentProvider, Op<CompiledProgram>.None);

	internal static Op<(CompiledProgram, ModuleOrFail root)> compileDir(Path dir) =>
		compile(Path.empty, DocumentProviders.fileSystemDocumentProvider(dir));

	internal static Op<(CompiledProgram, ModuleOrFail root)> compileFile(Path path) {
		var fileName = path.last.withoutEndIfEndsWith(".nz");
		var filePath = fileName == "index" ? Path.empty : Path.fromParts(fileName);
		return compile(filePath, DocumentProviders.fileSystemDocumentProvider(path.directory()));
	}

	/**
	isReused: true if this is the same module from the old program.
	*/
	(CompileSingleResult result, bool isReused) compileSingle(Path logicalPath) {
		if (modules.get(logicalPath, out var alreadyCompiled)) {
			switch (alreadyCompiled.kind) {
				case ModuleState.Kind.Compiling:
					//TODO: attach an error to the Module.
					//raiseWithPath(logicalPath, fromLoc ?? Loc.zero, Err.CircularDependency(logicalPath));
					return (CompileSingleResult.Circular, isReused: true);

				case ModuleState.Kind.CompiledFresh:
				case ModuleState.Kind.CompiledReused:
					// Already compiled in the new program.
					// This can happen if the same module is a dependency of two other modules.
					return (CompileSingleResult.Found(alreadyCompiled.module), isReused: alreadyCompiled.kind == ModuleState.Kind.CompiledReused);

				default:
					throw unreachable();
			}
		} else {
			if (!ModuleResolver.getDocumentFromLogicalPath(documentProvider, logicalPath, out var fullPath, out var isIndex, out var document)) {
				// Don't issue diagnostic yet; caller should be responsible for knowing what to do with a missing module.
				return (CompileSingleResult.Missing, isReused: false);
			}

			var parseResult = document.parseResult;
			if (parseResult.isRight) {
				var fail = new FailModule(logicalPath, isIndex, document, Arr.empty<Either<Imported, FailModule>>(), Arr.of(parseResult.right));
				modules.add(logicalPath, ModuleState.compiled(fail, isReused: false));
				return (CompileSingleResult.Found(fail), isReused: false);
			} else {
				var (importAsts, classAst) = parseResult.left;
				modules.add(logicalPath, ModuleState.compiling);
				var (module, isReused) = doCompileSingle(logicalPath, document, importAsts, classAst, fullPath, isIndex);
				modules.change(logicalPath, ModuleState.compiled(module, isReused));
				return (CompileSingleResult.Found(module), isReused);
			}
		}
	}

	(ModuleOrFail module, bool isReused) doCompileSingle(Path logicalPath, DocumentInfo document, Arr<Ast.Import> importAsts, Ast.ClassDeclaration classAst, Path fullPath, bool isIndex) {
		var (importsResult, allDependenciesReused) = resolveImports(fullPath, importAsts);

		// We will only bother looking at the old module if all of our dependencies were safely reused.
		// If oldModule doesn't exactly match, we'll ignore it completely.
		if (allDependenciesReused &&
			oldProgram.get(out var old) &&
			old.modules.get(logicalPath, out var oldModule) &&
			oldModule.document.sameVersionAs(document)) {
			return (oldModule, isReused: true);
		}

		if (importsResult.isRight) {
			var (attemptedImports, importDiagnostics) = importsResult.right;
			var fail = new FailModule(logicalPath, isIndex, document, attemptedImports, importDiagnostics);
			return (fail, isReused: false);
		}

		// All dependencies loaded successfully, so can successfully create a module.
		var imports = importsResult.left;
		var module = new Module(logicalPath, isIndex, document, imports);
		var name = logicalPath.opLast.get(out var nameText) ? Sym.of(nameText) : documentProvider.rootName;
		// We only type-check if there were no parse/import diagnostics, so if we got here, these are the only diagnostics.
		var (klass, diagnostics) = Checker.checkClass(module, imports, classAst, name);
		module.klass = klass;
		module.diagnostics = diagnostics;
		return (module, isReused: false);
	}

	(Either<Arr<Imported>, (Arr<Either<Imported, FailModule>>, Arr<Diagnostic>)>, bool dependenciesReused) resolveImports(Path fullPath, Arr<Ast.Import> importAsts) {
		var importDiagnostics = Arr.builder<Diagnostic>();
		var allDependenciesReused = true;
		var attemptedImports = importAsts.mapDefinedProbablyAll<Either<Imported, FailModule>>(import =>
			resolveImport(import, importDiagnostics, fullPath, ref allDependenciesReused));

		var res = attemptedImports.mapOrDie(i => i.opLeft, out var imports)
			? Either<Arr<Imported>, (Arr<Either<Imported, FailModule>>, Arr<Diagnostic>)>.Left(imports)
			: Either<Arr<Imported>, (Arr<Either<Imported, FailModule>>, Arr<Diagnostic>)>.Right((attemptedImports, importDiagnostics.finish()));
		return (res, allDependenciesReused);
	}

	Op<Either<Imported, FailModule>> resolveImport(Ast.Import import, Arr.Builder<Diagnostic> importDiagnostics, Path fullPath, ref bool allDependenciesReused) {
		switch (import) {
			case Ast.Import.Global g: {
				var (loc, path) = g;
				if (BuiltinsLoader.tryImportBuiltin(path, out var cls))
					return Op.Some(Either<Imported, FailModule>.Left(cls));
				else {
					importDiagnostics.add(new Diagnostic(loc, new CantFindGlobalModule(path)));
					return Op<Either<Imported, FailModule>>.None;
				}
			}

			case Ast.Import.Relative rel: {
				var (loc, relativePath) = rel;
				var (importedModule, isImportReused) = compileSingle(fullPath.resolve(relativePath));
				allDependenciesReused = allDependenciesReused && isImportReused;
				switch (importedModule.kind) {
					case CompileSingleResult.Kind.Circular:
					case CompileSingleResult.Kind.Missing: {
						var diag = importedModule.kind == CompileSingleResult.Kind.Circular
							? new CircularDependency(fullPath, relativePath)
							: new CantFindLocalModule(fullPath, relativePath).upcast<DiagnosticData>();
						importDiagnostics.add(new Diagnostic(import.loc, diag));
						return Op<Either<Imported, FailModule>>.None;
					}

					case CompileSingleResult.Kind.Found: {
						var i = importedModule.found;
						return Op.Some(i is Module m ? Either<Imported, FailModule>.Left(m) : Either<Imported, FailModule>.Right((FailModule)i));
					}

					default:
						throw unreachable();
				}
			}

			default:
				throw unreachable();
		}
	}

	struct CompileSingleResult {
		internal enum Kind { Missing, Circular, Found }
		internal readonly Kind kind;
		readonly Op<ModuleOrFail> _found;
		CompileSingleResult(Kind kind, Op<ModuleOrFail> found) { this.kind = kind; this._found = found; }

		internal ModuleOrFail found => _found.force;

		internal static readonly CompileSingleResult Missing = new CompileSingleResult(Kind.Missing, Op<ModuleOrFail>.None);
		internal static readonly CompileSingleResult Circular = new CompileSingleResult(Kind.Circular, Op<ModuleOrFail>.None);
		internal static CompileSingleResult Found(ModuleOrFail mf) => new CompileSingleResult(Kind.Found, Op.Some(mf));
	}

	struct ModuleState {
		internal readonly Op<ModuleOrFail> _module;
		internal readonly Kind kind;

		ModuleState(Op<ModuleOrFail> module, Kind kind) { this._module = module; this.kind = kind; }

		internal ModuleOrFail module => _module.force;

		internal enum Kind {
			CompiledReused, // Reused from old program.
			CompiledFresh,
			Compiling,
		}

		internal static readonly ModuleState compiling =
			new ModuleState(Op<ModuleOrFail>.None, Kind.Compiling);

		internal static ModuleState compiled(ModuleOrFail module, bool isReused) =>
			new ModuleState(Op.Some(module), isReused ? Kind.CompiledReused : Kind.CompiledFresh);
	}
}
