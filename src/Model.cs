using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using static Utils;

namespace Model {
	//TODO: actual types
	abstract class Ty : IEquatable<Ty> {
		internal abstract Type toType();
		public override bool Equals(object o) {
			var t = o as Ty;
			return t != null && Equals(t);
		}
		public abstract bool Equals(Ty ty);
		public override abstract int GetHashCode();
		public static bool operator ==(Ty a, Ty b) => a.Equals(b);
		public static bool operator !=(Ty a, Ty b) => !a.Equals(b);
	}

	sealed class Module {
		internal readonly Path path;
		internal readonly bool isMain;
		internal readonly string source;

		internal Module(Path path, bool isMain, string source) {
			this.path = path;
			this.isMain = isMain;
			this.source = source;
		}

		internal Path fullPath => Compiler.fullPath(path, isMain);

		ImmutableArray<Module>? _imports;
		internal ImmutableArray<Module> imports {
			get { return _imports.Value; }
			set { assert(!_imports.HasValue); _imports = value; }
		}

		internal bool importsAreResolved => _imports != null;

		Op<Klass> _klass;
		internal Klass klass {
			get { return _klass.force; }
			set { assert(!_klass.has); _klass = Op.Some(value); }
		}

		internal Sym name => klass.name;
	}

	abstract class ClassLike : Ty {
		internal abstract ImmutableDictionary<Sym, Member> membersMap { get; }
	}

	sealed class Klass : ClassLike {
		internal readonly Loc loc;
		internal readonly Sym name;

		internal override Type toType() => throw TODO();

		internal Klass(Loc loc, Sym name) {
			this.loc = loc;
			this.name = name;
		}

		Op<Head> _head;
		internal Head head {
			get { return _head.force; }
			set { assert(!_head.has); _head = Op.Some(value); }
		}

		Op<ImmutableDictionary<Sym, Member>> _membersMap;
		internal override ImmutableDictionary<Sym, Member> membersMap => _membersMap.force;
		internal void setMembersMap(ImmutableDictionary<Sym, Member> membersMap) { assert(!_membersMap.has); _membersMap = Op.Some(membersMap); }

		internal IEnumerable<Member> members => membersMap.Values;

		//internal IEnumerable<MethodWithBody> methods ...

		internal abstract class Head {
			Head() { }
			//TODO: isType, isGeneric

			internal class Slots : Head {
				internal readonly Loc loc;
				internal readonly ImmutableArray<Slot> slots;
				internal Slots(Loc loc, ImmutableArray<Slot> slots) {
					this.loc = loc;
					this.slots = slots;
				}

				internal sealed class Slot : Member {
					internal readonly Klass klass;
					internal readonly bool mutable;
					internal readonly Ty ty;

					internal Slot(Klass klass, Loc loc, bool mutable, Ty ty, Sym name) : base(loc, name) {
						this.klass = klass;
						this.mutable = mutable;
						this.ty = ty;
					}
				}
			}
		}

		public override bool Equals(Ty ty) => object.ReferenceEquals(this, ty);
		public override int GetHashCode() => name.GetHashCode();
	}

	abstract class Member {
		internal readonly Loc loc;
		internal readonly Sym name;
		internal Member(Loc loc, Sym name) { this.loc = loc; this.name = name; }
	}

	abstract class NzMethod : Member {
		internal readonly ClassLike klass;
		internal readonly bool isStatic;//TODO: just store static methods elsewhere?
		internal readonly Ty returnTy;
		internal readonly ImmutableArray<Parameter> parameters;

		internal NzMethod(ClassLike klass, Loc loc, bool isStatic, Ty returnTy, Sym name, ImmutableArray<Parameter> parameters) : base(loc, name) {
			this.klass = klass;
			this.isStatic = isStatic;
			this.returnTy = returnTy;
			this.parameters = parameters;
		}

		internal int arity => parameters.Length;

		internal sealed class Parameter {
			internal readonly Loc loc;
			internal readonly Ty ty;
			internal readonly Sym name;

			internal Parameter(Loc loc, Ty ty, Sym name) {
				this.loc = loc;
				this.ty = ty;
				this.name = name;
			}
		}
	}
	sealed class MethodWithBody : NzMethod {
		internal MethodWithBody(ClassLike klass, Loc loc, bool isStatic, Ty returnTy, Sym name, ImmutableArray<Parameter> parameters)
			: base(klass, loc, isStatic, returnTy, name, parameters) { }

		Op<Expr> _body;
		internal Expr body {
			get { return _body.force; }
			set { assert(!_body.has); _body = Op.Some(value); }
		}
	}
	//BuiltinMethod, MethodWithBody

	abstract class Pattern {
		readonly Loc loc;
		Pattern(Loc loc) { this.loc = loc; }

		internal sealed class Ignore : Pattern {
			internal Ignore(Loc loc) : base(loc) { }
		}
		internal sealed class Single : Pattern {
			internal readonly Ty ty;
			internal readonly Sym name;
			internal Single(Loc loc, Ty ty, Sym name) : base(loc) {
				this.ty = ty;
				this.name = name;
			}
		}
		internal sealed class Destruct : Pattern {
			internal readonly ImmutableArray<Pattern> destructuredInto;
			internal Destruct(Loc loc, ImmutableArray<Pattern> destructuredInto) : base(loc) {
				this.destructuredInto = destructuredInto;
			}
		}
	}

	abstract class Expr {
		internal readonly Loc loc;
		internal abstract Ty ty { get; }
		Expr(Loc loc) {
			this.loc = loc;
		}

		//TODO: don't think having a base class here helps...
		internal abstract class Access : Expr {
			internal abstract Sym name { get; }
			Access(Loc loc) : base(loc) { }

			internal sealed class Parameter : Access {
				internal readonly MethodWithBody.Parameter param;
				internal Parameter(Loc loc, MethodWithBody.Parameter param) : base(loc) {
					this.param = param;
				}

				internal override Ty ty => param.ty;
				internal override Sym name => param.name;
			}

			internal sealed class Local : Access {
				readonly Pattern.Single local;
				internal Local(Loc loc, Pattern.Single local) : base(loc) {
					this.local = local;
				}

				internal override Ty ty => local.ty;
				internal override Sym name => local.name;
			}
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

				internal sealed class Int : LiteralValue {
					internal readonly int value;
					internal Int(int value) { this.value = value; }
					internal override Ty ty => throw TODO();
				}

				internal sealed class Float : LiteralValue {
					internal readonly double value;
					internal Float(double value) { this.value = value; }
					internal override Ty ty => throw TODO();
				}

				internal sealed class Str : LiteralValue {
					internal readonly string value;
					internal Str(string value) { this.value = value; }
					internal override Ty ty => throw TODO();
				}
			}
		}

		internal sealed class StaticMethodCall : Expr {
			internal readonly NzMethod method;
			internal readonly ImmutableArray<Expr> args;
			internal StaticMethodCall(Loc loc, NzMethod method, ImmutableArray<Expr> args) : base(loc) {
				assert(method.isStatic);
				this.method = method;
				this.args = args;
			}

			internal override Ty ty => method.returnTy;
		}

		internal sealed class MethodCall : Expr {
			internal readonly Expr target;
			internal readonly NzMethod method;
			internal readonly ImmutableArray<Expr> args;
			internal MethodCall(Loc loc, Expr target, NzMethod method, ImmutableArray<Expr> args) : base(loc) {
				assert(!method.isStatic);
				this.target = target;
				this.method = method;
				this.args = args;
			}

			internal override Ty ty => method.returnTy;
		}

		//Share w/ GetSlot?
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

	//LiteralValue, Literal
	//StaticMethodCall, MethodCall

	/*sealed class GetSlot : Expr {
		readonly Expr target;
		readonly Slot slot;
		internal GetSlot(Loc loc, Expr target, Slot slot) : base(loc) {
			assert(!slot.mutable);
			this.target = target;
			this.slot = slot;
		}
	}*/
}
