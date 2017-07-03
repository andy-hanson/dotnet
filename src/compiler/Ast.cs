using System;

namespace Ast {
	abstract class Node : ToData<Node> {
		internal readonly Loc loc;
		protected Node(Loc loc) { this.loc = loc; }
		public sealed override bool Equals(object o) => throw new NotSupportedException();
		public sealed override int GetHashCode() => throw new NotSupportedException();
		public abstract bool deepEqual(Node n);
		protected bool locEq(Node n) => loc.deepEqual(n.loc);
		public abstract Dat toDat();
	}

	sealed class Module : Node, ToData<Module> {
		internal readonly Arr<Import> imports;
		internal readonly Klass klass;

		internal Module(Loc loc, Arr<Import> imports, Klass klass) : base(loc) {
			this.imports = imports;
			this.klass = klass;
		}

		public override bool deepEqual(Node n) => n is Module m && deepEqual(m);
		public bool deepEqual(Module m) => locEq(m) && imports.deepEqual(m.imports) && klass.deepEqual(m.klass);
		public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(imports), Dat.arr(imports), nameof(klass), klass);

		internal abstract class Import : Node, ToData<Import> {
			Import(Loc loc) : base(loc) {}
			public abstract bool deepEqual(Import o);

			internal sealed class Global : Import, ToData<Global> {
				internal readonly Path path;
				internal Global(Loc loc, Path path) : base(loc) { this.path = path; }
				public override bool deepEqual(Node n) => n is Global g && deepEqual(g);
				public override bool deepEqual(Import o) => o is Global g && deepEqual(g);
				public bool deepEqual(Global g) => locEq(g) && path == g.path;
				public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(path), path);
			}

			internal sealed class Relative : Import, ToData<Relative> {
				internal readonly RelPath path;
				internal Relative(Loc loc, RelPath path) : base(loc) { this.path = path; }
				public override bool deepEqual(Node n) => n is Relative r && deepEqual(r);
				public override bool deepEqual(Import o) => o is Relative r && deepEqual(r);
				public bool deepEqual(Relative r) => locEq(r) && path == r.path;
				public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(path), path);
			}
		}
	}

	sealed class Klass : Node, ToData<Klass> {
		internal readonly Head head;
		internal readonly Arr<Super> supers;
		internal readonly Arr<Member> methods;

		internal Klass(Loc loc, Head head, Arr<Super> supers, Arr<Member> methods) : base(loc) {
			this.head = head;
			this.supers = supers;
			this.methods = methods;
		}

		public override bool deepEqual(Node n) => n is Klass k && deepEqual(k);
		public bool deepEqual(Klass k) => locEq(k) && head.deepEqual(k.head) && supers.deepEqual(k.supers) && methods.deepEqual(k.methods);
		public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(head), head, nameof(supers), Dat.arr(supers), nameof(methods), Dat.arr(methods));

		internal abstract class Head : Node, ToData<Head> {
			Head(Loc loc) : base(loc) {}

			public bool deepEqual(Head h) => Equals((Node)h);

			internal sealed class Static : Head, ToData<Static> {
				internal Static(Loc loc) : base(loc) {}
				public override bool deepEqual(Node n) => n is Static s && deepEqual(s);
				public bool deepEqual(Static s) => locEq(s);
				public override Dat toDat() => Dat.of(this, nameof(loc), loc);
			}

			internal sealed class Abstract : Head, ToData<Abstract> {
				internal Abstract(Loc loc) : base(loc) {}
				public override bool deepEqual(Node n) => n is Abstract a && deepEqual(a);
				public bool deepEqual(Abstract a) => locEq(a);
				public override Dat toDat() => Dat.of(this, nameof(loc), loc);
			}

			internal sealed class Slots : Head, ToData<Slots> {
				internal readonly Arr<Slot> slots;
				internal Slots(Loc loc, Arr<Slot> slots) : base(loc) { this.slots = slots; }

				public override bool deepEqual(Node n) => n is Slots s && deepEqual(s);
				public bool deepEqual(Slots s) => locEq(s) && slots.deepEqual(s.slots);
				public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(slots), Dat.arr(slots));
			}
		}
	}

	internal sealed class Slot : Node, ToData<Slot> {
		internal readonly bool mutable;
		internal readonly Ty ty;
		internal readonly Sym name;
		internal Slot(Loc loc, bool mutable, Ty ty, Sym name) : base(loc) {
			this.mutable = mutable;
			this.ty = ty;
			this.name = name;
		}

		public override bool deepEqual(Node n) => n is Slot s && deepEqual(s);
		public bool deepEqual(Slot s) => locEq(s) && mutable == s.mutable && ty.deepEqual(s.ty) && name.deepEqual(s.name);
		public override Dat toDat() => Dat.of(this,
			nameof(loc), loc,
			nameof(mutable), Dat.boolean(mutable),
			nameof(ty), ty,
			nameof(name), name);
	}

	internal sealed class Super : Node, ToData<Super> {
		internal readonly Sym name;
		internal readonly Arr<Impl> impls;
		internal Super(Loc loc, Sym name, Arr<Impl> impls) : base(loc) {
			this.name = name;
			this.impls = impls;
		}

		public override bool deepEqual(Node n) => n is Super i && deepEqual(i);
		public bool deepEqual(Super i) =>
			locEq(i) &&
			name.deepEqual(i.name) &&
			impls.deepEqual(i.impls);
		public override Dat toDat() => Dat.of(this,
			nameof(loc), loc,
			nameof(name), name,
			nameof(impls), Dat.arr(impls));
	}

	internal sealed class Impl : Node, ToData<Impl> {
		internal readonly Sym name;
		internal readonly Arr<Sym> parameters;
		internal readonly Expr body;
		internal Impl(Loc loc, Sym name, Arr<Sym> parameters, Expr body) : base(loc) {
			this.name = name;
			this.parameters = parameters;
			this.body = body;
		}

		public override bool deepEqual(Node n) => n is Impl i && deepEqual(i);
		public bool deepEqual(Impl i) =>
			locEq(i) &&
			name.deepEqual(i.name) &&
			parameters.deepEqual(i.parameters) &&
			body.deepEqual(i.body);
		public override Dat toDat() => Dat.of(this,
			nameof(loc), loc,
			nameof(name), name,
			nameof(parameters), Dat.arr(parameters),
			nameof(body), body);
	}

	abstract class Member : Node, ToData<Member> {
		internal readonly Sym name;
		Member(Loc loc, Sym name) : base(loc) { this.name = name; }

		public override bool deepEqual(Node n) => n is Member m && deepEqual(m);
		public abstract bool deepEqual(Member m);

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

			public override bool deepEqual(Member m) => m is Method me && deepEqual(me);
			public bool deepEqual(Method m) =>
				locEq(m) &&
				isStatic == m.isStatic &&
				returnTy.deepEqual(m.returnTy) &&
				name.deepEqual(m.name) &&
				parameters.deepEqual(m.parameters) &&
				body.deepEqual(m.body);
			public override Dat toDat() => Dat.of(this,
				nameof(loc), loc,
				nameof(isStatic), Dat.boolean(isStatic),
				nameof(returnTy), returnTy,
				nameof(name), name,
				nameof(parameters), Dat.arr(parameters),
				nameof(body), body);
		}

		internal sealed class AbstractMethod : Member, ToData<AbstractMethod> {
			internal readonly Ty returnTy;
			internal readonly Arr<Parameter> parameters;
			internal AbstractMethod(Loc loc, Ty returnTy, Sym name, Arr<Parameter> parameters) : base(loc, name) {
				this.returnTy = returnTy;
				this.parameters = parameters;
			}

			public override bool deepEqual(Member m) => m is AbstractMethod a && deepEqual(a);
			public bool deepEqual(AbstractMethod a) =>
				locEq(a) &&
				returnTy.deepEqual(a.returnTy) &&
				name.deepEqual(a.name) &&
				parameters.deepEqual(a.parameters);
			public override Dat toDat() => Dat.of(this,
				nameof(loc), loc,
				nameof(returnTy), returnTy,
				nameof(name), name,
				nameof(parameters), Dat.arr(parameters));
		}

		internal sealed class Parameter : Node, ToData<Parameter> {
			internal readonly Ty ty;
			internal readonly Sym name;
			internal Parameter(Loc loc, Ty ty, Sym name) : base(loc) {
				this.ty = ty;
				this.name = name;
			}

			public override bool deepEqual(Node n) => n is Parameter p && deepEqual(p);
			public bool deepEqual(Parameter p) => locEq(p) && ty.deepEqual(p.ty) && name.deepEqual(p.name);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(ty), ty, nameof(name), name);
		}
	}

	abstract class Ty : Node, ToData<Ty> {
		Ty(Loc loc) : base(loc) {}
		public bool deepEqual(Ty ty) => Equals((Node)ty);

		internal sealed class Access : Ty, ToData<Access> {
			internal readonly Sym name;
			internal Access(Loc loc, Sym name) : base(loc) { this.name = name; }

			public override bool deepEqual(Node n) => n is Access a && deepEqual(a);
			public bool deepEqual(Access a) => locEq(a) && name.deepEqual(a.name);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(name), name);
		}

		internal sealed class Inst : Ty, ToData<Inst> {
			internal readonly Access instantiated;
			internal readonly Arr<Ty> tyArgs;
			internal Inst(Loc loc, Access instantiated, Arr<Ty> tyArgs) : base(loc) {
				this.instantiated = instantiated;
				this.tyArgs = tyArgs;
			}

			public override bool deepEqual(Node n) => n is Inst i && deepEqual(i);
			public bool deepEqual(Inst i) =>
				locEq(i) &&
				instantiated.deepEqual(i.instantiated) &&
				tyArgs.deepEqual(i.tyArgs);
			public override Dat toDat() => Dat.of(this,
				nameof(loc), loc,
				nameof(instantiated), instantiated,
				nameof(tyArgs), Dat.arr(tyArgs));
		}
	}

	abstract class Expr : Node, ToData<Expr> {
		Expr(Loc loc) : base(loc) {}
		// Node equality will cast to the right type anyway.
		public bool deepEqual(Expr e) => Equals((Node)e);

		internal sealed class Access : Expr, ToData<Access> {
			internal readonly Sym name;
			internal Access(Loc loc, Sym name) : base(loc) { this.name = name; }

			public override bool deepEqual(Node n) => n is Access a && deepEqual(a);
			public bool deepEqual(Access a) => locEq(a) && name.deepEqual(a.name);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(name), name);
		}

		internal sealed class StaticAccess : Expr, ToData<StaticAccess> {
			internal readonly Sym className;
			internal readonly Sym staticMethodName;
			internal StaticAccess(Loc loc, Sym className, Sym staticMethodName) : base(loc) {
				this.className = className;
				this.staticMethodName = staticMethodName;
			}

			public override bool deepEqual(Node n) => n is StaticAccess s && deepEqual(s);
			public bool deepEqual(StaticAccess s) =>
				locEq(s) &&
				className.deepEqual(s.className) &&
				staticMethodName.deepEqual(s.staticMethodName);
			public override Dat toDat() => Dat.of(this,
				nameof(loc), loc,
				nameof(className), className,
				nameof(staticMethodName), staticMethodName);
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

			public override bool deepEqual(Node n) => n is OperatorCall o && deepEqual(o);
			public bool deepEqual(OperatorCall o) =>
				locEq(o) &&
				left.deepEqual(o.left) &&
				oper.deepEqual(o.oper) &&
				right.deepEqual(o.right);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(oper), oper, nameof(right), right);
		}

		internal sealed class Call : Expr, ToData<Call> {
			internal readonly Expr target;
			internal readonly Arr<Expr> args;
			internal Call(Loc loc, Expr target, Arr<Expr> args) : base(loc) {
				this.target = target;
				this.args = args;
			}

			public override bool deepEqual(Node n) => n is Call c && deepEqual(c);
			public bool deepEqual(Call c) => locEq(c) && target.deepEqual(c.target) && args.deepEqual(c.args);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(target), target, nameof(args), Dat.arr(args));
		}

		internal sealed class New : Expr, ToData<New> {
			internal readonly Arr<Expr> args;
			internal New(Loc loc, Arr<Expr> args) : base(loc) {
				this.args = args;
			}

			public override bool deepEqual(Node n) => n is New ne && deepEqual(ne);
			public bool deepEqual(New n) => locEq(n) && args.deepEqual(n.args);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(args), Dat.arr(args));
		}

		internal sealed class GetProperty : Expr, ToData<GetProperty> {
			internal readonly Expr target;
			internal readonly Sym propertyName;
			internal GetProperty(Loc loc, Expr target, Sym propertyName) : base(loc) {
				this.target = target;
				this.propertyName = propertyName;
			}

			public override bool deepEqual(Node n) => n is GetProperty g && deepEqual(g);
			public bool deepEqual(GetProperty g) => locEq(g) && target.deepEqual(g.target) && propertyName.deepEqual(g.propertyName);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(target), target, nameof(propertyName), propertyName);
		}

		internal sealed class Let : Expr, ToData<Let> {
			internal readonly Pattern assigned;
			internal readonly Expr value;
			Late<Expr> _then;
			internal Expr then { get => _then.get; set => _then.set(value); }

			internal Let(Loc loc, Pattern assigned, Expr value) : base(loc) {
				this.assigned = assigned;
				this.value = value;
			}

			public override bool deepEqual(Node n) => n is Let l && deepEqual(l);
			public bool deepEqual(Let l) =>
				locEq(l) &&
				assigned.deepEqual(l.assigned) &&
				value.deepEqual(l.value) &&
				then.deepEqual(l.then);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(assigned), assigned, nameof(value), value, nameof(then), then);
		}

		internal sealed class Seq : Expr, ToData<Seq> {
			internal readonly Expr first;
			internal readonly Expr then;
			internal Seq(Loc loc, Expr first, Expr then) : base(loc) {
				this.first = first;
				this.then = then;
			}

			public override bool deepEqual(Node n) => n is Seq s && deepEqual(s);
			public bool deepEqual(Seq s) => locEq(s) && first.deepEqual(s.first) && then.deepEqual(s.then);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(first), first, nameof(then), then);
		}

		internal sealed class Literal : Expr, ToData<Literal> {
			internal readonly Model.Expr.Literal.LiteralValue value;
			internal Literal(Loc loc, Model.Expr.Literal.LiteralValue value) : base(loc) { this.value = value; }

			public override bool deepEqual(Node n) => n is Literal l && deepEqual(l);
			public bool deepEqual(Literal l) => locEq(l) && value.deepEqual(l.value);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(value), value);
		}

		internal sealed class Self : Expr, ToData<Self> {
			internal Self(Loc loc) : base(loc) {}

			public override bool deepEqual(Node n) => n is Self s && deepEqual(s);
			public bool deepEqual(Self s) => locEq(s);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc);
		}

		internal sealed class WhenTest : Expr, ToData<WhenTest> {
			internal readonly Arr<Case> cases;
			internal readonly Expr elseResult;
			internal WhenTest(Loc loc, Arr<Case> cases, Expr elseResult) : base(loc) { this.cases = cases; this.elseResult = elseResult; }

			public override bool deepEqual(Node n) => n is WhenTest w && deepEqual(w);
			public bool deepEqual(WhenTest w) => locEq(w) && cases.deepEqual(w.cases) && elseResult.deepEqual(w.elseResult);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(cases), Dat.arr(cases), nameof(elseResult), elseResult);

			internal sealed class Case : Node, ToData<Case> {
				internal readonly Expr test;
				internal readonly Expr result;
				internal Case(Loc loc, Expr test, Expr result) : base(loc) { this.test = test; this.result = result; }

				public override bool deepEqual(Node n) => n is Case && deepEqual(n);
				public bool deepEqual(Case c) => locEq(c) && test.deepEqual(c.test) && result.deepEqual(c.result);
				public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(test), test, nameof(result), result);
			}
		}

		internal sealed class Assert : Expr, ToData<Assert> {
			internal readonly Expr asserted;
			internal Assert(Loc loc, Expr asserted) : base(loc) { this.asserted = asserted; }

			public override bool deepEqual(Node n) => n is Assert a && deepEqual(a);
			public bool deepEqual(Assert a) => locEq(a) && asserted.deepEqual(a.asserted);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(asserted), asserted);
		}

		/** It will be a checker error if 'catch' is missing, except in the case of do-finally. */
		internal sealed class Try : Expr, ToData<Try> {
			// TODO: name these @do, @catch, @finally. But currently breaks syntax highlighting. (https://github.com/dotnet/csharp-tmLanguage/issues/46)
			internal readonly Expr _do;
			internal readonly Op<Catch> _catch;
			internal readonly Op<Expr> _finally;
			internal Try(Loc loc, Expr _do, Op<Catch> _catch, Op<Expr> _finally) : base(loc) {
				this._do = _do;
				this._catch = _catch;
				this._finally = _finally;
			}

			public override bool deepEqual(Node n) => n is Try t && deepEqual(t);
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

			internal sealed class Catch : Node, ToData<Catch> {
				internal readonly Ty exceptionTy;
				internal readonly Loc exceptionNameLoc;
				internal readonly Sym exceptionName;
				internal readonly Expr then;
				internal Catch(Loc loc, Ty ty, Loc nameLoc, Sym name, Expr then) : base(loc) {
					this.exceptionTy = ty;
					this.exceptionNameLoc = nameLoc;
					this.exceptionName = name;
					this.then = then;
				}

				public override bool deepEqual(Node n) => n is Catch c && deepEqual(c);
				public bool deepEqual(Catch c) => locEq(c) && exceptionTy.deepEqual(c.exceptionTy) && exceptionName.deepEqual(c.exceptionName) && then.deepEqual(c.then);
				public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(exceptionTy), exceptionTy, nameof(exceptionName), exceptionName, nameof(then), then);
			}
		}
	}

	internal abstract class Pattern : Node, ToData<Pattern> {
		Pattern(Loc loc) : base(loc) {}
		public bool deepEqual(Pattern p) => Equals((Node)p);

		internal sealed class Ignore : Pattern, ToData<Ignore> {
			internal Ignore(Loc loc) : base(loc) {}

			public override bool deepEqual(Node n) => n is Ignore i && deepEqual(i);
			public bool deepEqual(Ignore i) => locEq(i);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc);
		}

		internal sealed class Single : Pattern, ToData<Single> {
			internal readonly Sym name;
			internal Single(Loc loc, Sym name) : base(loc) { this.name = name; }

			public override bool deepEqual(Node n) => n is Single s && deepEqual(s);
			public bool deepEqual(Single s) => locEq(s) && name.deepEqual(s.name);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(name), name);
		}

		internal sealed class Destruct : Pattern, ToData<Destruct> {
			internal readonly Arr<Pattern> destructed;
			internal Destruct(Loc loc, Arr<Pattern> destructed) : base(loc) { this.destructed = destructed; }

			public override bool deepEqual(Node n) => n is Destruct d && deepEqual(d);
			public bool deepEqual(Destruct d) => locEq(d) && destructed.deepEqual(d.destructed);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(destructed), Dat.arr(destructed));
		}
	}
}
