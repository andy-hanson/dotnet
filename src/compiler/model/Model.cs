using System;

namespace Model {
	abstract class ModelElement {
		public override bool Equals(object o) => throw new NotSupportedException();
		public override int GetHashCode() => throw new NotSupportedException();
	}

	abstract class ClassDeclarationLike : ModelElement, TypeParameterOrigin, ToData<ClassDeclarationLike>, Identifiable<ClassDeclarationLike.Id>, FastEquals<ClassDeclarationLike> {
		internal readonly Sym name;
		internal readonly Arr<TypeParameter> typeParameters;
		protected ClassDeclarationLike(Sym name, Arr<TypeParameter> typeParameters) { this.name = name; this.typeParameters = typeParameters; }

		internal abstract Arr<Super> supers { get; }
		internal abstract Dict<Sym, MemberDeclaration> membersMap { get; }
		internal abstract bool isAbstract { get; }

		internal bool getMember(Sym name, out MemberDeclaration member) => membersMap.get(name, out member);

		public bool fastEquals(ClassDeclarationLike c) => object.ReferenceEquals(this, c);
		bool DeepEqual<TypeParameterOrigin>.deepEqual(TypeParameterOrigin o) => o is ClassDeclarationLike c && deepEqual(c);
		public abstract bool deepEqual(ClassDeclarationLike c);
		public abstract Dat toDat();
		TypeParameterOriginId Identifiable<TypeParameterOriginId>.getId() => getId();
		public abstract Id getId();

		internal sealed class Id : TypeParameterOriginId, ToData<Id> {
			// If this is a builtin, this will be missing.
			private readonly string id;
			Id(string id) { this.id = id; }
			internal static Id ofPath(Path path) => new Id(path.toPathString());
			internal static Id ofBuiltin(Sym name) => new Id(name.str);
			public override bool deepEqual(TypeParameterOriginId ti) => ti is Id i && deepEqual(i);
			public bool deepEqual(Id i) => id == i.id;
			public override Dat toDat() => Dat.str(id);
		}
	}

	sealed class Super : ModelElement, ToData<Super>, Identifiable<Super.Id> {
		internal readonly Loc loc;
		[ParentPointer] internal readonly ClassDeclaration containingClass;
		[UpPointer] internal readonly InstCls superClass;
		Late<Arr<Impl>> _impls;
		internal Arr<Impl> impls { get => _impls.get; set => _impls.set(value); }

		internal Super(Loc loc, ClassDeclaration klass, InstCls superClass) {
			this.loc = loc;
			this.containingClass = klass;
			this.superClass = superClass;
		}

		public bool deepEqual(Super s) =>
			containingClass.equalsId<ClassDeclaration, ClassDeclarationLike.Id>(s.containingClass) &&
			superClass.equalsId<InstCls, InstCls.Id>(s.superClass) &&
			impls.deepEqual(s.impls);
		public Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(superClass), superClass.getId(), nameof(impls), Dat.arr(impls));
		public Id getId() => new Id(containingClass.getId(), superClass.getId());

		internal struct Id : ToData<Id> {
			internal readonly ClassDeclarationLike.Id classId;
			internal readonly InstCls.Id superClassId;
			internal Id(ClassDeclarationLike.Id classId, InstCls.Id superClassId) {
				this.classId = classId;
				this.superClassId = superClassId;
			}

			public bool deepEqual(Id i) =>
				classId.deepEqual(i.classId) &&
				superClassId.deepEqual(i.superClassId);
			public Dat toDat() => Dat.of(this, nameof(classId), classId, nameof(superClassId), superClassId);
		}
	}

	interface MethodOrImplOrExpr {}

	interface MethodOrImpl : MethodOrImplOrExpr {
		MethodDeclaration implementedMethod { get; }
		ClassDeclaration klass { get; }
	}

	internal sealed class Impl : ModelElement, MethodOrImpl, ToData<Impl> {
		internal readonly Loc loc;
		[ParentPointer] internal readonly Super super;
		[UpPointer] internal readonly AbstractMethodLike implemented;
		Late<Expr> _body;
		internal Expr body {
			get => _body.get;
			set {
				value.parent = this;
				_body.set(value);
			}
		}

		ClassDeclaration MethodOrImpl.klass => super.containingClass;
		MethodDeclaration MethodOrImpl.implementedMethod => implemented;

		internal Impl(Super super, Loc loc, AbstractMethodLike implemented) {
			this.super = super;
			this.loc = loc;
			this.implemented = implemented;
		}

		public bool deepEqual(Impl i) =>
			loc.deepEqual(i.loc) &&
			implemented.equalsId<MethodDeclaration, MethodDeclaration.Id>(i.implemented) &&
			body.deepEqual(i.body);
		public Dat toDat() => Dat.of(this,
			nameof(loc), loc,
			nameof(super), super.getId(),
			nameof(implemented), implemented.getId(),
			nameof(body), body);
	}
}
