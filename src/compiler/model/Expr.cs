using static Utils;

namespace Model {
	abstract class Expr {
		internal readonly Loc loc;
		internal abstract Ty ty { get; }
		Expr(Loc loc) {
			this.loc = loc;
		}

		internal sealed class AccessParameter : Expr {
			internal readonly Method.MethodWithBody.Parameter param;
			internal AccessParameter(Loc loc, Method.MethodWithBody.Parameter param) : base(loc) {
				this.param = param;
			}

			internal override Ty ty => param.ty;
		}

		internal sealed class AccessLocal : Expr {
			internal readonly Pattern.Single local;
			internal AccessLocal(Loc loc, Pattern.Single local) : base(loc) {
				this.local = local;
			}

			internal override Ty ty => local.ty;
		}

		internal sealed class Let : Expr {
			internal readonly Expr value;
			internal readonly Expr then;

			internal Let(Loc loc, Pattern assigned, Expr value, Expr then) : base(loc) {
				this.value = value;
				this.then = then;
			}

			internal override Ty ty => then.ty;
		}

		internal sealed class Seq : Expr {
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
		}

		internal sealed class Literal : Expr {
			internal readonly LiteralValue value;
			internal Literal(Loc loc, LiteralValue value) : base(loc) { this.value = value; }
			internal override Ty ty => value.ty;

			internal abstract class LiteralValue {
				internal abstract Ty ty { get; }
				LiteralValue() {}

				internal sealed class Pass : LiteralValue {
					private Pass() {}
					internal static readonly Pass instance = new Pass();
					internal override Ty ty => BuiltinClass.Void;
				}

				internal sealed class Bool : LiteralValue {
					internal readonly bool value;
					internal Bool(bool value) { this.value = value; }
					internal override Ty ty => BuiltinClass.Bool;
				}

				internal sealed class Int : LiteralValue {
					internal readonly int value;
					internal Int(int value) { this.value = value; }
					internal override Ty ty => BuiltinClass.Int;
				}

				internal sealed class Float : LiteralValue {
					internal readonly double value;
					internal Float(double value) { this.value = value; }
					internal override Ty ty => BuiltinClass.Float;
				}

				internal sealed class Str : LiteralValue {
					internal readonly string value;
					internal Str(string value) { this.value = value; }
					internal override Ty ty => BuiltinClass.Str;
				}
			}
		}

		internal sealed class WhenTest : Expr {
			internal readonly Arr<Case> cases;
			internal readonly Expr elseResult;
			internal readonly Ty _ty; // Common type of all cases and elseResult.
			internal WhenTest(Loc loc, Arr<Case> cases, Expr elseResult, Ty ty) : base(loc) { this.cases = cases; this.elseResult = elseResult; this._ty = ty; }

			internal struct Case {
				internal readonly Loc loc;
				internal readonly Expr test;
				internal readonly Expr result;
				internal Case(Loc loc, Expr test, Expr result) { this.loc = loc; this.test = test; this.result = result; }
			}

			override internal Ty ty => ty;
		}

		internal sealed class StaticMethodCall : Expr {
			internal readonly Method method;
			internal readonly Arr<Expr> args;
			internal StaticMethodCall(Loc loc, Method method, Arr<Expr> args) : base(loc) {
				assert(method.isStatic);
				this.method = method;
				this.args = args;
			}

			internal override Ty ty => method.returnTy;
		}

		internal sealed class MethodCall : Expr {
			internal readonly Expr target;
			internal readonly Method method;
			internal readonly Arr<Expr> args;
			internal MethodCall(Loc loc, Expr target, Method method, Arr<Expr> args) : base(loc) {
				assert(!method.isStatic);
				this.target = target;
				this.method = method;
				this.args = args;
			}

			internal override Ty ty => method.returnTy;
		}

		//Note: this contains a pointer to the current class for convenience.
		//Note: this should only happen in a non-static method. Otherwise we have Expr.Error
		internal sealed class GetMySlot : Expr {
			internal readonly Klass klass; //This is just here for convenience. It *must* be the class of the method this expression is in.
			internal readonly Klass.Head.Slots.Slot slot;
			internal GetMySlot(Loc loc, Klass klass, Klass.Head.Slots.Slot slot) : base(loc) {
				this.klass = klass;
				this.slot = slot;
			}
			internal override Ty ty => slot.ty;
		}

		internal sealed class GetSlot : Expr {
			internal readonly Klass.Head.Slots.Slot slot;
			internal readonly Expr target;
			internal GetSlot(Loc loc, Expr target, Klass.Head.Slots.Slot slot) : base(loc) {
				this.target = target;
				this.slot = slot;
			}
			internal override Ty ty => slot.ty;
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