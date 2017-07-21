using System;
using System.Diagnostics;
using System.Reflection;

using static Utils;

namespace Model {
	// `fun` or `def` or `impl`, or a builtin method.
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

		private Method(ClassLike klass, Loc loc, Ty returnTy, Sym name, Effect selfEffect, Arr<Parameter> parameters) : base(loc, name) {
			this.klass = klass;
			this.returnTy = returnTy;
			this.selfEffect = selfEffect;
			this.parameters = parameters;
		}

		internal uint arity => parameters.length;

		[DebuggerDisplay("BuiltinMethod{name}")]
		internal sealed class BuiltinMethod : Method, IEquatable<BuiltinMethod>, ToData<BuiltinMethod> {
			readonly bool _isStatic;
			internal readonly MethodInfo methodInfo;

			BuiltinMethod(BuiltinClass klass, MethodInfo methodInfo, bool isStatic, Ty returnTy, Sym name, Effect selfEffect, Arr<Parameter> @params)
				: base(klass, Loc.zero, returnTy, name, selfEffect, @params) {
				this._isStatic = isStatic;
				this.methodInfo = methodInfo;
			}

			internal static BuiltinMethod of(BuiltinClass klass, MethodInfo method) {
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

				var returnCls = BuiltinClass.fromDotNetType(method.ReturnType);
				var name = NameEscaping.unescapeMethodName(method.Name);
				var dotNetParams = method.GetParameters();
				if (isSpecialInstance)
					assert(dotNetParams[0].ParameterType == klass.dotNetType);
				var @params = dotNetParams.mapSlice(isSpecialInstance ? 1u : 0, (p, i) => getParam(p, i, allPure));

				return new BuiltinMethod(klass, method, isStatic, Ty.of(returnEffect, returnCls), name, selfEffect, @params);
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

			internal override bool isStatic => _isStatic;
			internal override bool isAbstract => methodInfo.IsAbstract;

			bool IEquatable<BuiltinMethod>.Equals(BuiltinMethod other) => object.ReferenceEquals(this, other);
			public override bool deepEqual(Method m) => m is BuiltinMethod b && deepEqual(b);
			public bool deepEqual(BuiltinMethod m) =>
				loc.deepEqual(m.loc) &&
				name.deepEqual(m.name) &&
				isStatic == m.isStatic &&
				returnTy.equalsId<Ty, Ty.Id>(m.returnTy) &&
				selfEffect == m.selfEffect &&
				parameters.deepEqual(m.parameters);
			public override Dat toDat() => Dat.of(this,
				nameof(name), name,
				nameof(isStatic), Dat.boolean(isStatic),
				nameof(returnTy), returnTy.getId(),
				nameof(selfEffect), Dat.str(selfEffect.show()),
				nameof(parameters), Dat.arr(parameters));
		}

		internal sealed class MethodWithBody : Method, ToData<MethodWithBody> {
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

			public override bool deepEqual(Method m) => m is MethodWithBody mb && deepEqual(mb);
			public bool deepEqual(MethodWithBody m) => throw TODO(); // TODO: should methods with different (reference identity) parent be not equal?
			public override Dat toDat() => Dat.of(this,
				nameof(loc), loc,
				nameof(isStatic), Dat.boolean(isStatic),
				nameof(returnTy), returnTy.getId(),
				nameof(name), name,
				nameof(selfEffect), Dat.str(selfEffect.show()),
				nameof(parameters), Dat.arr(parameters),
				nameof(body), body);
		}

		// Remember, BuiltinMethod can be abstract too!
		internal sealed class AbstractMethod : Method, ToData<AbstractMethod> {
			internal override bool isAbstract => true;
			internal override bool isStatic => false;

			internal AbstractMethod(Klass klass, Loc loc, Ty returnTy, Sym name, Effect selfEffect, Arr<Parameter> parameters)
				: base(klass, loc, returnTy, name, selfEffect, parameters) {}

			public override bool deepEqual(Method m) => m is AbstractMethod a && deepEqual(a);
			public bool deepEqual(AbstractMethod a) => throw TODO();
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(returnTy), returnTy.getId(), nameof(name), name, nameof(parameters), Dat.arr(parameters));
		}
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

		public bool deepEqual(Parameter p) => loc.deepEqual(p.loc) && ty.equalsId<Ty, Ty.Id>(p.ty) && name.deepEqual(p.name) && index == p.index;
		public Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(ty), ty.getId(), nameof(name), name, nameof(index), Dat.nat(index));
		Sym Identifiable<Sym>.getId() => name;
	}
}
