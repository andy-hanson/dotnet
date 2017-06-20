using static Utils;

namespace Model {
	abstract class Expr : M, ToData<Expr> {
		internal readonly Loc loc;
		internal abstract Ty ty { get; }
		Expr(Loc loc) {
			this.loc = loc;
		}

		public abstract bool deepEqual(Expr e);
		public abstract Dat toDat();

		protected bool locEq(Expr e) => loc.Equals(e.loc);

		internal sealed class AccessParameter : Expr, ToData<AccessParameter> {
			internal readonly Method.MethodWithBody.Parameter param;
			internal AccessParameter(Loc loc, Method.MethodWithBody.Parameter param) : base(loc) {
				this.param = param;
			}

			internal override Ty ty => param.ty;

			public override bool deepEqual(Expr e) => e is AccessParameter a && Equals(a);
			public bool deepEqual(AccessParameter a) => locEq(a) && a.param.Equals(param);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(param), param);
		}

		internal sealed class AccessLocal : Expr, ToData<AccessLocal> {
			internal readonly Pattern.Single local;
			internal AccessLocal(Loc loc, Pattern.Single local) : base(loc) {
				this.local = local;
			}

			internal override Ty ty => local.ty;

			public override bool deepEqual(Expr e) => e is AccessLocal a && Equals(a);
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
				this.then = then;
			}

			internal override Ty ty => then.ty;

			public override bool deepEqual(Expr e) => e is Let l && Equals(l);
			public bool deepEqual(Let l) => locEq(l) && value.Equals(l.value) && then.Equals(l.then);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(value), value, nameof(then), then);
		}

		internal sealed class Seq : Expr, ToData<Seq> {
			internal readonly Expr action;
			internal readonly Expr then;

			internal Seq(Loc loc, Expr action, Expr then) : base(loc) {
				this.action = action;
				this.then = then;
			}

			internal override Ty ty => GetTy();

			Ty GetTy() {
				return then.ty;
			}

			public override bool deepEqual(Expr e) => e is Seq s && Equals(s);
			public bool deepEqual(Seq s) => locEq(s) && action.Equals(s.action) && then.Equals(s.then);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(action), action, nameof(then), then);
		}

		internal sealed class Literal : Expr, ToData<Literal> {
			internal readonly LiteralValue value;
			internal Literal(Loc loc, LiteralValue value) : base(loc) { this.value = value; }
			internal override Ty ty => value.ty;

			internal abstract class LiteralValue : ToData<LiteralValue> {
				internal abstract Ty ty { get; }
				LiteralValue() {}
				public abstract bool deepEqual(LiteralValue l);
				public abstract Dat toDat();

				internal sealed class Pass : LiteralValue, ToData<Pass> {
					private Pass() {}
					internal static readonly Pass instance = new Pass();
					internal override Ty ty => BuiltinClass.Void;
					public override bool deepEqual(LiteralValue l) => l == instance;
					public bool deepEqual(Pass p) => true;
					public override Dat toDat() => Dat.of(this);
				}

				internal sealed class Bool : LiteralValue, ToData<Bool> {
					internal readonly bool value;
					internal Bool(bool value) { this.value = value; }
					internal override Ty ty => BuiltinClass.Bool;

					public override bool deepEqual(LiteralValue l) => l is Bool b && Equals(b);
					public bool deepEqual(Bool b) => value == b.value;
					public override Dat toDat() => Dat.boolean(value);
				}

				internal sealed class Int : LiteralValue {
					internal readonly int value;
					internal Int(int value) { this.value = value; }
					internal override Ty ty => BuiltinClass.Int;

					public override bool deepEqual(LiteralValue l) => l is Int i && Equals(i);
					public bool deepEqual(Int i) => value == i.value;
					public override Dat toDat() => Dat.inum(value);
				}

				internal sealed class Float : LiteralValue {
					internal readonly double value;
					internal Float(double value) { this.value = value; }
					internal override Ty ty => BuiltinClass.Float;

					public override bool deepEqual(LiteralValue l) => l is Float f && Equals(f);
					public bool deepEqual(Float f) => value == f.value;
					public override Dat toDat() => Dat.floatDat(value);
				}

				internal sealed class Str : LiteralValue {
					internal readonly string value;
					internal Str(string value) { this.value = value; }
					internal override Ty ty => BuiltinClass.Str;

					public override bool deepEqual(LiteralValue l) => l is Str s && Equals(s);
					public bool deepEqual(Str s) => value == s.value;
					public override Dat toDat() => Dat.str(value);
				}
			}

			public override bool deepEqual(Expr e) => e is Literal l && Equals(l);
			public bool deepEqual(Literal l) => locEq(l) && value.Equals(l.value);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(value), value);
		}

		internal sealed class WhenTest : Expr, ToData<WhenTest> {
			internal readonly Arr<Case> cases;
			internal readonly Expr elseResult;
			[NotData] internal readonly Ty _ty; // Cached ommon type of all cases and elseResult.
			internal WhenTest(Loc loc, Arr<Case> cases, Expr elseResult, Ty ty) : base(loc) { this.cases = cases; this.elseResult = elseResult; this._ty = ty; }

			override internal Ty ty => ty;

			public override bool deepEqual(Expr e) => e is WhenTest w && Equals(w);
			public bool deepEqual(WhenTest w) => cases.deepEqual(w.cases) && elseResult.Equals(w.elseResult);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(cases), Dat.arr(cases), nameof(elseResult), elseResult);

			internal struct Case : ToData<Case> {
				internal readonly Loc loc;
				internal readonly Expr test;
				internal readonly Expr result;
				internal Case(Loc loc, Expr test, Expr result) { this.loc = loc; this.test = test; this.result = result; }

				public bool deepEqual(Case c) => loc.Equals(c.loc) && test.Equals(c.test) && result.Equals(c.result);
				public Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(test), test, nameof(result), result);
			}
		}

		internal sealed class StaticMethodCall : Expr, ToData<StaticMethodCall> {
			[UpPointer] internal readonly Method method;
			internal readonly Arr<Expr> args;
			internal StaticMethodCall(Loc loc, Method method, Arr<Expr> args) : base(loc) {
				assert(method.isStatic);
				this.method = method;
				this.args = args;
			}

			internal override Ty ty => method.returnTy;

			public override bool deepEqual(Expr e) => e is StaticMethodCall s && Equals(s);
			public bool deepEqual(StaticMethodCall s) => method.equalsId<Method, Method.Id>(s.method) && args.deepEqual(s.args);
			public override Dat toDat() => Dat.of(this, nameof(method), method.getId(), nameof(args), Dat.arr(args));
		}

		internal sealed class MethodCall : Expr, ToData<MethodCall> {
			internal readonly Expr target;
			[UpPointer] internal readonly Method method;
			internal readonly Arr<Expr> args;
			internal MethodCall(Loc loc, Expr target, Method method, Arr<Expr> args) : base(loc) {
				assert(!method.isStatic);
				this.target = target;
				this.method = method;
				this.args = args;
			}

			internal override Ty ty => method.returnTy;

			public override bool deepEqual(Expr e) => e is MethodCall m && Equals(m);
			public bool deepEqual(MethodCall m) => target.Equals(m.target) && method.equalsId<Method, Method.Id>(m.method) && args.deepEqual(m.args);
			public override Dat toDat() => Dat.of(this, nameof(target), target, nameof(method), method.getId(), nameof(args), Dat.arr(args));
		}

		//Note: this contains a pointer to the current class for convenience.
		//Note: this should only happen in a non-static method. Otherwise we have Expr.Error
		internal sealed class GetMySlot : Expr, ToData<GetMySlot> {
			[ParentPointer] internal readonly Klass klass; // Class of the method this expression is in.
			[UpPointer] internal readonly Klass.Head.Slots.Slot slot;
			internal GetMySlot(Loc loc, Klass klass, Klass.Head.Slots.Slot slot) : base(loc) {
				this.klass = klass;
				this.slot = slot;
			}
			internal override Ty ty => slot.ty;

			public override bool deepEqual(Expr e) => e is GetMySlot g && Equals(g);
			public bool deepEqual(GetMySlot g) => slot.equalsId<Klass.Head.Slots.Slot, Klass.Head.Slots.Slot.Id>(g.slot);
			public override Dat toDat() => Dat.of(this, nameof(slot), slot.getId());
		}

		internal sealed class GetSlot : Expr, ToData<GetSlot> {
			[UpPointer] internal readonly Klass.Head.Slots.Slot slot;
			internal readonly Expr target;
			internal GetSlot(Loc loc, Expr target, Klass.Head.Slots.Slot slot) : base(loc) {
				this.target = target;
				this.slot = slot;
			}
			internal override Ty ty => slot.ty;

			public override bool deepEqual(Expr e) => e is GetSlot g && Equals(g);
			public bool deepEqual(GetSlot g) => throw TODO(); // TODO: handle "slot" up-pointer
			public override Dat toDat() => throw TODO();
		}

		internal sealed class Self : Expr, ToData<Self> {
			[ParentPointer] internal readonly Klass klass; // Pointer to the class this appears in.
			internal Self(Loc loc, Klass klass) : base(loc) { this.klass = klass; }

			internal override Ty ty => klass;

			public override bool deepEqual(Expr e) => e is Self s && Equals(s);
			public bool deepEqual(Self s) {
				assert(klass.equalsId<ClassLike, ClassLike.Id>(s.klass));
				return locEq(s);
			}
			public override Dat toDat() => Dat.of(this, nameof(loc), loc);
		}

		/*internal sealed class GetMethod : Get {
			internal readonly NzMethod method;
			internal readonly Expr target;
			internal GetMethod(Loc loc, Expr target, MethodWithBody method) : base(loc, target) {
				assert(!method.isStatic);
				this.target = target;
				this.method = method;
			}
			internal override Ty ty => TODO(); //Function type for the method
		}*/

		/*internal sealed class GetStaticMethod : Expr {
			internal readonly NzMethod method;
			internal GetStaticMethod(Loc loc, MethodWithBody method) : base(loc) {
				assert(method.isStatic);
				this.method = method;
			}
			internal override Ty ty => TODO();
		}*/
	}
}