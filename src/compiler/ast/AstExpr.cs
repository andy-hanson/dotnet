namespace Ast {
	abstract class Expr : Node, ToData<Expr> {
		protected Expr(Loc loc) : base(loc) {}
		// Node equality will cast to the right type anyway.
		public bool deepEqual(Expr e) => Equals((Node)e);
	}

	internal sealed class Access : Expr, ToData<Access> {
		internal readonly Sym name;
		internal Access(Loc loc, Sym name) : base(loc) { this.name = name; }
		internal void Deconstruct(out Loc loc, out Sym name) { loc = this.loc; name = this.name; }

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
		internal void Deconstruct(out Loc loc, out Sym className, out Sym staticMethodName) {
			loc = this.loc; className = this.className; staticMethodName = this.staticMethodName;
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
		internal void Deconstruct(out Loc loc, out Expr left, out Sym oper, out Expr right) {
			loc = this.loc; left = this.left; oper = this.oper; right = this.right;
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
		internal void Deconstruct(out Loc loc, out Expr target, out Arr<Expr> args) {
			loc = this.loc; target = this.target; args = this.args;
		}

		public override bool deepEqual(Node n) => n is Call c && deepEqual(c);
		public bool deepEqual(Call c) => locEq(c) && target.deepEqual(c.target) && args.deepEqual(c.args);
		public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(target), target, nameof(args), Dat.arr(args));
	}

	internal sealed class Recur : Expr, ToData<Recur> {
		internal readonly Arr<Expr> args;
		internal Recur(Loc loc, Arr<Expr> args) : base(loc) { this.args = args; }
		internal void Deconstruct(out Loc loc, out Arr<Expr> args) { loc = this.loc; args = this.args; }

		public override bool deepEqual(Node n) => n is Recur r && deepEqual(r);
		public bool deepEqual(Recur r) => locEq(r) && args.deepEqual(r.args);
		public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(args), Dat.arr(args));
	}

	internal sealed class New : Expr, ToData<New> {
		internal readonly Arr<Expr> args;
		internal New(Loc loc, Arr<Expr> args) : base(loc) {
			this.args = args;
		}
		internal void Deconstruct(out Loc loc, out Arr<Expr> args) { loc = this.loc; args = this.args; }

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
		internal void Deconstruct(out Loc loc, out Expr target, out Sym propertyName) {
			loc = this.loc; target = this.target; propertyName = this.propertyName;
		}

		public override bool deepEqual(Node n) => n is GetProperty g && deepEqual(g);
		public bool deepEqual(GetProperty g) => locEq(g) && target.deepEqual(g.target) && propertyName.deepEqual(g.propertyName);
		public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(target), target, nameof(propertyName), propertyName);
	}

	internal sealed class SetProperty : Expr, ToData<SetProperty> {
		internal readonly Sym propertyName;
		internal readonly Expr value;
		internal SetProperty(Loc loc, Sym propertyName, Expr value) : base(loc) {
			this.propertyName = propertyName;
			this.value = value;
		}
		internal void Deconstruct(out Loc loc, out Sym propertyName, out Expr value) {
			loc = this.loc; propertyName = this.propertyName; value = this.value;
		}

		public override bool deepEqual(Node n) => n is SetProperty s && deepEqual(s);
		public bool deepEqual(SetProperty s) => locEq(s) && propertyName.deepEqual(s.propertyName) && value.deepEqual(s.value);
		public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(propertyName), propertyName, nameof(value), value);
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
		internal void Deconstruct(out Loc loc, out Pattern assigned, out Expr value, out Expr then) {
			loc = this.loc; assigned = this.assigned; value = this.value; then = this.then;
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
		internal void Deconstruct(out Loc loc, out Expr first, out Expr then) {
			loc = this.loc; first = this.first; then = this.then;
		}

		public override bool deepEqual(Node n) => n is Seq s && deepEqual(s);
		public bool deepEqual(Seq s) => locEq(s) && first.deepEqual(s.first) && then.deepEqual(s.then);
		public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(first), first, nameof(then), then);
	}

	internal sealed class Literal : Expr, ToData<Literal> {
		internal readonly LiteralValue value;
		internal Literal(Loc loc, LiteralValue value) : base(loc) { this.value = value; }
		internal void Deconstruct(out Loc loc, out LiteralValue value) { loc = this.loc; value = this.value; }

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
		internal void Deconstruct(out Loc loc, out Arr<Case> cases, out Expr elseResult) {
			loc = this.loc; cases = this.cases; elseResult = this.elseResult;
		}

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
		internal void Deconstruct(out Loc loc, out Expr asserted) { loc = this.loc; asserted = this.asserted; }

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
		internal void Deconstruct(out Loc loc, out Expr _do, out Op<Catch> _catch, out Op<Expr> _finally) {
			loc = this.loc; _do = this._do; _catch = this._catch; _finally = this._finally;
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
			internal Catch(Loc loc, Ty exceptionTy, Loc exceptionNameLoc, Sym exceptionName, Expr then) : base(loc) {
				this.exceptionTy = exceptionTy;
				this.exceptionNameLoc = exceptionNameLoc;
				this.exceptionName = exceptionName;
				this.then = then;
			}
			internal void Deconstruct(out Loc loc, out Ty exceptionTy, out Loc exceptionNameLoc, out Sym exceptionName, out Expr then) {
				loc = this.loc; exceptionTy = this.exceptionTy; exceptionNameLoc = this.exceptionNameLoc; exceptionName = this.exceptionName; then = this.then;
			}

			public override bool deepEqual(Node n) => n is Catch c && deepEqual(c);
			public bool deepEqual(Catch c) =>
				locEq(c) &&
				exceptionTy.deepEqual(c.exceptionTy) &&
				exceptionName.deepEqual(c.exceptionName) &&
				then.deepEqual(c.then);
			public override Dat toDat() => Dat.of(this,
				nameof(loc), loc,
				nameof(exceptionTy), exceptionTy,
				nameof(exceptionName), exceptionName,
				nameof(then), then);
		}
	}
}
