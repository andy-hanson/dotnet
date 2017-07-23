using Model;

namespace Diag.CheckExprDiags {
	internal sealed class DelegatesNotYetSupported : NoDataDiag<DelegatesNotYetSupported> {
		internal static readonly DelegatesNotYetSupported instance = new DelegatesNotYetSupported();
		internal override string show() => "Delegates not yet supported.";
	}

	internal sealed class CantCombineTypes : Diag<CantCombineTypes> {
		[UpPointer] internal readonly Ty ty1;
		[UpPointer] internal readonly Ty ty2;
		internal CantCombineTypes(Ty ty1, Ty ty2) { this.ty1 = ty1; this.ty2 = ty2; }

		internal override string show() =>
			$"Mismatch in type inference: {ty1}, {ty2}";

		public override bool deepEqual(CantCombineTypes e) => ty1.equalsId<Ty, TyId>(e.ty1) && ty2.equalsId<Ty, TyId>(e.ty2);
		public override Dat toDat() => Dat.of(this, nameof(ty1), ty1.getTyId(), nameof(ty2), ty2.getTyId());
	}
}
