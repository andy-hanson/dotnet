using System;
using System.Collections.Generic;

using Model;
using static Utils;

/**
Stores the state of a previously-compiled program.
This is 100% immutable.
*/
struct CompiledProgram {
	internal readonly DocumentProvider documentProvider;
	internal readonly Dict<Path, Module> modules;

	internal CompiledProgram(DocumentProvider documentProvider, Dict<Path, Module> modules) {
		this.documentProvider = documentProvider;
		this.modules = modules;
	}
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
	readonly Dictionary<Path, ModuleState> modules = new Dictionary<Path, ModuleState>();

	Compiler(DocumentProvider documentProvider, Op<CompiledProgram> oldProgram) { this.documentProvider = documentProvider; this.oldProgram = oldProgram; }

	internal static Module compile(Path path, DocumentProvider documentProvider, Op<CompiledProgram> oldProgram, out CompiledProgram newProgram) {
		oldProgram.each(o => { assert(o.documentProvider == documentProvider); });
		var c = new Compiler(documentProvider, oldProgram);
		var rootModule = c.compileSingle(Op<PathLoc>.None, path, out var rootIsReused);
		newProgram = new CompiledProgram(documentProvider, c.modules.mapValues(o => o.module));
		return rootModule;
	}

	/**
	isReused: true if this is the same module from the old program.
	*/
	Module compileSingle(Op<PathLoc> from, Path logicalPath, out bool isReused) {
		if (modules.TryGetValue(logicalPath, out var alreadyCompiled)) {
			switch (alreadyCompiled.kind) {
				case ModuleState.Kind.Compiling:
					//TODO: attach an error to the Module.
					//raiseWithPath(logicalPath, fromLoc ?? Loc.zero, Err.CircularDependency(logicalPath));
					throw new Exception($"Circular dependency around {logicalPath}");

				case ModuleState.Kind.CompiledFresh:
				case ModuleState.Kind.CompiledReused:
					// Already compiled in the new program.
					// This can happen if the same module is a dependency of two other modules.
					isReused = alreadyCompiled.kind == ModuleState.Kind.CompiledReused;
					return alreadyCompiled.module;

				default:
					throw unreachable();
			}
		} else {
			//Must make a mark in modules *before* compiling dependencies!
			modules.Add(logicalPath, ModuleState.compiling);
			var module = doCompileSingle(from, logicalPath, out isReused);
			modules[logicalPath] = ModuleState.compiled(module, isReused);
			return module;
		}
	}

	Module doCompileSingle(Op<PathLoc> from, Path logicalPath, out bool isReused) {
		if (!ModuleResolver.getDocumentFromLogicalPath(documentProvider, from, logicalPath, out var fullPath, out var isMain, out var documentInfo)) {
			throw TODO(); //TODO: return Either<Module, CompileError> ?
		}

		var ast = documentInfo.parseResult.force(); // TODO: don't force

		var allDependenciesReused = true;
		var imports = ast.imports.map(import => {
			var importedModule = compileSingle(Op.Some(new PathLoc(fullPath, import.loc)), importPath(fullPath, import), out var isImportReused);
			allDependenciesReused = allDependenciesReused && isImportReused;
			return importedModule;
		});

		// We will only bother looking at the old module if all of our dependencies were safely reused.
		// If oldModule doesn't exactly match, we'll ignore it completely.
		if (allDependenciesReused && oldProgram.get(out var op) && op.modules.get(logicalPath, out var oldModule)) {
			isReused = true;
			return oldModule;
		}

		isReused = false;
		var klass = Checker.checkClass(imports, ast.klass, name: logicalPath.last);
		return new Module(logicalPath, isMain, documentInfo, imports, klass);
	}

	static Path importPath(Path importerPath, Ast.Module.Import import) {
		var g = import as Ast.Module.Import.Global;
		if (g != null)
			throw TODO();

		var rel = (Ast.Module.Import.Relative) import;
		return importerPath.resolve(rel.path);
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

		internal static ModuleState compiling = new ModuleState(Op<Module>.None, Kind.Compiling);
		internal static ModuleState compiled(Module module, bool isReused) => new ModuleState(Op.Some(module), isReused ? Kind.CompiledReused : Kind.CompiledFresh);
	}
}
