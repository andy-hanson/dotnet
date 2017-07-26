using System;

using Diag;
using static Utils;

namespace Model {
	/** Module or BuiltinClass. */
	interface Imported {
		Sym name { get; }
		ClassLike importedClass { get; }
		Dat getImportedId();
	}

	// A module's identity is its path.
	sealed class Module : ModelElement, Imported, IEquatable<Module>, ToData<Module>, Identifiable<Path> {
		/**
		For "a.nz" this is "a".
		For "a/index.nz" this is still "a".
		The difference is indicated by `isIndex`.
		Use `fullPath` to get the full path.
		*/
		internal readonly Path logicalPath;
		internal readonly bool isIndex;
		internal readonly DocumentInfo document;
		// Technically these form a tree and thus aren't up-pointers, but don't want to serialize imports when serializing a module.
		[UpPointer] internal readonly Arr<Imported> imports;
		Late<Klass> _klass;
		internal Klass klass { get => _klass.get; set => _klass.set(value); }
		Late<Arr<Diagnostic>> _diagnostics;
		internal Arr<Diagnostic> diagnostics { get => _diagnostics.get; set => _diagnostics.set(value); }
		//TODO: does this belong here? Or somewhere else?
		[NotData] internal readonly LineColumnGetter lineColumnGetter;

		internal Module(Path logicalPath, bool isIndex, DocumentInfo document, Arr<Imported> imports) {
			this.logicalPath = logicalPath;
			this.isIndex = isIndex;
			this.document = document;
			this.imports = imports;
			this.lineColumnGetter = new LineColumnGetter(document.text);
		}

		internal Path fullPath() => ModuleResolver.fullPath(logicalPath, isIndex);
		internal Sym name => klass.name;
		Sym Imported.name => name;
		ClassLike Imported.importedClass => klass;

		internal string getText(Loc loc) =>
			document.text.slice(loc.start.index, loc.end.index);

		bool IEquatable<Module>.Equals(Module m) => object.ReferenceEquals(this, m);
		public override int GetHashCode() => logicalPath.GetHashCode();

		public bool deepEqual(Module m) =>
			logicalPath.deepEqual(m.logicalPath) &&
			isIndex == m.isIndex &&
			document.deepEqual(m.document) &&
			imports.eachCorresponds(m.imports, (a, b) => {
				switch (a) {
					case Module ma:
						return b is Module mb && ma.equalsId<Module, Path>(mb);
					case BuiltinClass ca:
						return b is BuiltinClass cb && ca.equalsId<BuiltinClass, BuiltinClass.Id>(cb);
					default:
						throw unreachable();
				}
			}) &&
			klass.deepEqual(m.klass);

		public Dat toDat() => Dat.of(this,
			nameof(logicalPath), logicalPath,
			nameof(isIndex), Dat.boolean(isIndex),
			nameof(document), document,
			nameof(imports), Dat.arr(imports.map(i => i.getImportedId())),
			nameof(klass), klass);

		public Path getId() => logicalPath;

		Dat Imported.getImportedId() => getId().toDat();
	}
}
