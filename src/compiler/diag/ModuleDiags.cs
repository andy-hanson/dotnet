namespace Diag.ModuleDiag {
	internal sealed class CircularDependency : Diag<CircularDependency> {
		internal readonly Path path;
		internal CircularDependency(Path path) { this.path = path; }

		internal override void show(StringMaker s) =>
			s.add("Circular dependency at module ").add(path.toPathString());

		public override bool deepEqual(CircularDependency c) => path.deepEqual(c.path);
		public override Dat toDat() => Dat.of(this, nameof(path), path);
	}

	internal sealed class CantFindLocalModule : Diag<CantFindLocalModule> {
		internal readonly Path logicalPath;
		internal CantFindLocalModule(Path logicalPath) { this.logicalPath = logicalPath; }

		internal override void show(StringMaker s) {
			s.add("Can't find module '");
			s.add(logicalPath.toPathString());
			s.add("'.\nTried ");
			ModuleResolver.attemptedPaths(logicalPath).join(", ", s, (ss, p) => p.toPathString(ss));
			s.add(".");
		}

		public override bool deepEqual(CantFindLocalModule c) => logicalPath.deepEqual(c.logicalPath);
		public override Dat toDat() => Dat.of(this, nameof(logicalPath), logicalPath);
	}
}
