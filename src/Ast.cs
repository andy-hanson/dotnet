using System.Collections.Immutable;

namespace Ast {
	abstract class Node {
		internal readonly Loc loc;
		internal Node(Loc loc) { this.loc = loc; }
	}

	sealed class Module : Node {
		internal readonly ImmutableArray<Import> imports;
		internal readonly Klass klass;

		internal Module(Loc loc, ImmutableArray<Import> imports, Klass klass) : base(loc) {
			this.imports = imports;
			this.klass = klass;
		}

		internal abstract class Import : Node {
			Import(Loc loc) : base(loc) {}

			internal sealed class Global : Import {
				internal readonly Path path;
				internal Global(Loc loc, Path path) : base(loc) { this.path = path; }
			}

			internal sealed class Relative : Import {
				internal readonly RelPath path;
				internal Relative(Loc loc, RelPath path) : base(loc) { this.path = path; }
			}
		}
	}

	sealed class Klass : Node {
		internal readonly Sym name;
		internal readonly Head head;
		internal readonly ImmutableArray<Member> members;

		internal Klass(Loc loc, Sym name, Head head, ImmutableArray<Member> members) : base(loc) {
			this.name = name;
			this.head = head;
			this.members = members;
		}

		internal abstract class Head : Node {
			Head(Loc loc) : base(loc) {}

			internal sealed class Slots : Head {
				internal readonly ImmutableArray<Slot> slots;
				internal Slots(Loc loc, ImmutableArray<Slot> vars) : base(loc) { this.slots = vars; }

				internal sealed class Slot : Node {
					internal readonly bool mutable;
					internal readonly Ty ty;
					internal readonly Sym name;
					internal Slot(Loc loc, bool mutable, Ty ty, Sym name) : base(loc) {
						this.mutable = mutable;
						this.ty = ty;
						this.name = name;
					}
				}
			}
		}
	}

	abstract class Member : Node {
		internal readonly Sym name;
		Member(Loc loc, Sym name) : base(loc) { this.name = name; }

		internal sealed class Method : Member {
			internal readonly bool isStatic;
			internal readonly Ty returnTy;
			internal readonly ImmutableArray<Parameter> parameters;
			internal readonly Expr body;
			internal Method(Loc loc, bool isStatic, Ty returnTy, Sym name, ImmutableArray<Parameter> parameters, Expr body) : base(loc, name) {
				this.isStatic = isStatic;
				this.returnTy = returnTy;
				this.parameters = parameters;
				this.body = body;
			}

			internal sealed class Parameter : Node {
				internal readonly Ty ty;
				internal readonly Sym name;
				internal Parameter(Loc loc, Ty ty, Sym name) : base(loc) {
					this.ty = ty;
					this.name = name;
				}
			}
		}
	}

	abstract class Ty : Node {
		Ty(Loc loc) : base(loc) {}

		internal sealed class Access : Ty {
			internal readonly Sym name;
			internal Access(Loc loc, Sym name) : base(loc) { this.name = name; }
		}

		internal sealed class Inst : Ty {
			internal readonly Access instantiated;
			internal readonly ImmutableArray<Ty> tyArgs;
			internal Inst(Loc loc, Access instantiated, ImmutableArray<Ty> tyArgs) : base(loc) {
				this.instantiated = instantiated;
				this.tyArgs = tyArgs;
			}
		}
	}

	abstract class Expr : Node {
		Expr(Loc loc) : base(loc) {}

		internal sealed class Access : Expr {
			internal readonly Sym name;
			internal Access(Loc loc, Sym name) : base(loc) { this.name = name; }
		}

		internal sealed class StaticAccess : Expr {
			internal readonly Sym className;
			internal readonly Sym staticMethodName;
			internal StaticAccess(Loc loc, Sym className, Sym staticMethodName) : base(loc) {
				this.className = className;
				this.staticMethodName = staticMethodName;
			}
		}

		internal sealed class OperatorCall : Expr {
			internal readonly Expr left;
			internal readonly Sym oper;
			internal readonly Expr right;
			internal OperatorCall(Loc loc, Expr left, Sym oper, Expr right) : base(loc) {
				this.left = left;
				this.oper = oper;
				this.right = right;
			}
		}

		internal sealed class Call : Expr {
			internal readonly Expr target;
			internal readonly ImmutableArray<Expr> args;
			internal Call(Loc loc, Expr target, ImmutableArray<Expr> args) : base(loc) {
				this.target = target;
				this.args = args;
			}
		}

		internal sealed class GetProperty : Expr {
			internal readonly Expr target;
			internal readonly Sym propertyName;
			internal GetProperty(Loc loc, Expr target, Sym propertyName) : base(loc) {
				this.target = target;
				this.propertyName = propertyName;
			}
		}

		internal sealed class Let : Expr {
			internal readonly Pattern assigned;
			internal readonly Expr value;
			internal readonly Expr then;
			internal Let(Loc loc, Pattern assigned, Expr value, Expr then) : base(loc) {
				this.assigned = assigned;
				this.value = value;
				this.then = then;
			}
		}

		internal class Seq : Expr {
			internal readonly Expr first;
			internal readonly Expr then;
			internal Seq(Loc loc, Expr first, Expr then) : base(loc){
				this.first = first;
				this.then = then;
			}
		}

		internal class Literal : Expr {
			internal readonly Model.Expr.Literal.LiteralValue value;
			internal Literal(Loc loc, Model.Expr.Literal.LiteralValue value) : base(loc) { this.value = value; }
		}
	}

	internal abstract class Pattern : Node {
		Pattern(Loc loc) : base(loc) {}
		
		internal sealed class Ignore : Pattern {
			internal Ignore(Loc loc) : base(loc) {}
		}
		
		internal sealed class Single : Pattern {
			internal readonly Sym name;
			internal Single(Loc loc, Sym name) : base(loc) { this.name = name; }
		}
		
		internal sealed class Destruct : Pattern {
			internal readonly ImmutableArray<Pattern> destructed;
			internal Destruct(Loc loc, ImmutableArray<Pattern> destructed) : base(loc) {
				this.destructed = destructed;
			}
		}
	}
}
