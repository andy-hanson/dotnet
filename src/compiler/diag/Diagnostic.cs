using System;

namespace Diag {
	struct Diagnostic : ToData<Diagnostic> {
		internal readonly Loc loc;
		internal readonly DiagnosticData data;
		internal Diagnostic(Loc loc, DiagnosticData data) { this.loc = loc; this.data = data; }

		public override bool Equals(object o) => throw new NotSupportedException();
		public override int GetHashCode() => throw new NotSupportedException();
		public bool deepEqual(Diagnostic e) => loc.deepEqual(e.loc) && data.deepEqual(e.data);
		public Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(data), data);
	}

	abstract class DiagnosticData : ToData<DiagnosticData>, Show {
		public sealed override bool Equals(object o) => throw new NotSupportedException();
		public sealed override int GetHashCode() => throw new NotSupportedException();

		public abstract bool deepEqual(DiagnosticData e);
		public abstract Dat toDat();

		public abstract void show(StringMaker s);
	}

	/** Implementation class for every DiagnosticData. */
	internal abstract class Diag<Self> : DiagnosticData, ToData<Self> where Self : DiagnosticData, ToData<Self> {
		public sealed override bool deepEqual(DiagnosticData e) => e is Self s && deepEqual(s);
		public abstract bool deepEqual(Self s);
	}

	abstract class NoDataDiag<Self> : Diag<Self> where Self : DiagnosticData, ToData<Self> {
		protected NoDataDiag() {}
		public override bool deepEqual(Self e) => object.ReferenceEquals(this, e);
		public override Dat toDat() => Dat.str(GetType().Name);
		public sealed override void show(StringMaker s) => s.add(str);
		protected abstract string str { get; }
	}
}
