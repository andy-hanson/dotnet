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

	internal sealed class DuplicateParameterName : Diag<DuplicateParameterName> {
		internal readonly Sym name;
		internal DuplicateParameterName(Sym name) { this.name = name; }

		internal override string show() =>
			$"There are two parameters named {name.str}";

		public override bool deepEqual(DuplicateParameterName d) => name.deepEqual(d.name);
		public override Dat toDat() => Dat.of(this, nameof(name), name);
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
}
