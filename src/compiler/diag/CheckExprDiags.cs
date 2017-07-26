using Model;

//mv
static class ShowUtils {
	internal static StringMaker showMember(this StringMaker s, Member m, bool upper) =>
		s.add(m.showKind(upper)).add(' ').add(m.klass.name.str).add('.').add(m.name.str);

	//mv?
	internal static StringMaker showTy(this StringMaker s, Ty ty) =>
		ShowTy.show(s, ty);

	internal static StringMaker showChar(this StringMaker s, char ch) {
		s.add('\'');
		switch (ch) {
			case '\n':
			case '\t':
				s.add('\\');
				goto default;
			default:
				s.add(ch);
				break;
		}
		return s.add('\'');
	}
}

namespace Diag.CheckExprDiags {
	internal sealed class NotAssignable : Diag<NotAssignable> {
		[UpPointer] internal readonly Ty expected;
		[UpPointer] internal readonly Ty actual;
		internal NotAssignable(Ty expected, Ty actual) { this.expected = expected; this.actual = actual; }

		public override void show(StringMaker s) {
			s.add("Expected type ");
			ShowTy.show(s, expected);
			s.add(", got ");
			ShowTy.show(s, actual);
		}

		public override bool deepEqual(NotAssignable n) =>
			expected.equalsId<Ty, TyId>(n.expected) && actual.equalsId<Ty, TyId>(n.actual);
		public override Dat toDat() => Dat.of(this, nameof(expected), expected, nameof(actual), actual);
	}

	internal sealed class CantReassignParameter : Diag<CantReassignParameter> {
		[UpPointer] internal readonly Parameter parameter;
		internal CantReassignParameter(Parameter parameter) { this.parameter = parameter; }

		public override void show(StringMaker s) =>
			s.add("Can't re-assign parameter ").add(parameter.name.str);

		public override bool deepEqual(CantReassignParameter c) => parameter.equalsId<Parameter, Sym>(c.parameter);
		public override Dat toDat() => Dat.of(this, nameof(parameter), parameter.getId());
	}

	internal sealed class CantReassignLocal : Diag<CantReassignLocal> {
		[UpPointer] internal readonly Pattern.Single local;
		internal CantReassignLocal(Pattern.Single local) { this.local = local; }

		public override void show(StringMaker s) =>
			s.add("Can't re-assign local ").add(local.name.str);

		public override bool deepEqual(CantReassignLocal c) => local.equalsId<Pattern.Single, Sym>(c.local);
		public override Dat toDat() => Dat.of(this, nameof(local), local.getId());
	}

	internal sealed class CantSetNonSlot : Diag<CantSetNonSlot> {
		[UpPointer] internal readonly Member member;
		internal CantSetNonSlot(Member member) { this.member = member; }

		public override void show(StringMaker s) =>
			s.showMember(member, upper: true).add(" is not a slot; can't be set.");

		public override bool deepEqual(CantSetNonSlot c) => member.equalsId<Member, MemberId>(c.member);
		public override Dat toDat() => Dat.of(this, nameof(member), member.getMemberId());
	}

	internal sealed class SlotNotMutable : Diag<SlotNotMutable> {
		[UpPointer] internal readonly Slot slot;
		internal SlotNotMutable(Slot slot) { this.slot = slot; }

		public override void show(StringMaker s) =>
			s.showMember(slot, upper: true).add(" is not mutable.");

		public override bool deepEqual(SlotNotMutable s) => slot.equalsId<Slot, Slot.Id>(s.slot);
		public override Dat toDat() => Dat.of(this, nameof(slot), slot.getId());
	}

	internal sealed class MissingEffectToSetSlot : Diag<MissingEffectToSetSlot> {
		[UpPointer] internal readonly Slot slot;
		internal MissingEffectToSetSlot(Slot slot) { this.slot = slot; }

		public override void show(StringMaker s) =>
			s.showMember(slot, upper: true).add(" can't be set through a reference that doesn't have the 'set' effect.");

		public override bool deepEqual(MissingEffectToSetSlot m) => slot.equalsId<Slot, Slot.Id>(m.slot);
		public override Dat toDat() => Dat.of(this, nameof(slot), slot.getId());
	}

	internal sealed class MissingEffectToGetSlot : Diag<MissingEffectToGetSlot> {
		[UpPointer] internal readonly Slot slot;
		internal MissingEffectToGetSlot(Slot slot) { this.slot = slot; }

		public override void show(StringMaker s) =>
			s.showMember(slot, upper: true).add(" is mutable, and can't be read through a pure reference.");

		public override bool deepEqual(MissingEffectToGetSlot m) => slot.equalsId<Slot, Slot.Id>(m.slot);
		public override Dat toDat() => Dat.of(this, nameof(slot), slot.getId());
	}

	internal sealed class NewInvalid : Diag<NewInvalid> {
		[UpPointer] internal readonly Klass klass;
		internal NewInvalid(Klass klass) { this.klass = klass; }

		public override void show(StringMaker s) =>
			s.add("Class ").add(klass.name.str).add(" does not have 'slots', so can't call 'new'.");

		public override bool deepEqual(NewInvalid n) =>
			klass.equalsId<Klass, Klass.Id>(n.klass);
		public override Dat toDat() => Dat.of(this,
			nameof(klass), klass.getId());
	}

	internal sealed class NewArgumentCountMismatch : Diag<NewArgumentCountMismatch> {
		[UpPointer] internal readonly KlassHead.Slots slots;
		internal readonly uint argumentsCount;
		internal NewArgumentCountMismatch(KlassHead.Slots slots, uint argumentsCount) {
			this.slots = slots;
			this.argumentsCount = argumentsCount;
		}

		public override void show(StringMaker s) =>
			s.add("Class ").add(slots.klass.name.str).add(" has ").add(slots.slots.length).add(" slots, but there are ").add(argumentsCount).add("arguments to 'new'.");

		public override bool deepEqual(NewArgumentCountMismatch n) =>
			slots.equalsId<KlassHead.Slots, Klass.Id>(n.slots) &&
			argumentsCount == n.argumentsCount;
		public override Dat toDat() => Dat.of(this,
			nameof(slots), slots.getId(),
			nameof(argumentsCount), Dat.nat(argumentsCount));
	}

	internal sealed class ArgumentCountMismatch : Diag<ArgumentCountMismatch> {
		[UpPointer] internal readonly Method called;
		internal readonly uint argumentsCount;
		internal ArgumentCountMismatch(Method called, uint argumentsCount) {
			this.called = called;
			this.argumentsCount = argumentsCount;
		}

		public override void show(StringMaker s) =>
			s.showMember(called, upper: true).add(" takes ").add(called.parameters.length).add(" arguments; got ").add(argumentsCount);

		public override bool deepEqual(ArgumentCountMismatch a) =>
			called.equalsId<Method, Method.Id>(a.called) &&
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

		public override void show(StringMaker s) =>
			s.add("Target object has a ").add(targetEffect.show).add(" effect. Can't call method with a ").add(methodEffect.show).add(" effect.");

		public override bool deepEqual(IllegalEffect i) =>
			targetEffect.deepEqual(i.targetEffect) &&
			methodEffect.deepEqual(i.methodEffect);
		public override Dat toDat() => Dat.of(this,
			nameof(targetEffect), targetEffect.toDat(),
			nameof(methodEffect), methodEffect.toDat());
	}

	internal sealed class CantAccessSlotFromStaticMethod : Diag<CantAccessSlotFromStaticMethod> {
		[UpPointer] internal readonly Slot slot;
		internal CantAccessSlotFromStaticMethod(Slot slot) { this.slot = slot; }

		public override void show(StringMaker s) =>
			s.showMember(slot, upper: true).add("can't be accessed from a static method.");

		public override bool deepEqual(CantAccessSlotFromStaticMethod c) =>
			slot.equalsId<Slot, Slot.Id>(c.slot);
		public override Dat toDat() => Dat.of(this, nameof(slot), slot.getId());
	}

	internal sealed class CantCallInstanceMethodFromStaticMethod : Diag<CantCallInstanceMethodFromStaticMethod> {
		[UpPointer] internal readonly Method method;
		internal CantCallInstanceMethodFromStaticMethod(Method method) { this.method = method; }

		public override void show(StringMaker s) =>
			s.showMember(method, upper: true).add("can't be called from a function.");

		public override bool deepEqual(CantCallInstanceMethodFromStaticMethod c) =>
			method.equalsId<Method, Method.Id>(c.method);
		public override Dat toDat() => Dat.of(this, nameof(method), method.getId());
	}

	internal sealed class CantAccessStaticMethodThroughInstance : Diag<CantAccessStaticMethodThroughInstance> {
		[UpPointer] internal readonly Method method;
		internal CantAccessStaticMethodThroughInstance(Method method) { this.method = method; }

		public override void show(StringMaker s) =>
			s.showMember(method, upper: true).add(" can't be called like a method.");

		public override bool deepEqual(CantAccessStaticMethodThroughInstance c) =>
			method.equalsId<Method, Method.Id>(c.method);
		public override Dat toDat() => Dat.of(this, nameof(method), method.getId());
	}

	internal sealed class MemberNotFound : Diag<MemberNotFound> {
		[UpPointer] internal readonly ClsRef cls;
		internal readonly Sym memberName;
		internal MemberNotFound(ClsRef cls, Sym memberName) { this.cls = cls; this.memberName = memberName; }

		public override void show(StringMaker s) =>
			s.add(cls.name.str).add(" has no value ").add(memberName.str);

		public override bool deepEqual(MemberNotFound m) =>
			cls.equalsId<ClsRef, ClsRefId>(m.cls) &&
			memberName.deepEqual(m.memberName);
		public override Dat toDat() => Dat.of(this,
			nameof(cls), cls.getClsRefId(),
			nameof(memberName), memberName);
	}

	internal sealed class CantCombineTypes : Diag<CantCombineTypes> {
		[UpPointer] internal readonly Ty ty1;
		[UpPointer] internal readonly Ty ty2;
		internal CantCombineTypes(Ty ty1, Ty ty2) { this.ty1 = ty1; this.ty2 = ty2; }

		public override void show(StringMaker s) =>
			s.add("Mismatch in type inference: inferred ").showTy(ty1).add(" earlier; now inferred ").showTy(ty2).add(".");

		public override bool deepEqual(CantCombineTypes e) => ty1.equalsId<Ty, TyId>(e.ty1) && ty2.equalsId<Ty, TyId>(e.ty2);
		public override Dat toDat() => Dat.of(this, nameof(ty1), ty1.getTyId(), nameof(ty2), ty2.getTyId());
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
