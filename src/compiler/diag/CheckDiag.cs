using Model;

namespace Diag.CheckDiags {
	internal sealed class CombineTypes : Diag<CombineTypes> {
		internal readonly Ty ty1;
		internal readonly Ty ty2;
		internal CombineTypes(Ty ty1, Ty ty2) { this.ty1 = ty1; this.ty2 = ty2; }
		internal override string show => $"Mismatch in type inference: {ty1}, {ty2}";

		public override bool deepEqual(CombineTypes e) => ty1.deepEqual(e.ty1) && ty2.deepEqual(e.ty2);
		public override Dat toDat() => Dat.of(this, nameof(ty1), ty1, nameof(ty2), ty2);
	}
}
