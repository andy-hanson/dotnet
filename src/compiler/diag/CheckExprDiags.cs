using Model;

//mv
static class ShowUtils {
	internal static S showMember<S>(this S s, MemberDeclaration m, bool upper) where S : Shower<S> =>
		s.add(m.showKind(upper)).add(' ').add(m.klass.name.str).add('.').add(m.name.str);
}

namespace Diag.CheckExprDiags {
	internal sealed class NotAssignable : Diag<NotAssignable> {
		[UpPointer] internal readonly Ty expected;
		[UpPointer] internal readonly Ty actual;
		internal NotAssignable(Ty expected, Ty actual) { this.expected = expected; this.actual = actual; }

		public override void show<S>(S s) =>
			s.add("Expected type ").showTy(expected).add(", got ").showTy(actual);

		public override bool deepEqual(NotAssignable n) =>
			expected.equalsId<Ty, TyId>(n.expected) && actual.equalsId<Ty, TyId>(n.actual);
		public override Dat toDat() => Dat.of(this, nameof(expected), expected, nameof(actual), actual);
	}

	internal sealed class CantReassignParameter : Diag<CantReassignParameter> {
		[UpPointer] internal readonly Parameter parameter;
		internal CantReassignParameter(Parameter parameter) { this.parameter = parameter; }

		public override void show<S>(S s) =>
			s.add("Can't re-assign parameter ").add(parameter.name.str);

		public override bool deepEqual(CantReassignParameter c) => parameter.equalsId<Parameter, Sym>(c.parameter);
		public override Dat toDat() => Dat.of(this, nameof(parameter), parameter.getId());
	}

	internal sealed class CantReassignLocal : Diag<CantReassignLocal> {
		[UpPointer] internal readonly Pattern.Single local;
		internal CantReassignLocal(Pattern.Single local) { this.local = local; }

		public override void show<S>(S s) =>
			s.add("Can't re-assign local ").add(local.name.str);

		public override bool deepEqual(CantReassignLocal c) => local.equalsId<Pattern.Single, Sym>(c.local);
		public override Dat toDat() => Dat.of(this, nameof(local), local.getId());
	}

	internal sealed class CantSetNonSlot : Diag<CantSetNonSlot> {
		[UpPointer] internal readonly MemberDeclaration member;
		internal CantSetNonSlot(MemberDeclaration member) { this.member = member; }

		public override void show<S>(S s) =>
			s.showMember(member, upper: true).add(" is not a slot; can't be set.");

		public override bool deepEqual(CantSetNonSlot c) => member.equalsId<MemberDeclaration, MemberId>(c.member);
		public override Dat toDat() => Dat.of(this, nameof(member), member.getMemberId());
	}

	internal sealed class SlotNotMutable : Diag<SlotNotMutable> {
		[UpPointer] internal readonly SlotDeclaration slot;
		internal SlotNotMutable(SlotDeclaration slot) { this.slot = slot; }

		public override void show<S>(S s) =>
			s.showMember(slot, upper: true).add(" is not mutable.");

		public override bool deepEqual(SlotNotMutable s) => slot.equalsId<SlotDeclaration, SlotDeclaration.Id>(s.slot);
		public override Dat toDat() => Dat.of(this, nameof(slot), slot.getId());
	}

	internal sealed class MissingEffectToSetSlot : Diag<MissingEffectToSetSlot> {
		internal readonly Effect effect;
		[UpPointer] internal readonly SlotDeclaration slot;
		internal MissingEffectToSetSlot(Effect effect, SlotDeclaration slot) { this.effect = effect; this.slot = slot; }

		public override void show<S>(S s) =>
			s.showMember(slot, upper: true).add(" can't be set through a reference with only the ").add(effect).add(" effect. Needs ").add(Effect.set);

		public override bool deepEqual(MissingEffectToSetSlot m) => slot.equalsId<SlotDeclaration, SlotDeclaration.Id>(m.slot);
		public override Dat toDat() => Dat.of(this, nameof(slot), slot.getId());
	}

	internal sealed class MissingEffectToGetSlot : Diag<MissingEffectToGetSlot> {
		[UpPointer] internal readonly SlotDeclaration slot;
		internal MissingEffectToGetSlot(SlotDeclaration slot) { this.slot = slot; }

		public override void show<S>(S s) =>
			s.showMember(slot, upper: true).add(" is mutable, and can't be read through a ").add(Effect.pure).add(" reference. Needs ").add(Effect.get);

		public override bool deepEqual(MissingEffectToGetSlot m) => slot.equalsId<SlotDeclaration, SlotDeclaration.Id>(m.slot);
		public override Dat toDat() => Dat.of(this, nameof(slot), slot.getId());
	}

	internal sealed class NewInvalid : Diag<NewInvalid> {
		[UpPointer] internal readonly ClassDeclaration klass;
		internal NewInvalid(ClassDeclaration klass) { this.klass = klass; }

		public override void show<S>(S s) =>
			s.add("Class ").add(klass.name.str).add(" does not have 'slots', so can't call 'new'.");

		public override bool deepEqual(NewInvalid n) =>
			klass.equalsId<ClassDeclaration, ClassDeclaration.Id>(n.klass);
		public override Dat toDat() => Dat.of(this,
			nameof(klass), klass.getId());
	}

	internal sealed class NewArgumentCountMismatch : Diag<NewArgumentCountMismatch> {
		[UpPointer] internal readonly ClassHead.Slots slots;
		internal readonly uint argumentsCount;
		internal NewArgumentCountMismatch(ClassHead.Slots slots, uint argumentsCount) {
			this.slots = slots;
			this.argumentsCount = argumentsCount;
		}

		public override void show<S>(S s) =>
			s.add("Class ").add(slots.klass.name.str).add(" has ").add(slots.slots.length).add(" slots, but there are ").add(argumentsCount).add("arguments to 'new'.");

		public override bool deepEqual(NewArgumentCountMismatch n) =>
			slots.equalsId<ClassHead.Slots, ClassDeclaration.Id>(n.slots) &&
			argumentsCount == n.argumentsCount;
		public override Dat toDat() => Dat.of(this,
			nameof(slots), slots.getId(),
			nameof(argumentsCount), Dat.nat(argumentsCount));
	}

	internal sealed class ArgumentCountMismatch : Diag<ArgumentCountMismatch> {
		[UpPointer] internal readonly MethodDeclaration called;
		internal readonly uint argumentsCount;
		internal ArgumentCountMismatch(MethodDeclaration called, uint argumentsCount) {
			this.called = called;
			this.argumentsCount = argumentsCount;
		}

		public override void show<S>(S s) =>
			s.showMember(called, upper: true).add(" takes ").add(called.parameters.length).add(" arguments; got ").add(argumentsCount);

		public override bool deepEqual(ArgumentCountMismatch a) =>
			called.equalsId<MethodDeclaration, MethodDeclaration.Id>(a.called) &&
			argumentsCount == a.argumentsCount;
		public override Dat toDat() => Dat.of(this,
			nameof(called), called.getId(),
			nameof(argumentsCount), Dat.nat(argumentsCount));
	}

	internal sealed class IllegalEffect : Diag<IllegalEffect> {
		internal readonly Effect targetEffect;
		internal readonly Effect methodEffect;
		internal IllegalEffect(Effect targetEffect, Effect methodEffect) {
			this.targetEffect = targetEffect;
			this.methodEffect = methodEffect;
		}

		public override void show<S>(S s) =>
			s.add("Target object has a ").add(targetEffect).add(" effect. Can't call method with a ").add(methodEffect).add(" effect.");

		public override bool deepEqual(IllegalEffect i) =>
			targetEffect.deepEqual(i.targetEffect) &&
			methodEffect.deepEqual(i.methodEffect);
		public override Dat toDat() => Dat.of(this,
			nameof(targetEffect), targetEffect.toDat(),
			nameof(methodEffect), methodEffect.toDat());
	}

	internal sealed class CantAccessSlotFromStaticMethod : Diag<CantAccessSlotFromStaticMethod> {
		[UpPointer] internal readonly SlotDeclaration slot;
		internal CantAccessSlotFromStaticMethod(SlotDeclaration slot) { this.slot = slot; }

		public override void show<S>(S s) =>
			s.showMember(slot, upper: true).add("can't be accessed from a static method.");

		public override bool deepEqual(CantAccessSlotFromStaticMethod c) =>
			slot.equalsId<SlotDeclaration, SlotDeclaration.Id>(c.slot);
		public override Dat toDat() => Dat.of(this, nameof(slot), slot.getId());
	}

	internal sealed class CantCallInstanceMethodFromStaticMethod : Diag<CantCallInstanceMethodFromStaticMethod> {
		[UpPointer] internal readonly MethodDeclaration method;
		internal CantCallInstanceMethodFromStaticMethod(MethodDeclaration method) { this.method = method; }

		public override void show<S>(S s) =>
			s.showMember(method, upper: true).add("can't be called from a function.");

		public override bool deepEqual(CantCallInstanceMethodFromStaticMethod c) =>
			method.equalsId<MethodDeclaration, MethodDeclaration.Id>(c.method);
		public override Dat toDat() => Dat.of(this, nameof(method), method.getId());
	}

	internal sealed class CantAccessStaticMethodThroughInstance : Diag<CantAccessStaticMethodThroughInstance> {
		[UpPointer] internal readonly MethodDeclaration method;
		internal CantAccessStaticMethodThroughInstance(MethodDeclaration method) { this.method = method; }

		public override void show<S>(S s) =>
			s.showMember(method, upper: true).add(" can't be called like a method.");

		public override bool deepEqual(CantAccessStaticMethodThroughInstance c) =>
			method.equalsId<MethodDeclaration, MethodDeclaration.Id>(c.method);
		public override Dat toDat() => Dat.of(this, nameof(method), method.getId());
	}

	internal sealed class MemberNotFound : Diag<MemberNotFound> {
		[UpPointer] internal readonly ClassDeclarationLike cls;
		internal readonly Sym memberName;
		internal MemberNotFound(ClassDeclarationLike cls, Sym memberName) { this.cls = cls; this.memberName = memberName; }

		public override void show<S>(S s) =>
			s.add(cls.name.str).add(" has no value ").add(memberName.str);

		public override bool deepEqual(MemberNotFound m) =>
			cls.equalsId<ClassDeclarationLike, ClassDeclarationLike.Id>(m.cls) &&
			memberName.deepEqual(m.memberName);
		public override Dat toDat() => Dat.of(this,
			nameof(cls), cls.getId(),
			nameof(memberName), memberName);
	}

	internal sealed class CantCombineTypes : Diag<CantCombineTypes> {
		[UpPointer] internal readonly Ty ty1;
		[UpPointer] internal readonly Ty ty2;
		internal CantCombineTypes(Ty ty1, Ty ty2) { this.ty1 = ty1; this.ty2 = ty2; }

		public override void show<S>(S s) =>
			s.add("Mismatch in type inference: inferred ").showTy(ty1).add(" earlier; now inferred ").showTy(ty2).add(".");

		public override bool deepEqual(CantCombineTypes e) => ty1.equalsId<Ty, TyId>(e.ty1) && ty2.equalsId<Ty, TyId>(e.ty2);
		public override Dat toDat() => Dat.of(this, nameof(ty1), ty1.getTyId(), nameof(ty2), ty2.getTyId());
	}

	internal sealed class CantNarrowEffectOfNonCovariantGeneric : Diag<CantNarrowEffectOfNonCovariantGeneric> {
		internal readonly Effect narrowedEffect;
		[UpPointer] internal readonly PlainTy ty;
		internal CantNarrowEffectOfNonCovariantGeneric(Effect narrowedEffect, PlainTy ty) {
			this.narrowedEffect = narrowedEffect;
			this.ty = ty;
		}

		public override void show<S>(S s) =>
			s.add("Property access with narrowed effect of ").add(narrowedEffect).add(" on type ").showTy(ty)
				.add(" forbidden; can't make an effect-narrowed version of a non-covariant generic type.");

		public override bool deepEqual(CantNarrowEffectOfNonCovariantGeneric c) => narrowedEffect.deepEqual(c.narrowedEffect) && ty.equalsId<Ty, TyId>(c.ty);
		public override Dat toDat() => Dat.of(this, nameof(narrowedEffect), narrowedEffect, nameof(ty), ty.getTyId());
	}

	internal sealed class DelegatesNotYetSupported : NoDataDiag<DelegatesNotYetSupported> {
		internal static readonly DelegatesNotYetSupported instance = new DelegatesNotYetSupported();
		protected override string str => "Delegates not yet supported.";
	}

	internal sealed class NotATailCall : NoDataDiag<NotATailCall> {
		internal static readonly NotATailCall instance = new NotATailCall();
		protected override string str => "'recur' does not appear in a tail-recursive position.";
	}
}
