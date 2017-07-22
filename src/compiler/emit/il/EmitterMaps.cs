using System;
using System.Collections.Generic;
using System.Reflection;

using Model;
using static Utils;

interface EmitterMaps {
	MethodInfo getMethodInfo(Method method);
	ConstructorInfo getConstructorInfo(Klass klass);
	FieldInfo getFieldInfo(Slot slot);
	Type toType(Ty ty); // Just calls toType(ty.cls)
	Type toType(ClsRef cls);
}

sealed class EmitterMapsBuilder : EmitterMaps {
	// This will not be filled for a BuiltinMethod
	readonly Dictionary<Klass, TypeBuilding> typeInfos = new Dictionary<Klass, TypeBuilding>();
	internal readonly Dict.Builder<Method, MethodInfo> methodInfos = Dict.builder<Method, MethodInfo>();
	internal readonly Dict.Builder<Klass, ConstructorInfo> classToConstructor = Dict.builder<Klass, ConstructorInfo>();
	internal readonly Dict.Builder<Slot, FieldInfo> slotToField = Dict.builder<Slot, FieldInfo>();

	struct TypeBuilding {
		internal readonly TypeInfo info;
		readonly Op<Type> _type;
		internal Type type => _type.force;

		internal TypeBuilding(TypeInfo info) { this.info = info; this._type = Op<Type>.None; }
		internal TypeBuilding(TypeInfo info, Type type) { this.info = info; this._type = Op.Some(type); }
		internal TypeBuilding withType(Type type) => new TypeBuilding(this.info, type);
	}

	MethodInfo EmitterMaps.getMethodInfo(Method method) {
		switch (method) {
			case BuiltinMethodWithBody b:
				return b.methodInfo;
			case BuiltinAbstractMethod b:
				return b.methodInfo;
			default:
				return methodInfos[method];
		}
	}

	ConstructorInfo EmitterMaps.getConstructorInfo(Klass klass) => classToConstructor[klass];
	FieldInfo EmitterMaps.getFieldInfo(Slot slot) => slotToField[slot];

	internal bool tryGetAlreadyEmittedTypeForKlass(Klass k, out Type t) {
		var res = typeInfos.TryGetValue(k, out var tb);
		t = res ? tb.type : null;
		return res;
	}

	internal void beginTypeBuilding(Klass klass, TypeInfo info) =>
		typeInfos[klass] = new TypeBuilding(info);

	internal void finishTypeBuilding(Klass klass, Type type) =>
		typeInfos[klass] = typeInfos[klass].withType(type);

	Type EmitterMaps.toType(Ty ty) => toType(ty);
	Type EmitterMaps.toType(ClsRef cls) => toType(cls);
	internal Type toType(Ty ty) => toType(ty.cls);
	internal Type toType(ClsRef cls) {
		switch (cls) {
			case BuiltinClass b:
				return b.dotNetType;
			case Klass k:
				return typeInfos[k].info;
			default:
				throw TODO();
		}
	}

	internal FieldInfo getSlotField(Slot slot) => slotToField[slot];
}
