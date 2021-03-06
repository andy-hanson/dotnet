using Model;

namespace Diag.CheckDiags {
	internal sealed class NotAnAbstractClass : Diag<NotAnAbstractClass> {
		[UpPointer] internal readonly ClassDeclarationLike cls;
		internal NotAnAbstractClass(ClassDeclarationLike cls) { this.cls = cls; }

		public override void show<S>(S s) =>
			s.add("Can't extend non-abstract class ").add(cls.name.str);

		public override bool deepEqual(NotAnAbstractClass n) =>
			cls.equalsId<ClassDeclarationLike, ClassDeclarationLike.Id>(n.cls);
		public override Dat toDat() => Dat.of(this,
			nameof(cls), cls);
	}

	internal sealed class ClassNotFound : Diag<ClassNotFound> {
		internal readonly Sym name;
		internal ClassNotFound(Sym name) { this.name = name; }

		public override void show<S>(S s) =>
			s.add("Class ").add(name.str).add(" not found.");

		public override bool deepEqual(ClassNotFound c) => name.deepEqual(c.name);
		public override Dat toDat() => Dat.of(this, nameof(name), name);
	}

	internal sealed class ImplsMismatch : Diag<ImplsMismatch> {
		[UpPointer] internal readonly Arr<AbstractMethodLike> abstractMethods;
		internal ImplsMismatch(Arr<AbstractMethodLike> abstractMethods) { this.abstractMethods = abstractMethods; }

		public override void show<S>(S s) =>
			s.add("Abstract method implementations must be exactly, in order: ")
				.join(abstractMethods, a => a.name.str);

		public override bool deepEqual(ImplsMismatch w) =>
			abstractMethods.eachEqualId<AbstractMethodLike, AbstractMethodLike.Id>(w.abstractMethods);
		public override Dat toDat() => Dat.of(this,
			nameof(abstractMethods), Dat.arrOfIds<AbstractMethodLike, AbstractMethodLike.Id>(abstractMethods));
	}

	internal sealed class DuplicateParameterName : Diag<DuplicateParameterName> {
		internal readonly Sym name;
		internal DuplicateParameterName(Sym name) { this.name = name; }

		public override void show<S>(S s) =>
			s.add("There are two parameters named ").add(name.str);

		public override bool deepEqual(DuplicateParameterName d) => name.deepEqual(d.name);
		public override Dat toDat() => Dat.of(this, nameof(name), name);
	}

	internal sealed class WrongImplParameters : Diag<WrongImplParameters> {
		[UpPointer] internal readonly AbstractMethodLike implemented;
		internal WrongImplParameters(AbstractMethodLike implemented) { this.implemented = implemented; }

		public override void show<S>(S s) =>
			s.add("Parameters for implementation of ")
				.add(implemented.name.str)
				.add(" must be exactly, in order: ")
				.join(implemented.parameters, p => p.name.str);

		public override bool deepEqual(WrongImplParameters w) =>
			implemented.equalsId<AbstractMethodLike, AbstractMethodLike.Id>(w.implemented);
		public override Dat toDat() => Dat.of(this,
			nameof(implemented), implemented.getId());
	}

	internal sealed class DuplicateMember : Diag<DuplicateMember> {
		[UpPointer] internal readonly MemberDeclaration firstMember;
		[UpPointer] internal readonly MemberDeclaration secondMember;
		internal DuplicateMember(MemberDeclaration firstMember, MemberDeclaration secondMember) { this.firstMember = firstMember; this.secondMember = secondMember; }

		public override void show<S>(S s) =>
			s.add("Duplicate members: ").showMember(firstMember, upper: false).add(" and ").showMember(secondMember, upper: false).add('.');

		public override bool deepEqual(DuplicateMember d) =>
			firstMember.equalsId<MemberDeclaration, MemberId>(d.firstMember) &&
			secondMember.equalsId<MemberDeclaration, MemberId>(d.secondMember);
		public override Dat toDat() => Dat.of(this,
			nameof(firstMember), firstMember.getMemberId(),
			nameof(secondMember), secondMember.getMemberId());
	}
}
