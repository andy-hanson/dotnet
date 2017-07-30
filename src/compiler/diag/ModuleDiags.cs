namespace Diag.ModuleDiag {
	internal sealed class CircularDependency : Diag<CircularDependency> {
		internal readonly Path importerPath;
		internal readonly RelPath importedPath;
		internal CircularDependency(Path importerPath, RelPath importedPath) {
			this.importerPath = importerPath;
			this.importedPath = importedPath;
		}

		public override void show(StringMaker s) {
			s.add("Circular dependency when module ");
			s.add(importerPath);
			s.add(" imports ");
			s.add(importedPath);
			s.add(".");
		}

		public override bool deepEqual(CircularDependency c) =>
			importerPath.deepEqual(c.importerPath) &&
			importedPath.deepEqual(c.importedPath);
		public override Dat toDat() => Dat.of(this,
			nameof(importerPath), importerPath,
			nameof(importedPath), importedPath);
	}

	internal sealed class CantFindLocalModule : Diag<CantFindLocalModule> {
		internal readonly Path importerPath;
		internal readonly RelPath importedPath;
		internal CantFindLocalModule(Path importerPath, RelPath importedPath) {
			this.importerPath = importerPath;
			this.importedPath = importedPath;
		}

		public override void show(StringMaker s) =>
			s.add("Can't find module '")
				.add(importedPath)
				.add("' from '")
				.add(importerPath)
				.add("'.\nTried ")
				.join(ModuleResolver.attemptedPaths(importerPath, importedPath))
				.add(".");

		public override bool deepEqual(CantFindLocalModule c) =>
			importerPath.deepEqual(c.importerPath) &&
			importedPath.deepEqual(c.importedPath);
		public override Dat toDat() => Dat.of(this,
			nameof(importerPath), importerPath,
			nameof(importedPath), importedPath);
	}

	internal sealed class CantFindGlobalModule : Diag<CantFindGlobalModule> {
		internal readonly Path path;
		internal CantFindGlobalModule(Path path) { this.path = path; }

		public override void show(StringMaker s) =>
			s.add("Can't find global module '").add(path).add("'.");

		public override bool deepEqual(CantFindGlobalModule g) => path.deepEqual(g.path);
		public override Dat toDat() => Dat.of(this, nameof(path), path);
	}
}
