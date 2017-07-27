using System;
using static Utils;

namespace Model {
	/**
	 * Base class of all method-like things.
	 * Does not include Impl, though that has a *pointer* to the implemented AbstractMethod.
	 */
	abstract class Method : Member, MethodOrImpl, ToData<Method>, Identifiable<Method.Id>, IEquatable<Method> {
		internal struct Id : ToData<Id> {
			internal readonly ClassLike.Id klassId;
			internal readonly Sym name;
			internal Id(ClassLike.Id klass, Sym name) { this.klassId = klass; this.name = name; }
			public bool deepEqual(Id m) => klassId.deepEqual(m.klassId) && name.deepEqual(m.name);
			public Dat toDat() => Dat.of(this, nameof(klassId), klassId, nameof(name), name);
		}

		Klass MethodOrImpl.klass => (Klass)klass; //TODO: this method shouldn't be called on AbstractMethod, so maybe MethodOrImpl shouldn't contain that?
		Method MethodOrImpl.implementedMethod => this;

		bool IEquatable<Method>.Equals(Method m) => object.ReferenceEquals(this, m);
		public sealed override int GetHashCode() => name.GetHashCode();

		[ParentPointer] readonly ClassLike _klass;
		internal override ClassLike klass => _klass;
		internal abstract bool isAbstract { get; }
		internal abstract bool isStatic { get; }
		[UpPointer] internal readonly Ty returnTy;
		internal readonly Effect selfEffect;
		internal readonly Arr<Parameter> parameters;

		public sealed override bool deepEqual(Member m) => m is Method mt && deepEqual(mt);
		public abstract bool deepEqual(Method m);
		public Id getId() => new Id(klass.getId(), name);

		protected Method(ClassLike klass, Loc loc, Ty returnTy, Sym name, Effect selfEffect, Arr<Parameter> parameters) : base(loc, name) {
			this._klass = klass;
			this.returnTy = returnTy;
			this.selfEffect = selfEffect;
			this.parameters = parameters;
		}

		internal uint arity => parameters.length;
	}

	/** AbstractMethod or BuiltinAbstractMethod */
	abstract class AbstractMethodLike : Method {
		protected AbstractMethodLike(ClassLike klass, Loc loc, Ty returnTy, Sym name, Effect selfEffect, Arr<Parameter> parameters)
			: base(klass, loc, returnTy, name, selfEffect, parameters) {}
	}

	/** MethodWithBody or BuiltinMethodWithBody */
	abstract class MethodWithBodyLike : Method {
		protected MethodWithBodyLike(ClassLike klass, Loc loc, Ty returnTy, Sym name, Effect selfEffect, Arr<Parameter> parameters)
			: base(klass, loc, returnTy, name, selfEffect, parameters) {}
	}

	internal sealed class MethodWithBody : MethodWithBodyLike, ToData<MethodWithBody> {
		internal readonly bool _isStatic;
		internal override bool isAbstract => false;
		internal override bool isStatic => _isStatic;
		Late<Expr> _body;
		internal Expr body {
			get => _body.get;
			set {
				value.parent = this;
				_body.set(value);
			}
		}

		internal MethodWithBody(Klass klass, Loc loc, bool isStatic, Ty returnTy, Sym name, Effect selfEffect, Arr<Parameter> parameters)
			: base(klass, loc, returnTy, name, selfEffect, parameters) {
			this._isStatic = isStatic;
		}

		internal override MemberId getMemberId() => throw TODO();
		internal override string showKind(bool upper) => isStatic ? (upper ? "Function" : "function") : (upper ? nameof(Method) : "method");
		public override bool deepEqual(Method m) => m is MethodWithBody mb && deepEqual(mb);
		public bool deepEqual(MethodWithBody m) => throw TODO(); // TODO: should methods with different (reference identity) parent be not equal?
		public override Dat toDat() => Dat.of(this,
			nameof(loc), loc,
			nameof(isStatic), Dat.boolean(isStatic),
			nameof(returnTy), returnTy.getTyId(),
			nameof(name), name,
			nameof(selfEffect), Dat.str(selfEffect.show),
			nameof(parameters), Dat.arr(parameters),
			nameof(body), body);
	}

	internal sealed class AbstractMethod : AbstractMethodLike, ToData<AbstractMethod> {
		internal override bool isAbstract => true;
		internal override bool isStatic => false;

		internal override MemberId getMemberId() => throw TODO();
		internal override string showKind(bool upper) => upper ? "Abstract method" : "abstract method";
		internal AbstractMethod(Klass klass, Loc loc, Ty returnTy, Sym name, Effect selfEffect, Arr<Parameter> parameters)
			: base(klass, loc, returnTy, name, selfEffect, parameters) {}

		public override bool deepEqual(Method m) => m is AbstractMethod a && deepEqual(a);
		public bool deepEqual(AbstractMethod a) => throw TODO();
		public override Dat toDat() => Dat.of(this,
			nameof(loc), loc,
			nameof(returnTy), returnTy.getTyId(),
			nameof(name), name,
			nameof(parameters), Dat.arr(parameters));
	}

	// Since there's no shadowing allowed, parameters can be identified by just their name.
	internal sealed class Parameter : ModelElement, ToData<Parameter>, Identifiable<Sym> {
		internal readonly Loc loc;
		[UpPointer] internal readonly Ty ty;
		internal readonly Sym name;
		internal readonly uint index;

		internal Parameter(Loc loc, Ty ty, Sym name, uint index) {
			this.loc = loc;
			this.ty = ty;
			this.name = name;
			this.index = index;
		}

		public bool deepEqual(Parameter p) =>
			loc.deepEqual(p.loc) &&
			ty.equalsId<Ty, TyId>(p.ty) &&
			name.deepEqual(p.name) &&
			index == p.index;
		public Dat toDat() => Dat.of(this,
			nameof(loc), loc,
			nameof(ty), ty.getTyId(),
			nameof(name), name,
			nameof(index), Dat.nat(index));
		public Sym getId() => name;
	}
}
