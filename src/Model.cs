using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

using static Utils;

namespace Model {
	//TODO: actual types
	abstract class Ty {
		internal abstract Type toType();
	}

	sealed class Module {

		internal readonly string source;

		internal Module(Path path, bool isMain, string source) {
			this.source = source;
		}

		private ImmutableArray<Module> _imports;
		internal ImmutableArray<Module> imports { get { return nonNull(_imports); } set { _imports = value; } }

		bool importsAreResolved => _imports != null;

		private Klass _klass;
		internal Klass klass { get { return nonNull(_klass); } set { _klass = value; } }

		Sym name => klass.name;
	}

	abstract class ClassLike { }

	sealed class Klass {
		internal readonly Loc loc;
		internal readonly Sym name;

		internal Klass(Loc loc, Sym name) {
			this.loc = loc;
			this.name = name;
		}

		Head _head;
		internal Head head { get { return nonNull(_head); } set { _head = value; } }

		private ImmutableDictionary<Sym, Member> _membersMap;
		internal ImmutableDictionary<Sym, Member> membersMap {
			private get { return nonNull(_membersMap); }
			set { _membersMap = value; }
		}

		internal Member this[Sym name] => membersMap[name];

		internal IEnumerable<Member> members => membersMap.Values;

		//internal IEnumerable<MethodWithBody> methods ...

		internal abstract class Head {
			private Head() { }
			//TODO: isType, isGeneric

			internal class Slots : Head {
				internal readonly Loc loc;
				internal readonly ImmutableArray<Slot> slots;
				internal Slots(Loc loc, ImmutableArray<Slot> slots) {
					this.loc = loc;
					this.slots = slots;
				}
			}
		}
	}

	abstract class Member {
		internal abstract Sym name { get; }
		internal abstract Loc loc { get; }
	}

	abstract class NzMethod : Member {
		internal readonly ClassLike klass;
		private readonly Loc _loc;
		internal readonly bool isStatic;//TODO: just store static methods elsewhere?
		internal readonly Ty returnTy;
		readonly Sym _name;
		internal readonly ImmutableArray<Parameter> parameters;

		internal NzMethod(ClassLike klass, Loc loc, bool isStatic, Ty returnTy, Sym name, ImmutableArray<Parameter> parameters) {
			this.klass = klass;
			this._loc = loc;
			this.isStatic = isStatic;
			this.returnTy = returnTy;
			this._name = name;
			this.parameters = parameters;
		}

		internal override Loc loc => _loc;
		internal override Sym name => _name;

		int arity => parameters.Length;

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

		Expr _body;
		internal Expr body { get { return nonNull(_body); } set { _body = value; } }
	}
	//BuiltinMethod, MethodWithBody

	sealed class Slot {
		internal readonly ClassLike klass;
		internal readonly Loc loc;
		internal readonly bool mutable;
		internal readonly Ty ty;
		internal readonly Sym name;

		Slot(ClassLike klass, Loc loc, bool mutable, Ty ty, Sym name) {
			this.klass = klass;
			this.loc = loc;
			this.mutable = mutable;
			this.ty = ty;
			this.name = name;
		}
	}

	enum ExprKind {
		Access,
		Let,
		Seq,
	}

	abstract class Expr {
		internal ExprKind kind { get; }
		internal readonly Loc loc;
		internal abstract Ty ty { get; }
		protected Expr(Loc loc) {
			this.loc = loc;
		}
	}

	abstract class Pattern {
		readonly Loc loc;
		private Pattern(Loc loc) { this.loc = loc; }

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


	abstract class Access : Expr {
		internal abstract Sym name { get; }
		private Access(Loc loc) : base(loc) { }

		//class Parameter : Access {
		//    constructor(Loc loc) : base(loc) {
		//    }
		//}

		internal sealed class Local : Access {
			readonly Pattern.Single local;
			internal Local(Loc loc, Pattern.Single local) : base(loc) {
				this.local = local;
			}

			internal override Ty ty => local.ty;
			internal override Sym name => local.name;
		}
	}

	sealed class Let : Expr {
		internal readonly Expr value;
		internal readonly Expr then;

		internal Let(Loc loc, Pattern assigned, Expr value, Expr then) : base(loc) {
			this.value = value;
			this.then = then;
		}

		internal override Ty ty => then.ty;
	}

	sealed class Seq : Expr {
		internal readonly Expr action;
		internal readonly Expr then;

		internal Seq(Loc loc, Expr action, Expr then) : base(loc) {
			this.action = action;
			this.then = then;
		}

		internal override Ty ty => GetTy();

		private Ty GetTy() {
			return then.ty;
		}
	}

	abstract class LiteralValue { /*...*/ }

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
