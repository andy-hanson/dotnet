namespace Ast {
	abstract class Node : ToData<Node> {
		internal readonly Loc loc;
		internal Node(Loc loc) { this.loc = loc; }
		public abstract bool Equals(Node n);
		protected bool locEq(Node n) => loc.Equals(n.loc);
		public abstract Dat toDat();
	}

	sealed class Module : Node, ToData<Module> {
		internal readonly Arr<Import> imports;
		internal readonly Klass klass;

		internal Module(Loc loc, Arr<Import> imports, Klass klass) : base(loc) {
			this.imports = imports;
			this.klass = klass;
		}

		public override bool Equals(Node n) => n is Module m && Equals(m);
		public bool Equals(Module m) => locEq(m) && imports.eq(m.imports) && klass.Equals(m.klass);
		public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(imports), Dat.arr(imports), nameof(klass), klass);

		internal abstract class Import : Node, ToData<Import> {
			Import(Loc loc) : base(loc) {}
			public abstract bool Equals(Import o);

			internal sealed class Global : Import, ToData<Global> {
				internal readonly Path path;
				internal Global(Loc loc, Path path) : base(loc) { this.path = path; }
				public override bool Equals(Node n) => n is Global g && Equals(g);
				public override bool Equals(Import o) => o is Global g && Equals(g);
				public bool Equals(Global g) => locEq(g) && path == g.path;
				public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(path), path);
			}

			internal sealed class Relative : Import, ToData<Relative> {
				internal readonly RelPath path;
				internal Relative(Loc loc, RelPath path) : base(loc) { this.path = path; }
				public override bool Equals(Node n) => n is Relative r && Equals(r);
				public override bool Equals(Import o) => o is Relative r && Equals(r);
				public bool Equals(Relative r) => locEq(r) && path == r.path;
				public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(path), path);
			}
		}
	}

	sealed class Klass : Node, ToData<Klass> {
		internal readonly Head head;
		internal readonly Arr<Member> members;

		internal Klass(Loc loc, Head head, Arr<Member> members) : base(loc) {
			this.head = head;
			this.members = members;
		}

		public override bool Equals(Node n) => n is Klass k && Equals(k);
		public bool Equals(Klass k) => locEq(k) && head.Equals(k.head) && members.Equals(k.members);
		public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(head), head, nameof(members), Dat.arr(members));

		internal abstract class Head : Node, ToData<Head> {
			Head(Loc loc) : base(loc) {}

			public bool Equals(Head h) => Equals((Node) h);

			internal sealed class Static : Head, ToData<Static> {
				internal Static(Loc loc) : base(loc) {}
				public override bool Equals(Node n) => n is Static s && Equals(s);
				public bool Equals(Static s) => locEq(s);
				public override Dat toDat() => Dat.of(this, nameof(loc), loc);
			}

			internal sealed class Abstract : Head, ToData<Abstract> {
				internal Abstract(Loc loc) : base(loc) {}
				public override bool Equals(Node n) => n is Abstract a && Equals(a);
				public bool Equals(Abstract a) => locEq(a);
				public override Dat toDat() => Dat.of(this, nameof(loc), loc);
			}

			internal sealed class Slots : Head, ToData<Slots> {
				internal readonly Arr<Slot> slots;
				internal Slots(Loc loc, Arr<Slot> vars) : base(loc) { this.slots = vars; }

				public override bool Equals(Node n) => n is Slots s && Equals(s);
				public bool Equals(Slots s) => locEq(s) && slots.eq(s.slots);
				public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(slots), Dat.arr(slots));

				internal sealed class Slot : Node, ToData<Slot> {
					internal readonly bool mutable;
					internal readonly Ty ty;
					internal readonly Sym name;
					internal Slot(Loc loc, bool mutable, Ty ty, Sym name) : base(loc) {
						this.mutable = mutable;
						this.ty = ty;
						this.name = name;
					}

					public override bool Equals(Node n) => n is Slot s && Equals(s);
					public bool Equals(Slot s) => locEq(s) && mutable == s.mutable && ty.Equals(s.ty) && name.Equals(s.name);
					public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(mutable), Dat.boolean(mutable), nameof(ty), ty, nameof(name), name);
				}
			}
		}
	}

	abstract class Member : Node, ToData<Member> {
		internal readonly Sym name;
		Member(Loc loc, Sym name) : base(loc) { this.name = name; }

		public bool Equals(Member m) => Equals((Node) m);

		internal sealed class Method : Member, ToData<Method> {
			internal readonly bool isStatic;
			internal readonly Ty returnTy;
			internal readonly Arr<Parameter> parameters;
			internal readonly Expr body;
			internal Method(Loc loc, bool isStatic, Ty returnTy, Sym name, Arr<Parameter> parameters, Expr body) : base(loc, name) {
				this.isStatic = isStatic;
				this.returnTy = returnTy;
				this.parameters = parameters;
				this.body = body;
			}

			public override bool Equals(Node n) => n is Method m && Equals(m);
			public bool Equals(Method m) =>
				locEq(m) &&
				isStatic == m.isStatic &&
				returnTy.Equals(m.returnTy) &&
				parameters.eq(m.parameters) &&
				body.Equals(m.body);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(isStatic), Dat.boolean(isStatic), nameof(returnTy), returnTy, nameof(parameters), Dat.arr(parameters), nameof(body), body);

			internal sealed class Parameter : Node, ToData<Parameter> {
				internal readonly Ty ty;
				internal readonly Sym name;
				internal Parameter(Loc loc, Ty ty, Sym name) : base(loc) {
					this.ty = ty;
					this.name = name;
				}

				public override bool Equals(Node n) => n is Parameter p && Equals(p);
				public bool Equals(Parameter p) => locEq(p) && ty.Equals(p.ty) && name.Equals(p.name);
				public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(ty), ty, nameof(name), name);
			}
		}
	}

	abstract class Ty : Node, ToData<Ty> {
		Ty(Loc loc) : base(loc) {}
		public bool Equals(Ty ty) => Equals((Node) ty);

		internal sealed class Access : Ty, ToData<Access> {
			internal readonly Sym name;
			internal Access(Loc loc, Sym name) : base(loc) { this.name = name; }

			public override bool Equals(Node n) => n is Access a && Equals(a);
			public bool Equals(Access a) => locEq(a) && name.Equals(a.name);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(name), name);
		}

		internal sealed class Inst : Ty, ToData<Inst> {
			internal readonly Access instantiated;
			internal readonly Arr<Ty> tyArgs;
			internal Inst(Loc loc, Access instantiated, Arr<Ty> tyArgs) : base(loc) {
				this.instantiated = instantiated;
				this.tyArgs = tyArgs;
			}

			public override bool Equals(Node n) => n is Inst i && Equals(i);
			public bool Equals(Inst i) => locEq(i) && instantiated.Equals(i.instantiated) && tyArgs.eq(i.tyArgs);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(instantiated), instantiated, nameof(tyArgs), Dat.arr(tyArgs));
		}
	}

	abstract class Expr : Node, ToData<Expr> {
		Expr(Loc loc) : base(loc) {}
		// Node equality will cast to the right type anyway.
		public bool Equals(Expr e) => Equals((Node) e);

		internal sealed class Access : Expr, ToData<Access> {
			internal readonly Sym name;
			internal Access(Loc loc, Sym name) : base(loc) { this.name = name; }

			public override bool Equals(Node n) => n is Access a && Equals(a);
			public bool Equals(Access a) => locEq(a) && name.Equals(a.name);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(name), name);
		}

		internal sealed class StaticAccess : Expr, ToData<StaticAccess> {
			internal readonly Sym className;
			internal readonly Sym staticMethodName;
			internal StaticAccess(Loc loc, Sym className, Sym staticMethodName) : base(loc) {
				this.className = className;
				this.staticMethodName = staticMethodName;
			}

			public override bool Equals(Node n) => n is StaticAccess s && Equals(s);
			public bool Equals(StaticAccess s) => locEq(s) && className.Equals(s.className) && staticMethodName.Equals(s.staticMethodName);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(className), className, nameof(staticMethodName), staticMethodName);
		}

		internal sealed class OperatorCall : Expr, ToData<OperatorCall> {
			internal readonly Expr left;
			internal readonly Sym oper;
			internal readonly Expr right;
			internal OperatorCall(Loc loc, Expr left, Sym oper, Expr right) : base(loc) {
				this.left = left;
				this.oper = oper;
				this.right = right;
			}

			public override bool Equals(Node n) => n is OperatorCall o && Equals(o);
			public bool Equals(OperatorCall o) =>
				locEq(o) &&
				left.Equals(o.left) &&
				oper.Equals(o.oper) &&
				right.Equals(o.right);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(oper), oper, nameof(right), right);
		}

		internal sealed class Call : Expr, ToData<Call> {
			internal readonly Expr target;
			internal readonly Arr<Expr> args;
			internal Call(Loc loc, Expr target, Arr<Expr> args) : base(loc) {
				this.target = target;
				this.args = args;
			}

			public override bool Equals(Node n) => n is Call c && Equals(c);
			public bool Equals(Call c) => locEq(c) && target.Equals(c.target) && args.eq(c.args);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(target), target, nameof(args), Dat.arr(args));
		}

		internal sealed class GetProperty : Expr, ToData<GetProperty> {
			internal readonly Expr target;
			internal readonly Sym propertyName;
			internal GetProperty(Loc loc, Expr target, Sym propertyName) : base(loc) {
				this.target = target;
				this.propertyName = propertyName;
			}

			public override bool Equals(Node n) => n is GetProperty g && Equals(g);
			public bool Equals(GetProperty g) => locEq(g) && target.Equals(g.target) && propertyName.Equals(g.propertyName);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(target), target, nameof(propertyName), propertyName);
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

			public override bool Equals(Node n) => n is Let l && Equals(l);
			public bool Equals(Let l) =>
				locEq(l) &&
				assigned.Equals(l.assigned) &&
				value.Equals(l.value) &&
				then.Equals(l.then);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(assigned), assigned, nameof(value), value, nameof(then), then);
		}

		internal sealed class Seq : Expr, ToData<Seq> {
			internal readonly Expr first;
			internal readonly Expr then;
			internal Seq(Loc loc, Expr first, Expr then) : base(loc){
				this.first = first;
				this.then = then;
			}

			public override bool Equals(Node n) => n is Seq s && Equals(s);
			public bool Equals(Seq s) => locEq(s) && first.Equals(s.first) && then.Equals(s.then);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(first), first, nameof(then), then);
		}

		internal sealed class Literal : Expr, ToData<Literal> {
			internal readonly Model.Expr.Literal.LiteralValue value;
			internal Literal(Loc loc, Model.Expr.Literal.LiteralValue value) : base(loc) { this.value = value; }

			public override bool Equals(Node n) => n is Literal l && Equals(l);
			public bool Equals(Literal l) => locEq(l) && value.Equals(l.value);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(value), value);
		}

		internal sealed class WhenTest : Expr, ToData<WhenTest> {
			internal readonly Arr<Case> cases;
			internal readonly Expr elseResult;
			internal WhenTest(Loc loc, Arr<Case> cases, Expr elseResult) : base(loc) { this.cases = cases; this.elseResult = elseResult; }

			public override bool Equals(Node n) => n is WhenTest w && Equals(w);
			public bool Equals(WhenTest w) => locEq(w) && cases.eq(w.cases) && elseResult.Equals(w.elseResult);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(cases), Dat.arr(cases), nameof(elseResult), elseResult);

			internal struct Case : ToData<Case> {
				internal readonly Loc loc;
				internal readonly Expr test;
				internal readonly Expr result;
				internal Case(Loc loc, Expr test, Expr result) { this.loc = loc; this.test = test; this.result = result; }

				public bool Equals(Case c) => loc.Equals(c.loc) && test.Equals(c.test) && result.Equals(c.result);
				public Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(test), test, nameof(result), result);
			}
		}
	}

	internal abstract class Pattern : Node, ToData<Pattern> {
		Pattern(Loc loc) : base(loc) {}
		public bool Equals(Pattern p) => Equals((Node) p);

		internal sealed class Ignore : Pattern, ToData<Ignore> {
			internal Ignore(Loc loc) : base(loc) {}

			public override bool Equals(Node n) => n is Ignore i && Equals(i);
			public bool Equals(Ignore i) => locEq(i);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc);
		}

		internal sealed class Single : Pattern, ToData<Single> {
			internal readonly Sym name;
			internal Single(Loc loc, Sym name) : base(loc) { this.name = name; }

			public override bool Equals(Node n) => n is Single s && Equals(s);
			public bool Equals(Single s) => locEq(s) && name.Equals(s.name);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(name), name);
		}

		internal sealed class Destruct : Pattern, ToData<Destruct> {
			internal readonly Arr<Pattern> destructed;
			internal Destruct(Loc loc, Arr<Pattern> destructed) : base(loc) { this.destructed = destructed; }

			public override bool Equals(Node n) => n is Destruct d && Equals(d);
			public bool Equals(Destruct d) => locEq(d) && destructed.eq(d.destructed);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(destructed), Dat.arr(destructed));
		}
	}
}
