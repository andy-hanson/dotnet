using System.Text;

using Model;

namespace Diag.CheckDiags {
	internal sealed class NotAnAbstractClass : Diag<NotAnAbstractClass> {
		[UpPointer] internal readonly ClsRef cls;
		internal NotAnAbstractClass(ClsRef cls) { this.cls = cls; }

		internal override string show() =>
			$"Can't extend non-abstract class ${cls.name.str}";

		public override bool deepEqual(NotAnAbstractClass n) =>
			cls.equalsId<ClsRef, ClsRefId>(n.cls);
		public override Dat toDat() => Dat.of(this,
			nameof(cls), cls);
	}

	internal sealed class ImplsMismatch : Diag<ImplsMismatch> {
		[UpPointer] internal readonly Arr<AbstractMethodLike> abstractMethods;
		internal ImplsMismatch(Arr<AbstractMethodLike> abstractMethods) { this.abstractMethods = abstractMethods; }

		internal override string show() {
			var sb = new StringBuilder();
			sb.Append("Abstract method implementations must be exactly, in order: ");
			abstractMethods.join(", ", a => a.name.str);
			return sb.ToString();
		}

		public override bool deepEqual(ImplsMismatch w) =>
			abstractMethods.eachEqualId<AbstractMethodLike, AbstractMethodLike.Id>(w.abstractMethods);
		public override Dat toDat() => Dat.of(this,
			nameof(abstractMethods), Dat.arrOfIds<AbstractMethodLike, AbstractMethodLike.Id>(abstractMethods));
	}

	internal sealed class WrongImplParameters : Diag<WrongImplParameters> {
		[UpPointer] internal readonly AbstractMethodLike implemented;
		internal WrongImplParameters(AbstractMethodLike implemented) { this.implemented = implemented; }

		internal override string show() {
			var sb = new StringBuilder();
			sb.Append("Parameters for implementation of ");
			sb.Append(implemented.name.str);
			sb.Append(" must be exactly, in order: ");
			implemented.parameters.join(", ", p => p.name.str);
			return sb.ToString();
		}

		public override bool deepEqual(WrongImplParameters w) =>
			implemented.equalsId<AbstractMethodLike, AbstractMethodLike.Id>(w.implemented);
		public override Dat toDat() => Dat.of(this,
			nameof(implemented), implemented.getId());
	}

	internal sealed class DuplicateMember : Diag<DuplicateMember> {
		[UpPointer] internal readonly Member firstMember;
		[UpPointer] internal readonly Member secondMember;
		internal DuplicateMember(Member firstMember, Member secondMember) { this.firstMember = firstMember; this.secondMember = secondMember; }

		internal override string show() =>
			$"Duplicate members. First member: ${firstMember.showKind()}, second: ${secondMember.showKind()}";

		public override bool deepEqual(DuplicateMember d) =>
			firstMember.equalsId<Member, MemberId>(d.firstMember) &&
			secondMember.equalsId<Member, MemberId>(d.secondMember);
		public override Dat toDat() => Dat.of(this,
			nameof(firstMember), firstMember.getMemberId(),
			nameof(secondMember), secondMember.getMemberId());
	}

	internal sealed class CombineTypes : Diag<CombineTypes> {
		[UpPointer] internal readonly Ty ty1;
		[UpPointer] internal readonly Ty ty2;
		internal CombineTypes(Ty ty1, Ty ty2) { this.ty1 = ty1; this.ty2 = ty2; }

		internal override string show() =>
			$"Mismatch in type inference: {ty1}, {ty2}";

		public override bool deepEqual(CombineTypes e) => ty1.equalsId<Ty, Ty.Id>(e.ty1) && ty2.equalsId<Ty, Ty.Id>(e.ty2);
		public override Dat toDat() => Dat.of(this, nameof(ty1), ty1.getId(), nameof(ty2), ty2.getId());
	}
}
