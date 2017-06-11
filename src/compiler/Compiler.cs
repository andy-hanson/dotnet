using System;
using System.Collections.Generic;
using System.IO;

using Model;
using static Utils;

interface FileInput {
    // Null if the file was not found.
    Op<string> read(Path path);
}

sealed class NativeFileInput : FileInput {
    readonly Path rootDir;
    public NativeFileInput(Path rootDir) { this.rootDir = rootDir; }

    public Op<string> read(Path path) {
        var fullPath = Path.resolveWithRoot(rootDir, path).ToString();
        if (File.Exists(fullPath)) {
            return Op.Some(File.ReadAllText(fullPath));
        }
        return Op<string>.None;
    }
}

internal interface CompilerHost {
    FileInput io { get; }
}

class DefaultCompilerHost : CompilerHost {
    readonly FileInput _io;
    internal DefaultCompilerHost(Path rootDir) {
        this._io = new NativeFileInput(rootDir);
    }

    FileInput CompilerHost.io => _io;
}

sealed class Compiler {
    readonly CompilerHost host;
    readonly IDictionary<Path, Module> modules = new Dictionary<Path, Module>();
    //todo: classloader

    internal Compiler(CompilerHost host) {
        this.host = host;
    }

    internal Ast.Module debugParse(Path path) {
        var source = host.io.read(path).force;
        return Parser.parseOrFail(path.last, source);
    }

    internal Module compile(Path path) => compileSingle(null, path);

    Module compileSingle(PathLoc? from, Path logicalPath) {
        if (modules.TryGetValue(logicalPath, out var alreadyCompiled)) {
            if (!alreadyCompiled.importsAreResolved)
                //TODO: attach an error to the Module.
                //raiseWithPath(logicalPath, fromLoc ?? Loc.zero, Err.CircularDependency(logicalPath));
                throw new Exception($"Circular dependency around {logicalPath}");
            return alreadyCompiled;
        }

        var module = resolveModule(host.io, from, logicalPath);

        //TODO: catch error and add it to the module, then don't do the rest.
        var moduleName = logicalPath.last;
        var ast = Parser.parseOrFail(moduleName, module.source);
        var imports = ast.imports.map(import => {
            var fromPath = module.fullPath;
            var fromHere = new PathLoc(module.fullPath, import.loc);
            if (import is Ast.Module.Import.Global)
                throw TODO();
            var rel = (Ast.Module.Import.Relative) import;
            return compileSingle(fromHere, fromPath.resolve(rel.path));
        });

        module.imports = imports;
        module.klass = Checker.checkClass(imports, ast.klass);
        //TODO: also write bytecode!
        return module;
    }


    static Module resolveModule(FileInput io, PathLoc? from, Path logicalPath) {
        Op<Module> attempt(Path fullPath, bool isMain) =>
            io.read(fullPath).map(source => new Module(logicalPath, isMain, source));

        //var main = logicalPath.add(mainNz);
        if (attempt(regularPath(logicalPath), false).get(out var res))
            return res;
        if (attempt(mainPath(logicalPath), true).get(out res))
            return res;

        var err = new Err.CantFindLocalModule(logicalPath);
        //TODO: error handle better
        if (from.HasValue) {
            throw new Exception($"Error at {from}: {err}");
        } else {
            throw new Exception(err.ToString());
        }
    }

    //kill
    internal static Arr<Path> attemptedPaths(Path logicalPath) =>
        Arr.of(regularPath(logicalPath), mainPath(logicalPath));

    internal static Path fullPath(Path logicalPath, bool isMain) =>
        isMain ? mainPath(logicalPath) : regularPath(logicalPath);

    static Path regularPath(Path logicalPath) =>
        logicalPath.addExtension(extension);

    static Path mainPath(Path logicalPath) =>
        logicalPath.add(mainNz);

    const string extension = ".nz";
    static Sym mainNz = Sym.of($"main{extension}");
}
