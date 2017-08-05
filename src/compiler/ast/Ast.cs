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
		internal readonly ClassDeclaration klass;

		internal Module(Loc loc, Arr<Import> imports, ClassDeclaration klass) : base(loc) {
			this.imports = imports;
			this.klass = klass;
		}
		internal void Deconstruct(out Arr<Import> imports, out ClassDeclaration klass) { imports = this.imports; klass = this.klass; }

		public override bool deepEqual(Node n) => n is Module m && deepEqual(m);
		public bool deepEqual(Module m) => locEq(m) && imports.deepEqual(m.imports) && klass.deepEqual(m.klass);
		public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(imports), Dat.arr(imports), nameof(klass), klass);
	}

	internal abstract class Import : Node, ToData<Import> {
		Import(Loc loc) : base(loc) {}
		public abstract bool deepEqual(Import o);

		internal sealed class Global : Import, ToData<Global> {
			internal readonly Path path;
			internal Global(Loc loc, Path path) : base(loc) { this.path = path; }
			internal void Deconstruct(out Loc loc, out Path path) { loc = this.loc; path = this.path; }

			public override bool deepEqual(Node n) => n is Global g && deepEqual(g);
			public override bool deepEqual(Import o) => o is Global g && deepEqual(g);
			public bool deepEqual(Global g) => locEq(g) && path == g.path;
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(path), path);
		}

		internal sealed class Relative : Import, ToData<Relative> {
			internal readonly RelPath path;
			internal Relative(Loc loc, RelPath path) : base(loc) { this.path = path; }
			internal void Deconstruct(out Loc loc, out RelPath path) { loc = this.loc; path = this.path; }

			public override bool deepEqual(Node n) => n is Relative r && deepEqual(r);
			public override bool deepEqual(Import o) => o is Relative r && deepEqual(r);
			public bool deepEqual(Relative r) => locEq(r) && path == r.path;
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(path), path);
		}
	}

	sealed class ClassDeclaration : Node, ToData<ClassDeclaration> {
		internal readonly Arr<Sym> typeParameters;
		internal readonly Op<Head> head;
		internal readonly Arr<Super> supers;
		internal readonly Arr<Method> methods;

		internal ClassDeclaration(Loc loc, Arr<Sym> typeParameters, Op<Head> head, Arr<Super> supers, Arr<Method> methods) : base(loc) {
			this.typeParameters = typeParameters;
			this.head = head;
			this.supers = supers;
			this.methods = methods;
		}
		internal void Deconstruct(out Loc loc, out Arr<Sym> typeParameters, out Op<Head> head, out Arr<Super> supers, out Arr<Method> methods) {
			loc = this.loc;
			typeParameters = this.typeParameters;
			head = this.head;
			supers = this.supers;
			methods = this.methods;
		}

		public override bool deepEqual(Node n) => n is ClassDeclaration k && deepEqual(k);
		public bool deepEqual(ClassDeclaration k) =>
			locEq(k) &&
			typeParameters.deepEqual(k.typeParameters) &&
			head.deepEqual(k.head) &&
			supers.deepEqual(k.supers) &&
			methods.deepEqual(k.methods);
		public override Dat toDat() => Dat.of(this,
			nameof(loc), loc,
			nameof(typeParameters), Dat.arr(typeParameters),
			nameof(head), Dat.op(head),
			nameof(supers), Dat.arr(supers),
			nameof(methods), Dat.arr(methods));

		internal abstract class Head : Node, ToData<Head> {
			Head(Loc loc) : base(loc) {}

			public bool deepEqual(Head h) => Equals((Node)h);

			internal sealed class Abstract : Head, ToData<Abstract> {
				internal readonly Arr<AbstractMethod> abstractMethods;
				internal Abstract(Loc loc, Arr<AbstractMethod> abstractMethods) : base(loc) { this.abstractMethods = abstractMethods; }
				public override bool deepEqual(Node n) => n is Abstract a && deepEqual(a);
				public bool deepEqual(Abstract a) => locEq(a);
				public override Dat toDat() => Dat.of(this, nameof(loc), loc);

				internal sealed class AbstractMethod : Node, ToData<AbstractMethod> {
					internal readonly Ty returnTy;
					internal readonly Sym name;
					internal readonly Arr<Sym> typeParameters;
					internal readonly Model.Effect selfEffect;
					internal readonly Arr<Parameter> parameters;
					internal AbstractMethod(Loc loc, Arr<Sym> typeParameters, Ty returnTy, Sym name, Model.Effect selfEffect, Arr<Parameter> parameters) : base(loc) {
						this.returnTy = returnTy;
						this.name = name;
						this.typeParameters = typeParameters;
						this.selfEffect = selfEffect;
						this.parameters = parameters;
					}
					internal void Deconstruct(out Loc loc, out Arr<Sym> typeParameters, out Ty returnTy, out Sym name, out Model.Effect selfEffect, out Arr<Parameter> parameters) {
						loc = this.loc;
						typeParameters = this.typeParameters;
						returnTy = this.returnTy;
						name = this.name;
						selfEffect = this.selfEffect;
						parameters = this.parameters;
					}

					public override bool deepEqual(Node n) => n is AbstractMethod a && deepEqual(a);
					public bool deepEqual(AbstractMethod a) =>
						locEq(a) &&
						returnTy.deepEqual(a.returnTy) &&
						name.deepEqual(a.name) &&
						typeParameters.deepEqual(a.typeParameters) &&
						parameters.deepEqual(a.parameters);
					public override Dat toDat() => Dat.of(this,
						nameof(loc), loc,
						nameof(returnTy), returnTy,
						nameof(name), name,
						nameof(typeParameters), Dat.arr(typeParameters),
						nameof(parameters), Dat.arr(parameters));
				}
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
		internal void Deconstruct(out Loc loc, out bool mutable, out Ty ty, out Sym name) {
			loc = this.loc; mutable = this.mutable; ty = this.ty; name = this.name;
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
		internal readonly Arr<Ty> tyArgs;
		internal readonly Arr<Impl> impls;
		internal Super(Loc loc, Sym name, Arr<Ty> tyArgs, Arr<Impl> impls) : base(loc) {
			this.name = name;
			this.tyArgs = tyArgs;
			this.impls = impls;
		}
		internal void Deconstruct(out Loc loc, out Sym name, out Arr<Ty> tyArgs, out Arr<Impl> impls) {
			loc = this.loc; name = this.name; tyArgs = this.tyArgs; impls = this.impls;
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
		internal readonly Arr<Sym> parameterNames;
		internal readonly Expr body;
		internal Impl(Loc loc, Sym name, Arr<Sym> parameterNames, Expr body) : base(loc) {
			this.name = name;
			this.parameterNames = parameterNames;
			this.body = body;
		}
		internal void Deconstruct(out Loc loc, out Sym name, out Arr<Sym> parameterNames, out Expr body) {
			loc = this.loc; name = this.name; parameterNames = this.parameterNames; body = this.body;
		}

		public override bool deepEqual(Node n) => n is Impl i && deepEqual(i);
		public bool deepEqual(Impl i) =>
			locEq(i) &&
			name.deepEqual(i.name) &&
			parameterNames.deepEqual(i.parameterNames) &&
			body.deepEqual(i.body);
		public override Dat toDat() => Dat.of(this,
			nameof(loc), loc,
			nameof(name), name,
			nameof(parameterNames), Dat.arr(parameterNames),
			nameof(body), body);
	}

	internal sealed class Method : Node, ToData<Method> {
		internal readonly bool isStatic;
		internal readonly Arr<Sym> typeParameters;
		internal readonly Ty returnTy;
		internal readonly Sym name;
		internal readonly Model.Effect selfEffect;
		internal readonly Arr<Parameter> parameters;
		internal readonly Expr body;
		internal Method(Loc loc, bool isStatic, Arr<Sym> typeParameters, Ty returnTy, Sym name, Model.Effect selfEffect, Arr<Parameter> parameters, Expr body) : base(loc) {
			this.isStatic = isStatic;
			this.typeParameters = typeParameters;
			this.returnTy = returnTy;
			this.name = name;
			this.selfEffect = selfEffect;
			this.parameters = parameters;
			this.body = body;
		}
		internal void Deconstruct(out Loc loc, out bool isStatic, out Arr<Sym> typeParameters, out Ty returnTy, out Sym name, out Model.Effect selfEffect, out Arr<Parameter> parameters, out Expr body) {
			loc = this.loc;
			isStatic = this.isStatic;
			typeParameters = this.typeParameters;
			returnTy = this.returnTy;
			name = this.name;
			selfEffect = this.selfEffect;
			parameters = this.parameters;
			body = this.body;
		}

		public override bool deepEqual(Node n) => n is Method m && deepEqual(m);
		public bool deepEqual(Method m) =>
			locEq(m) &&
			isStatic == m.isStatic &&
			typeParameters.deepEqual(m.typeParameters) &&
			returnTy.deepEqual(m.returnTy) &&
			name.deepEqual(m.name) &&
			parameters.deepEqual(m.parameters) &&
			body.deepEqual(m.body);
		public override Dat toDat() => Dat.of(this,
			nameof(loc), loc,
			nameof(isStatic), Dat.boolean(isStatic),
			nameof(typeParameters), Dat.arr(typeParameters),
			nameof(returnTy), returnTy,
			nameof(name), name,
			nameof(parameters), Dat.arr(parameters),
			nameof(body), body);
	}

	internal sealed class Parameter : Node, ToData<Parameter> {
		internal readonly Ty ty;
		internal readonly Sym name;
		internal Parameter(Loc loc, Ty ty, Sym name) : base(loc) {
			this.ty = ty;
			this.name = name;
		}
		internal void Deconstruct(out Loc loc, out Ty ty, out Sym name) { loc = this.loc; ty = this.ty; name = this.name; }

		public override bool deepEqual(Node n) => n is Parameter p && deepEqual(p);
		public bool deepEqual(Parameter p) => locEq(p) && ty.deepEqual(p.ty) && name.deepEqual(p.name);
		public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(ty), ty, nameof(name), name);
	}

	sealed class Ty : Node, ToData<Ty> {
		internal readonly Model.Effect effect;
		internal readonly Sym name;
		internal readonly Arr<Ty> tyArgs;
		internal Ty(Loc loc, Model.Effect effect, Sym name, Arr<Ty> tyArgs) : base(loc) {
			this.effect = effect;
			this.name = name;
			this.tyArgs = tyArgs;
		}
		internal void Deconstruct(out Loc loc, out Model.Effect effect, out Sym name, out Arr<Ty> tyArgs) {
			loc = this.loc;
			effect = this.effect;
			name = this.name;
			tyArgs = this.tyArgs;
		}

		public override bool deepEqual(Node n) => n is Ty t && deepEqual(t);
		public bool deepEqual(Ty t) =>
			locEq(t) &&
			effect.deepEqual(t.effect) &&
			name.deepEqual(t.name) &&
			tyArgs.deepEqual(t.tyArgs);
		public override Dat toDat() => Dat.of(this,
			nameof(loc), loc,
			nameof(effect), effect,
			nameof(name), name,
			nameof(tyArgs), Dat.arr(tyArgs));
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
