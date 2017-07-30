using Diag;
using Model;
using static Utils;

/** Module or FailModule */
interface ModuleOrFail : ToData<ModuleOrFail> {
	Path logicalPath { get; }
	Path fullPath();
	bool isIndex { get; }
	DocumentInfo document { get; }
	Arr<Diagnostic> diagnostics { get; }
}

sealed class FailModule : ModuleOrFail, ToData<FailModule> {
	internal readonly Path logicalPath;
	Path ModuleOrFail.logicalPath => logicalPath;
	internal readonly bool isIndex;
	bool ModuleOrFail.isIndex => isIndex;
	internal readonly DocumentInfo document;
	DocumentInfo ModuleOrFail.document => document;
	internal readonly Arr<Either<Imported, FailModule>> imports;
	internal readonly Arr<Diagnostic> diagnostics;
	Arr<Diagnostic> ModuleOrFail.diagnostics => diagnostics;

	internal FailModule(Path logicalPath, bool isIndex, DocumentInfo document, Arr<Either<Imported, FailModule>> imports, Arr<Diagnostic> diagnostics) {
		this.logicalPath = logicalPath;
		this.isIndex = isIndex;
		this.document = document;
		this.imports = imports;
		this.diagnostics = diagnostics;
		assert(diagnostics.any);
	}

	public Path fullPath() => ModuleResolver.fullPath(logicalPath, isIndex);

	public bool deepEqual(ModuleOrFail m) => m is FailModule f && deepEqual(f);
	public bool deepEqual(FailModule f) =>
		logicalPath.deepEqual(f.logicalPath) &&
		isIndex == f.isIndex &&
		document.deepEqual(f.document) &&
		imports.deepEqual(f.imports) &&
		diagnostics.deepEqual(f.diagnostics);

	public Dat toDat() => Dat.of(this,
		nameof(logicalPath), logicalPath,
		nameof(isIndex), Dat.boolean(isIndex),
		nameof(document), document,
		nameof(imports), Dat.arr(imports),
		nameof(diagnostics), Dat.arr(diagnostics));
}
