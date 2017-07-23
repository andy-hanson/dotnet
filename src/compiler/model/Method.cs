using System;
using System.Diagnostics;
using System.Reflection;

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

		[ParentPointer] internal readonly ClassLike klass;
		internal abstract bool isAbstract { get; }
		internal abstract bool isStatic { get; }
		[UpPointer] internal readonly Ty returnTy;
		internal readonly Effect selfEffect;
		internal readonly Arr<Parameter> parameters;

		public sealed override bool deepEqual(Member m) => m is Method mt && deepEqual(mt);
		public abstract bool deepEqual(Method m);
		public Id getId() => new Id(klass.getId(), name);

		protected Method(ClassLike klass, Loc loc, Ty returnTy, Sym name, Effect selfEffect, Arr<Parameter> parameters) : base(loc, name) {
			this.klass = klass;
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

	[DebuggerDisplay("BuiltinMethod{name}")]
	internal static class BuiltinMethod {
		internal static Method of(BuiltinClass klass, MethodInfo method) {
			bool isStatic;
			var isSpecialInstance = method.GetCustomAttribute<InstanceAttribute>() != null;
			if (isSpecialInstance) {
				assert(method.IsStatic);
				assert(klass.dotNetType.IsValueType);
				isStatic = false;
			} else {
				isStatic = method.IsStatic;
				if (!isStatic)
					// Struct instance methods should be implemented as static methods
					// so we don't have to pass by ref.
					assert(!klass.dotNetType.IsValueType);
			}

			var allPure = method.hasAttribute<AllPureAttribute>();
			var selfEffect = getEffect(method.GetCustomAttribute<SelfEffectAttribute>(), allPure);
			var returnEffect = getEffect(method.GetCustomAttribute<ReturnEffectAttribute>(), allPure);

			var returnTy = Ty.of(returnEffect, BuiltinClass.fromDotNetType(method.ReturnType));
			var name = NameEscaping.unescapeMethodName(method.Name);
			var dotNetParams = method.GetParameters();
			if (isSpecialInstance)
				assert(dotNetParams[0].ParameterType == klass.dotNetType);
			var @params = dotNetParams.mapSlice(isSpecialInstance ? 1u : 0, (p, i) => getParam(p, i, allPure));

			return method.IsAbstract
				? new BuiltinAbstractMethod(klass, method, returnTy, name, selfEffect, @params).upcast<Method>()
				: new BuiltinMethodWithBody(isStatic, klass, method, returnTy, name, selfEffect, @params);
		}

		static Parameter getParam(ParameterInfo p, uint index, bool allPure) {
			assert(!p.IsIn);
			assert(!p.IsLcid);
			assert(!p.IsOut);
			assert(!p.IsOptional);
			assert(!p.IsRetval);

			var effect = getEffect(p.GetCustomAttribute<ParameterEffectAttribute>(), allPure);
			var cls = BuiltinClass.fromDotNetType(p.ParameterType);
			return new Parameter(Loc.zero, Ty.of(effect, cls), Sym.of(p.Name), index);
		}

		static Effect getEffect(EffectLikeAttribute effectAttr, bool allPure) {
			if (effectAttr == null) {
				assert(allPure);
				return Effect.Pure;
			} else
				return effectAttr.effect;
		}
	}

	internal sealed class BuiltinMethodWithBody : MethodWithBodyLike, ToData<BuiltinMethodWithBody>, IEquatable<BuiltinMethodWithBody> {
		internal readonly MethodInfo methodInfo;
		readonly bool _isStatic;

		internal BuiltinMethodWithBody(bool isStatic, BuiltinClass klass, MethodInfo methodInfo, Ty returnTy, Sym name, Effect selfEffect, Arr<Parameter> @params)
			: base(klass, Loc.zero, returnTy, name, selfEffect, @params) {
			this.methodInfo = methodInfo;
			this._isStatic = isStatic;
		}

		internal override bool isStatic => _isStatic;
		internal override bool isAbstract => methodInfo.IsAbstract;

		internal override MemberId getMemberId() => throw new NotSupportedException();
		internal override string showKind() => throw new NotSupportedException();
		bool IEquatable<BuiltinMethodWithBody>.Equals(BuiltinMethodWithBody other) => object.ReferenceEquals(this, other);
		public override bool deepEqual(Method m) => m is BuiltinMethodWithBody b && deepEqual(b);
		public bool deepEqual(BuiltinMethodWithBody m) => throw new NotSupportedException();
		public override Dat toDat() => throw new NotSupportedException();
	}

	internal sealed class BuiltinAbstractMethod : AbstractMethodLike, ToData<BuiltinAbstractMethod> {
		internal readonly MethodInfo methodInfo;

		internal BuiltinAbstractMethod(BuiltinClass klass, MethodInfo methodInfo, Ty returnTy, Sym name, Effect selfEffect, Arr<Parameter> @params)
			: base(klass, Loc.zero, returnTy, name, selfEffect, @params) {
			assert(methodInfo.IsAbstract);
			this.methodInfo = methodInfo;
		}

		internal override bool isStatic => false;
		internal override bool isAbstract => true;

		internal override MemberId getMemberId() => throw new NotSupportedException();
		internal override string showKind() => throw new NotSupportedException();
		public override bool deepEqual(Method m) => m is BuiltinAbstractMethod b && deepEqual(b);
		public bool deepEqual(BuiltinAbstractMethod b) => throw new NotSupportedException();
		public override Dat toDat() => throw new NotSupportedException();
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
		internal override string showKind() => "method";
		public override bool deepEqual(Method m) => m is MethodWithBody mb && deepEqual(mb);
		public bool deepEqual(MethodWithBody m) => throw TODO(); // TODO: should methods with different (reference identity) parent be not equal?
		public override Dat toDat() => Dat.of(this,
			nameof(loc), loc,
			nameof(isStatic), Dat.boolean(isStatic),
			nameof(returnTy), returnTy.getTyId(),
			nameof(name), name,
			nameof(selfEffect), Dat.str(selfEffect.show()),
			nameof(parameters), Dat.arr(parameters),
			nameof(body), body);
	}

	internal sealed class AbstractMethod : AbstractMethodLike, ToData<AbstractMethod> {
		internal override bool isAbstract => true;
		internal override bool isStatic => false;

		internal override MemberId getMemberId() => throw TODO();
		internal override string showKind() => "abstract method";
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
