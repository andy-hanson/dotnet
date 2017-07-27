using System;
using System.Diagnostics;
using System.Reflection;

using BuiltinAttributes;
using static Utils;

namespace Model {
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

			var returnTy = Ty.of(returnEffect, BuiltinsLoader.fromDotNetType(method.ReturnType));
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
			var cls = BuiltinsLoader.fromDotNetType(p.ParameterType);
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
		internal override string showKind(bool upper) => throw new NotSupportedException();
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
		internal override string showKind(bool upper) => throw new NotSupportedException();
		public override bool deepEqual(Method m) => m is BuiltinAbstractMethod b && deepEqual(b);
		public bool deepEqual(BuiltinAbstractMethod b) => throw new NotSupportedException();
		public override Dat toDat() => throw new NotSupportedException();
	}
}
