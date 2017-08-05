using System;
using static Utils;

namespace Model {
	/**
	 * Base class of all method-like things.
	 * Does not include Impl, though that has a *pointer* to the implemented AbstractMethod.
	 */
	abstract class MethodDeclaration : MemberDeclaration, TypeParameterOrigin, MethodOrImpl, ToData<MethodDeclaration>, Identifiable<MethodDeclaration.Id>, IEquatable<MethodDeclaration> {
		ClassDeclaration MethodOrImpl.klass => (ClassDeclaration)klass; //TODO: this method shouldn't be called on AbstractMethod, so maybe MethodOrImpl shouldn't contain that?
		MethodDeclaration MethodOrImpl.implementedMethod => this;

		bool IEquatable<MethodDeclaration>.Equals(MethodDeclaration m) => object.ReferenceEquals(this, m);
		public sealed override int GetHashCode() => name.GetHashCode();

		[ParentPointer] readonly ClassDeclarationLike _klass;
		internal override ClassDeclarationLike klass => _klass;
		internal abstract bool isAbstract { get; }
		internal abstract bool isStatic { get; }
		internal readonly Arr<TypeParameter> typeParameters;
		[UpPointer] internal readonly Ty returnTy;
		internal readonly Effect selfEffect;
		internal readonly Arr<Parameter> parameters;

		bool DeepEqual<TypeParameterOrigin>.deepEqual(TypeParameterOrigin o) => o is MethodDeclaration m && deepEqual(m);
		public sealed override bool deepEqual(MemberDeclaration m) => m is MethodDeclaration mt && deepEqual(mt);
		public abstract bool deepEqual(MethodDeclaration m);
		TypeParameterOriginId Identifiable<TypeParameterOriginId>.getId() => getId();
		public Id getId() => new Id(klass.getId(), name);

		protected MethodDeclaration(ClassDeclarationLike klass, Loc loc, Arr<TypeParameter> typeParameters, Ty returnTy, Sym name, Effect selfEffect, Arr<Parameter> parameters) : base(loc, name) {
			this._klass = klass;
			this.typeParameters = typeParameters;
			this.returnTy = returnTy;
			this.selfEffect = selfEffect;
			this.parameters = parameters;
		}

		internal uint arity => parameters.length;

		internal sealed class Id : TypeParameterOriginId, ToData<Id> {
			internal readonly ClassDeclarationLike.Id klassId;
			internal readonly Sym name;
			internal Id(ClassDeclarationLike.Id klass, Sym name) { this.klassId = klass; this.name = name; }
			public override bool deepEqual(TypeParameterOriginId ti) => ti is Id i && deepEqual(i);
			public bool deepEqual(Id i) => klassId.deepEqual(i.klassId) && name.deepEqual(i.name);
			public override Dat toDat() => Dat.of(this, nameof(klassId), klassId, nameof(name), name);
		}
	}

	/** AbstractMethod or BuiltinAbstractMethod */
	abstract class AbstractMethodLike : MethodDeclaration {
		protected AbstractMethodLike(ClassDeclarationLike klass, Loc loc, Arr<TypeParameter> typeParameters, Ty returnTy, Sym name, Effect selfEffect, Arr<Parameter> parameters)
			: base(klass, loc, typeParameters, returnTy, name, selfEffect, parameters) {}
	}

	/** MethodWithBody or BuiltinMethodWithBody */
	abstract class MethodWithBodyLike : MethodDeclaration {
		protected MethodWithBodyLike(ClassDeclarationLike klass, Loc loc, Arr<TypeParameter> typeParameters, Ty returnTy, Sym name, Effect selfEffect, Arr<Parameter> parameters)
			: base(klass, loc, typeParameters, returnTy, name, selfEffect, parameters) {}
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

		internal MethodWithBody(ClassDeclaration klass, Loc loc, bool isStatic, Arr<TypeParameter> typeParameters, Ty returnTy, Sym name, Effect selfEffect, Arr<Parameter> parameters)
			: base(klass, loc, typeParameters, returnTy, name, selfEffect, parameters) {
			this._isStatic = isStatic;
		}

		internal override MemberId getMemberId() => throw TODO();
		internal override string showKind(bool upper) => isStatic ? (upper ? "Function" : "function") : (upper ? nameof(MethodDeclaration) : "method");
		public override bool deepEqual(MethodDeclaration m) => m is MethodWithBody mb && deepEqual(mb);
		public bool deepEqual(MethodWithBody m) => throw TODO(); // TODO: should methods with different (reference identity) parent be not equal?
		public override Dat toDat() => Dat.of(this,
			nameof(loc), loc,
			nameof(isStatic), Dat.boolean(isStatic),
			nameof(returnTy), returnTy.getTyId(),
			nameof(name), name,
			nameof(selfEffect), selfEffect,
			nameof(parameters), Dat.arr(parameters),
			nameof(body), body);
	}

	internal sealed class AbstractMethod : AbstractMethodLike, ToData<AbstractMethod> {
		internal override bool isAbstract => true;
		internal override bool isStatic => false;

		internal override MemberId getMemberId() => throw TODO();
		internal override string showKind(bool upper) => upper ? "Abstract method" : "abstract method";
		internal AbstractMethod(ClassDeclaration klass, Loc loc, Arr<TypeParameter> typeParameters, Ty returnTy, Sym name, Effect selfEffect, Arr<Parameter> parameters)
			: base(klass, loc, typeParameters, returnTy, name, selfEffect, parameters) {}

		public override bool deepEqual(MethodDeclaration m) => m is AbstractMethod a && deepEqual(a);
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

	struct MethodInst : ToData<MethodInst> {
		[UpPointer] internal readonly MethodDeclaration decl;
		[UpPointer] internal readonly Arr<Ty> typeArguments;
		internal MethodInst(MethodDeclaration methodDecl, Arr<Ty> typeArguments) {
			this.decl = methodDecl;
			this.typeArguments = typeArguments;
			assert(methodDecl.typeParameters.length == typeArguments.length);
		}
		internal void Deconstruct(out MethodDeclaration methodDecl, out Arr<Ty> typeArguments) {
			methodDecl = this.decl;
			typeArguments = this.typeArguments;
		}

		internal bool isStatic =>
			decl.isStatic;
		internal bool isAbstract =>
			decl.isAbstract;

		internal TyReplacer replacer =>
			TyReplacer.ofMethod(decl, typeArguments);

		internal Arr<Parameter> parameters =>
			decl.parameters;

		public bool deepEqual(MethodInst m) =>
			decl.equalsId<MethodDeclaration, MethodDeclaration.Id>(m.decl) &&
			typeArguments.eachEqualId<Ty, TyId>(m.typeArguments);
		public Dat toDat() => Dat.of(this,
			nameof(decl), decl.getId(),
			nameof(typeArguments), Dat.arr(typeArguments.map(a => a.getTyId())));
	}
}
