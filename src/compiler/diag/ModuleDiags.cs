namespace Diag.ModuleDiag {
	internal sealed class CircularDependency : Diag<CircularDependency> {
		internal readonly Path path;
		internal CircularDependency(Path path) { this.path = path; }
		internal override string show => $"Circular dependency at module {path}";

		public override bool deepEqual(CircularDependency c) => path.deepEqual(c.path);
		public override Dat toDat() => Dat.of(this, nameof(path), path);
	}

	internal sealed class CantFindLocalModule : Diag<CantFindLocalModule> {
		internal readonly Path logicalPath;
		internal CantFindLocalModule(Path logicalPath) { this.logicalPath = logicalPath; }

		internal override string show =>
			$"Can't find module '{logicalPath}'.\nTried '{ModuleResolver.attemptedPaths(logicalPath)}'.";

		public override bool deepEqual(CantFindLocalModule c) => logicalPath.deepEqual(c.logicalPath);
		public override Dat toDat() => Dat.of(this, nameof(logicalPath), logicalPath);
	}
}
