using System.Collections.Generic;

using static Utils;

namespace Model {
	abstract class Expr : ModelElement, MethodOrImplOrExpr, ToData<Expr> {
		internal readonly Loc loc;
		internal abstract Ty ty { get; }
		Late<MethodOrImplOrExpr> _parent;
		internal MethodOrImplOrExpr parent { get => _parent.get; set => _parent.set(value); }

		internal string getText() =>
			getModule().getText(loc);
		internal Module getModule() => getMethodOrImpl().klass.module;
		internal MethodOrImpl getMethodOrImpl() {
			var p = this.parent;
			while (p is Expr e)
				p = e.parent;
			return (MethodOrImpl)p;
		}

		protected Expr(Loc loc) {
			this.loc = loc;
		}

		internal abstract IEnumerable<Expr> children();
		protected static readonly IEnumerable<Expr> noChildren = System.Linq.Enumerable.Empty<Expr>();
		public abstract bool deepEqual(Expr e);
		public abstract Dat toDat();

		protected bool locEq(Expr e) => loc.deepEqual(e.loc);
	}

	/**
	Expr for a badly-typed expression.
	"casts" it to the correct type.
	Since programs with diagnostics should not be emitted, this does not need to compile to anything.
	*/
	internal sealed class BogusCast : Expr, ToData<BogusCast> {
		[UpPointer] internal readonly Ty correctTy;
		internal readonly Expr expr;
		internal BogusCast(Ty correctTy, Expr expr) : base(expr.loc) { this.correctTy = correctTy; this.expr = expr; }

		internal override IEnumerable<Expr> children() { yield return expr; }
		internal override Ty ty => correctTy;

		public override bool deepEqual(Expr e) => e is BogusCast b && deepEqual(b);
		public bool deepEqual(BogusCast b) =>
			locEq(b) &&
			correctTy.equalsId<Ty, TyId>(b.correctTy)
			&& expr.deepEqual(b.expr);
		public override Dat toDat() => Dat.of(this,
			// Don't include loc, since that's identical to expr's
			nameof(correctTy), correctTy.getTyId(),
			nameof(expr), expr);
	}

	/**
	We couldn't come up with a good Expr, so just use this placeholder.
	*/
	internal sealed class Bogus : Expr, ToData<Bogus> {
		[UpPointer] readonly Ty _ty;
		internal Bogus(Loc loc, Ty ty) : base(loc) { this._ty = ty; }

		internal override IEnumerable<Expr> children() => noChildren;
		internal override Ty ty => _ty;

		public override bool deepEqual(Expr e) => e is Bogus b && deepEqual(b);
		public bool deepEqual(Bogus b) => locEq(b) && ty.equalsId<Ty, TyId>(b.ty);
		public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(ty), _ty);
	}

	internal sealed class AccessParameter : Expr, ToData<AccessParameter> {
		[UpPointer] internal readonly Parameter param;
		internal AccessParameter(Loc loc, Parameter param) : base(loc) {
			this.param = param;
		}

		internal override IEnumerable<Expr> children() => noChildren;
		internal override Ty ty => param.ty;

		public override bool deepEqual(Expr e) => e is AccessParameter a && deepEqual(a);
		public bool deepEqual(AccessParameter a) => locEq(a) && param.equalsId<Parameter, Sym>(a.param);
		public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(param), param);
	}

	internal sealed class AccessLocal : Expr, ToData<AccessLocal> {
		[UpPointer] internal readonly Pattern.Single local;
		internal AccessLocal(Loc loc, Pattern.Single local) : base(loc) {
			this.local = local;
		}

		internal override IEnumerable<Expr> children() => noChildren;
		internal override Ty ty => local.ty;

		public override bool deepEqual(Expr e) => e is AccessLocal a && deepEqual(a);
		public bool deepEqual(AccessLocal a) => locEq(a) && local.equalsId<Pattern.Single, Sym>(a.local);
		public override Dat toDat() => Dat.of(this, nameof(local), local.getId());
	}

	internal sealed class Let : Expr, ToData<Let> {
		internal readonly Pattern assigned;
		internal readonly Expr value;
		internal readonly Expr then;

		internal Let(Loc loc, Pattern assigned, Expr value, Expr then) : base(loc) {
			this.assigned = assigned;
			this.value = value;
			value.parent = this;
			this.then = then;
			then.parent = this;
		}

		internal override IEnumerable<Expr> children() {
			yield return value;
			yield return then;
		}
		internal override Ty ty => then.ty;

		public override bool deepEqual(Expr e) => e is Let l && deepEqual(l);
		public bool deepEqual(Let l) => locEq(l) && assigned.deepEqual(l.assigned) && value.deepEqual(l.value) && then.deepEqual(l.then);
		public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(assigned), assigned, nameof(value), value, nameof(then), then);
	}

	internal sealed class Seq : Expr, ToData<Seq> {
		internal readonly Expr action;
		internal readonly Expr then;

		internal Seq(Loc loc, Expr action, Expr then) : base(loc) {
			this.action = action;
			action.parent = this;
			this.then = then;
			then.parent = this;
		}

		internal override IEnumerable<Expr> children() {
			yield return action;
			yield return then;
		}
		internal override Ty ty => then.ty;

		public override bool deepEqual(Expr e) => e is Seq s && deepEqual(s);
		public bool deepEqual(Seq s) => locEq(s) && action.deepEqual(s.action) && then.deepEqual(s.then);
		public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(action), action, nameof(then), then);
	}

	internal sealed class Literal : Expr, ToData<Literal> {
		internal readonly LiteralValue value;
		internal Literal(Loc loc, LiteralValue value) : base(loc) { this.value = value; }

		internal override IEnumerable<Expr> children() => noChildren;
		internal override Ty ty => value.ty;

		public override bool deepEqual(Expr e) => e is Literal l && deepEqual(l);
		public bool deepEqual(Literal l) => locEq(l) && value.deepEqual(l.value);
		public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(value), value);
	}

	internal sealed class IfElse : Expr, ToData<IfElse> {
		internal readonly Expr test;
		internal readonly Expr then;
		internal readonly Expr @else;
		[NotData] internal readonly Ty _ty; // Cached common type of 'then' and '@else'
		internal IfElse(Loc loc, Expr test, Expr then, Expr @else, Ty ty) : base(loc) {
			this.test = test;
			test.parent = this;
			this.then = then;
			then.parent = this;
			this.@else = @else;
			@else.parent = this;
			this._ty = ty;
		}

		internal override IEnumerable<Expr> children() {
			yield return test;
			yield return then;
			yield return @else;
		}
		internal override Ty ty => _ty;

		public override bool deepEqual(Expr e) => e is IfElse i && deepEqual(i);
		public bool deepEqual(IfElse i) =>
			locEq(i) &&
			test.deepEqual(i.test) &&
			then.deepEqual(i.then) &&
			@else.deepEqual(i.@else);
		public override Dat toDat() => Dat.of(this,
			nameof(loc), loc,
			nameof(test), test,
			nameof(then), then,
			nameof(@else), @else);
	}

	internal sealed class WhenTest : Expr, ToData<WhenTest> {
		internal readonly Arr<Case> cases;
		internal readonly Expr elseResult;
		[NotData] internal readonly Ty _ty; // Cached common type of all cases and elseResult.
		internal WhenTest(Loc loc, Arr<Case> cases, Expr elseResult, Ty ty) : base(loc) {
			assert(cases.length != 0);
			this.cases = cases;
			this.elseResult = elseResult;
			this._ty = ty;
			foreach (var kase in cases) {
				kase.test.parent = this;
				kase.result.parent = this;
			}
			elseResult.parent = this;
		}

		internal override IEnumerable<Expr> children() {
			foreach (var kase in cases) {
				yield return kase.test;
				yield return kase.result;
			}
			yield return elseResult;
		}
		internal override Ty ty => _ty;

		public override bool deepEqual(Expr e) => e is WhenTest w && deepEqual(w);
		public bool deepEqual(WhenTest w) =>
			locEq(w) &&
			cases.deepEqual(w.cases) &&
			elseResult.deepEqual(w.elseResult);
		public override Dat toDat() => Dat.of(this,
			nameof(loc), loc,
			nameof(cases), Dat.arr(cases),
			nameof(elseResult), elseResult);

		internal struct Case : ToData<Case> {
			internal readonly Loc loc;
			internal readonly Expr test;
			internal readonly Expr result;
			internal Case(Loc loc, Expr test, Expr result) { this.loc = loc; this.test = test; this.result = result; }

			public bool deepEqual(Case c) => loc.deepEqual(c.loc) && test.deepEqual(c.test) && result.deepEqual(c.result);
			public Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(test), test, nameof(result), result);
		}
	}

	internal sealed class Try : Expr, ToData<Try> {
		internal readonly Expr _do;
		internal readonly Op<Catch> _catch;
		internal readonly Op<Expr> _finally;
		[NotData] internal readonly Ty _ty; // Cached common type of 'do' and 'catch'
		internal Try(Loc loc, Expr _do, Op<Catch> _catch, Op<Expr> _finally, Ty ty) : base(loc) {
			this._do = _do;
			_do.parent = this;
			this._catch = _catch;
			if (_catch.get(out var c))
				c.then.parent = this;
			this._finally = _finally;
			if (_finally.get(out var f))
				f.parent = this;
			this._ty = ty;
		}

		internal override IEnumerable<Expr> children() {
			yield return _do;
			if (_catch.get(out var c))
				yield return c.then;
			if (_finally.get(out var f))
				yield return f;
		}
		internal override Ty ty => _ty;

		public override bool deepEqual(Expr e) => e is Try t && deepEqual(t);
		public bool deepEqual(Try t) =>
			locEq(t) &&
			_do.deepEqual(t._do) &&
			_catch.deepEqual(t._catch) &&
			_finally.deepEqual(t._finally);
		public override Dat toDat() => Dat.of(this,
			nameof(loc), loc,
			nameof(_do), _do,
			nameof(_catch), Dat.op(_catch),
			nameof(_finally), Dat.op(_finally));

		internal struct Catch : ToData<Catch> {
			internal readonly Loc loc;
			// Normally the type of a Pattern.Local is an inferred type, but here it's data! But avoid storing it twice.
			internal Ty exceptionTy => caught.ty;
			internal readonly Pattern.Single caught; // Includes the exception ty
			internal readonly Expr then;
			internal Catch(Loc loc, Pattern.Single exception, Expr then) {
				this.loc = loc;
				this.caught = exception;
				this.then = then;
			}

			public bool deepEqual(Catch c) =>
				loc.deepEqual(c.loc) &&
				exceptionTy.equalsId<Ty, TyId>(c.exceptionTy) &&
				caught.deepEqual(c.caught) &&
				then.deepEqual(c.then);
			public Dat toDat() => Dat.of(this,
				nameof(loc), loc,
				nameof(exceptionTy), exceptionTy.getTyId(),
				nameof(caught), caught,
				nameof(then), then);
		}
	}

	internal sealed class StaticMethodCall : Expr, ToData<StaticMethodCall> {
		[UpPointer] internal readonly MethodWithBodyLike method;
		internal readonly Arr<Expr> args;
		internal StaticMethodCall(Loc loc, MethodWithBodyLike method, Arr<Expr> args) : base(loc) {
			assert(method.isStatic);
			this.method = method;
			this.args = args;
			foreach (var arg in args)
				arg.parent = this;
		}

		internal override IEnumerable<Expr> children() => args;
		internal override Ty ty => method.returnTy;

		public override bool deepEqual(Expr e) => e is StaticMethodCall s && deepEqual(s);
		public bool deepEqual(StaticMethodCall s) =>
			method.equalsId<Method, Method.Id>(s.method) &&
			args.deepEqual(s.args);
		public override Dat toDat() => Dat.of(this,
			nameof(method), method.getId(),
			nameof(args), Dat.arr(args));
	}

	internal sealed class InstanceMethodCall : Expr, ToData<InstanceMethodCall> {
		internal readonly Expr target;
		[UpPointer] internal readonly Method method;
		internal readonly Arr<Expr> args;
		internal InstanceMethodCall(Loc loc, Expr target, Method method, Arr<Expr> args) : base(loc) {
			assert(!method.isStatic);
			this.target = target;
			target.parent = this;
			this.method = method;
			this.args = args;
			foreach (var arg in args)
				arg.parent = this;
		}

		internal override IEnumerable<Expr> children() {
			yield return target;
			foreach (var arg in args)
				yield return arg;
		}
		internal override Ty ty => method.returnTy;

		public override bool deepEqual(Expr e) => e is InstanceMethodCall m && deepEqual(m);
		public bool deepEqual(InstanceMethodCall m) =>
			locEq(m) &&
			target.deepEqual(m.target) &&
			method.equalsId<Method, Method.Id>(m.method) &&
			args.deepEqual(m.args);
		public override Dat toDat() => Dat.of(this,
			nameof(loc), loc,
			nameof(target), target,
			nameof(method), method.getId(),
			nameof(args), Dat.arr(args));
	}

	internal sealed class MyInstanceMethodCall : Expr, ToData<MyInstanceMethodCall> {
		[UpPointer] internal readonly Method method;
		internal readonly Arr<Expr> args;
		internal MyInstanceMethodCall(Loc loc, Method method, Arr<Expr> args) : base(loc) {
			assert(!method.isStatic);
			this.method = method;
			this.args = args;
			foreach (var arg in args)
				arg.parent = this;
		}

		internal override IEnumerable<Expr> children() => args;
		internal override Ty ty => method.returnTy;

		public override bool deepEqual(Expr e) => e is MyInstanceMethodCall m && deepEqual(m);
		public bool deepEqual(MyInstanceMethodCall m) =>
			locEq(m) &&
			method.equalsId<Method, Method.Id>(m.method) &&
			args.deepEqual(m.args);
		public override Dat toDat() => Dat.of(this,
			nameof(loc), loc,
			nameof(method), method.getId(),
			nameof(args), Dat.arr(args));
	}

	internal sealed class New : Expr, ToData<New> {
		/**
		This must be the slots of the class the 'new' is defined in.
		Can't directly construct any other class.
		Also, length must match with args.
		*/
		[ParentPointer] internal readonly KlassHead.Slots slots;
		internal readonly Arr<Expr> args;

		internal New(Loc loc, KlassHead.Slots slots, Arr<Expr> args) : base(loc) {
			this.slots = slots;
			this.args = args;
			foreach (var arg in args)
				arg.parent = this;
		}

		internal Klass klass => slots.klass;
		internal override IEnumerable<Expr> children() => args;
		internal override Ty ty => Ty.io(klass); // A new object always has full permission.

		public override bool deepEqual(Expr e) => e is New n && deepEqual(n);
		// Don't need to compare `klass` since that has only one legal value.
		public bool deepEqual(New n) => args.deepEqual(n.args);
		public override Dat toDat() => Dat.of(this, nameof(args), Dat.arr(args));
	}

	//Note: this contains a pointer to the current class for convenience.
	//Note: this should only happen in a non-static method. Otherwise we have Expr.Error
	internal sealed class GetMySlot : Expr, ToData<GetMySlot> {
		[ParentPointer] internal readonly Klass klass; // Class of the method this expression is in.
		[UpPointer] internal readonly Slot slot;
		internal GetMySlot(Loc loc, Klass klass, Slot slot) : base(loc) {
			this.klass = klass;
			this.slot = slot;
		}
		internal override IEnumerable<Expr> children() => noChildren;
		internal override Ty ty => slot.ty;

		public override bool deepEqual(Expr e) => e is GetMySlot g && deepEqual(g);
		public bool deepEqual(GetMySlot g) => slot.equalsId<Slot, Slot.Id>(g.slot);
		public override Dat toDat() => Dat.of(this, nameof(slot), slot.getId());
	}

	internal sealed class GetSlot : Expr, ToData<GetSlot> {
		internal readonly Expr target;
		[UpPointer] internal readonly Slot slot;
		internal GetSlot(Loc loc, Expr target, Slot slot) : base(loc) {
			this.target = target;
			target.parent = this;
			this.slot = slot;
		}

		internal override IEnumerable<Expr> children() { yield return target; }
		internal override Ty ty => TyUtils.getTyWithNarrowedEffect(target.ty.effect, slot.ty);

		public override bool deepEqual(Expr e) => e is GetSlot g && deepEqual(g);
		public bool deepEqual(GetSlot g) =>
			locEq(g) &&
			target.deepEqual(g.target) &&
			slot.equalsId<Slot, Slot.Id>(g.slot);
		public override Dat toDat() => Dat.of(this,
			nameof(loc), loc,
			nameof(target), target,
			nameof(slot), slot.getId());
	}

	internal sealed class SetSlot : Expr, ToData<SetSlot> {
		[UpPointer] internal readonly Slot slot;
		internal readonly Expr value;
		internal SetSlot(Loc loc, Slot slot, Expr value) : base(loc) {
			this.slot = slot;
			this.value = value;
			value.parent = this;
		}

		internal override IEnumerable<Expr> children() { yield return value; }
		internal override Ty ty => Ty.Void;

		public override bool deepEqual(Expr e) => e is SetSlot s && deepEqual(s);
		public bool deepEqual(SetSlot s) =>
			locEq(s) &&
			slot.equalsId<Slot, Slot.Id>(s.slot) &&
			value.deepEqual(s.value);
		public override Dat toDat() => Dat.of(this,
			nameof(loc), loc,
			nameof(slot), slot.getId(),
			nameof(value), value);
	}

	internal sealed class Self : Expr, ToData<Self> {
		[ParentPointer] internal readonly Ty _ty; // Class this method was defined in, plus selfEffect of the method.
		internal Self(Loc loc, Ty ty) : base(loc) { _ty = ty; }

		internal override IEnumerable<Expr> children() => noChildren;
		internal override Ty ty => _ty;

		public override bool deepEqual(Expr e) => e is Self s && deepEqual(s);
		public bool deepEqual(Self s) {
			assert(ty.equalsId<Ty, TyId>(s.ty));
			return locEq(s);
		}
		public override Dat toDat() => Dat.of(this, nameof(loc), loc);
	}

	internal sealed class Assert : Expr, ToData<Assert> {
		internal readonly Expr asserted;
		internal Assert(Loc loc, Expr asserted) : base(loc) {
			this.asserted = asserted;
			asserted.parent = this;
		}

		internal override IEnumerable<Expr> children() { yield return asserted; }
		internal override Ty ty => Ty.Void;

		public override bool deepEqual(Expr e) => e is Assert a && deepEqual(a);
		public bool deepEqual(Assert a) => locEq(a) && asserted.deepEqual(a.asserted);
		public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(asserted), asserted);
	}

	internal sealed class Recur : Expr, ToData<Recur> {
		[ParentPointer] internal readonly MethodOrImpl recurseTo;
		internal readonly Arr<Expr> args;
		internal Recur(Loc loc, MethodOrImpl recurseTo, Arr<Expr> args) : base(loc) {
			this.recurseTo = recurseTo;
			this.args = args;
			foreach (var arg in args)
				arg.parent = this;
		}

		internal override IEnumerable<Expr> children() => args;
		internal override Ty ty => recurseTo.implementedMethod.returnTy;

		public override bool deepEqual(Expr e) => e is Recur && deepEqual(e);
		public bool deepEqual(Recur r) => locEq(r) && args.deepEqual(r.args);
		public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(args), Dat.arr(args));
	}
}
