using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

using static Utils;

namespace Model {
	//TODO: actual types
	abstract class Ty {
		public abstract Type toType();
	}

	sealed class Module {

		public readonly string source;

		public Module(Path path, bool isMain, string source) {
			this.source = source;
		}

		private ImmutableArray<Module> _imports;
		public ImmutableArray<Module> imports { get { return nonNull(_imports); } set { _imports = value; } }

		bool importsAreResolved => _imports != null;

		private Klass _klass;
		public Klass klass { get { return nonNull(_klass); } set { _klass = value; } }

		Sym name => klass.name;
	}

	abstract class ClassLike { }

	sealed class Klass {
		public readonly Loc loc;
		public readonly Sym name;

		public Klass(Loc loc, Sym name) {
			this.loc = loc;
			this.name = name;
		}

		Head _head;
		public Head head { get { return nonNull(_head); } set { _head = value; } }

		private ImmutableDictionary<Sym, Member> _membersMap;
		public ImmutableDictionary<Sym, Member> membersMap {
			private get { return nonNull(_membersMap); }
			set { _membersMap = value; }
		}

		public Member this[Sym name] => membersMap[name];

		public IEnumerable<Member> members => membersMap.Values;

		//public IEnumerable<MethodWithBody> methods ...

		public abstract class Head {
			private Head() { }
			//TODO: isType, isGeneric

			public class Slots : Head {
				public readonly Loc loc;
				public readonly ImmutableArray<Slot> slots;
				public Slots(Loc loc, ImmutableArray<Slot> slots) {
					this.loc = loc;
					this.slots = slots;
				}
			}
		}
	}

	abstract class Member {
		public abstract Sym name { get; }
		public abstract Loc loc { get; }
	}

	abstract class NzMethod : Member {
		public readonly ClassLike klass;
		private readonly Loc _loc;
		public readonly bool isStatic;//TODO: just store static methods elsewhere?
		public readonly Ty returnTy;
		readonly Sym _name;
		public readonly ImmutableArray<Parameter> parameters;

		public NzMethod(ClassLike klass, Loc loc, bool isStatic, Ty returnTy, Sym name, ImmutableArray<Parameter> parameters) {
			this.klass = klass;
			this._loc = loc;
			this.isStatic = isStatic;
			this.returnTy = returnTy;
			this._name = name;
			this.parameters = parameters;
		}

		public override Loc loc => _loc;
		public override Sym name => _name;

		int arity => parameters.Length;

		public sealed class Parameter {
			public readonly Loc loc;
			public readonly Ty ty;
			public readonly Sym name;

			public Parameter(Loc loc, Ty ty, Sym name) {
				this.loc = loc;
				this.ty = ty;
				this.name = name;
			}
		}
	}
	sealed class MethodWithBody : NzMethod {
		public MethodWithBody(ClassLike klass, Loc loc, bool isStatic, Ty returnTy, Sym name, ImmutableArray<Parameter> parameters)
			: base(klass, loc, isStatic, returnTy, name, parameters) { }

		Expr _body;
		public Expr body { get { return nonNull(_body); } set { _body = value; } }
	}
	//BuiltinMethod, MethodWithBody

	sealed class Slot {
		public readonly ClassLike klass;
		public readonly Loc loc;
		public readonly bool mutable;
		public readonly Ty ty;
		public readonly Sym name;

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
		public ExprKind kind { get; }
		public readonly Loc loc;
		public abstract Ty ty { get; }
		protected Expr(Loc loc) {
			this.loc = loc;
		}
	}

	abstract class Pattern {
		readonly Loc loc;
		private Pattern(Loc loc) { this.loc = loc; }

		public sealed class Ignore : Pattern {
			public Ignore(Loc loc) : base(loc) { }
		}
		public sealed class Single : Pattern {
			public readonly Ty ty;
			public readonly Sym name;
			public Single(Loc loc, Ty ty, Sym name) : base(loc) {
				this.ty = ty;
				this.name = name;
			}
		}
		public sealed class Destruct : Pattern {
			public readonly ImmutableArray<Pattern> destructuredInto;
			public Destruct(Loc loc, ImmutableArray<Pattern> destructuredInto) : base(loc) {
				this.destructuredInto = destructuredInto;
			}
		}
	}


	abstract class Access : Expr {
		public abstract Sym name { get; }
		private Access(Loc loc) : base(loc) { }

		//class Parameter : Access {
		//    constructor(Loc loc) : base(loc) {
		//    }
		//}

		public sealed class Local : Access {
			readonly Pattern.Single local;
			public Local(Loc loc, Pattern.Single local) : base(loc) {
				this.local = local;
			}

			public override Ty ty => local.ty;
			public override Sym name => local.name;
		}
	}

	sealed class Let : Expr {
		public readonly Expr value;
		public readonly Expr then;

		public Let(Loc loc, Pattern assigned, Expr value, Expr then) : base(loc) {
			this.value = value;
			this.then = then;
		}

		public override Ty ty => then.ty;
	}

	sealed class Seq : Expr {
		public readonly Expr action;
		public readonly Expr then;

		public Seq(Loc loc, Expr action, Expr then) : base(loc) {
			this.action = action;
			this.then = then;
		}

		public override Ty ty => GetTy();

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
		public GetSlot(Loc loc, Expr target, Slot slot) : base(loc) {
			assert(!slot.mutable);
			this.target = target;
			this.slot = slot;
		}
	}*/
}
