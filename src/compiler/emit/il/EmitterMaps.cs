using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using Model;
using static Utils;

interface EmitterMaps {
	MethodInfo getMethodInfo(MethodInst method);
	ConstructorInfo getConstructorInfo(ClassDeclaration klass);
	FieldInfo getFieldInfo(SlotDeclaration slot);
	Type toType(Ty ty);
	Type toType(InstCls cls);
}

sealed class EmitterMapsBuilder : EmitterMaps {
	// This will not be filled for a BuiltinMethod
	readonly Dictionary<ClassDeclaration, TypeBuilding> typeInfos = new Dictionary<ClassDeclaration, TypeBuilding>();
	readonly Dictionary<TypeParameter, Type> typeParameterToTypeInfo = new Dictionary<TypeParameter, Type>();
	internal readonly Dict.Builder<MethodDeclaration, MethodInfo> methodInfos = Dict.builder<MethodDeclaration, MethodInfo>();
	internal readonly Dict.Builder<ClassDeclaration, ConstructorInfo> classToConstructor = Dict.builder<ClassDeclaration, ConstructorInfo>();
	internal readonly Dict.Builder<SlotDeclaration, FieldInfo> slotToField = Dict.builder<SlotDeclaration, FieldInfo>();

	struct TypeBuilding {
		internal readonly TypeInfo info;
		readonly Op<Type> _type;
		internal Type type => _type.force;

		internal TypeBuilding(TypeInfo info) { this.info = info; this._type = Op<Type>.None; }
		internal TypeBuilding(TypeInfo info, Type type) { this.info = info; this._type = Op.Some(type); }
		internal TypeBuilding withType(Type type) => new TypeBuilding(this.info, type);
	}

	MethodInfo EmitterMaps.getMethodInfo(MethodInst method) {
		var (methodDecl, typeArguments) = method;
		var info = getMethodInfo(methodDecl);
		return typeArguments.any ? info.MakeGenericMethod(typeArguments.mapToArray(toType)) : info;
	}

	MethodInfo getMethodInfo(MethodDeclaration methodDecl) {
		switch (methodDecl) {
			case BuiltinMethodWithBody b:
				return b.methodInfo;
			case BuiltinAbstractMethod b:
				return b.methodInfo;
			default:
				return methodInfos[methodDecl];
		}
	}

	ConstructorInfo EmitterMaps.getConstructorInfo(ClassDeclaration klass) => classToConstructor[klass];
	FieldInfo EmitterMaps.getFieldInfo(SlotDeclaration slot) => slotToField[slot];

	internal bool tryGetAlreadyEmittedTypeForKlass(ClassDeclaration k, out Type t) {
		var res = typeInfos.TryGetValue(k, out var tb);
		t = res ? tb.type : null;
		return res;
	}

	internal void beginTypeBuilding(ClassDeclaration klass, TypeInfo info) =>
		typeInfos[klass] = new TypeBuilding(info);

	internal void finishTypeBuilding(ClassDeclaration klass, Type type) =>
		typeInfos[klass] = typeInfos[klass].withType(type);

	Type EmitterMaps.toType(Ty ty) => toType(ty);
	Type EmitterMaps.toType(InstCls cls) => toType(cls);

	internal void associateTypeParameters(Arr<TypeParameter> typeParameters, GenericTypeParameterBuilder[] ilTypeParameters) {
		assert(typeParameters.length == ilTypeParameters.Length);
		for (uint i = 0; i < typeParameters.length; i++)
			typeParameterToTypeInfo.Add(typeParameters[i], ilTypeParameters[i]);
	}

	internal Type toClassType(ClassDeclarationLike classDeclaration) {
		switch (classDeclaration) {
			case BuiltinClass b:
				return b.dotNetType;
			case ClassDeclaration k:
				return typeInfos[k].info;
			default:
				throw TODO();
		}
	}

	internal Type toType(Ty ty) {
		switch (ty) {
			case TypeParameter tp:
				return typeParameterToTypeInfo[tp];

			case PlainTy pt:
				return toType(pt.instantiatedClass);

			case BogusTy _:
			default:
				throw unreachable();
		}
	}

	internal Type toType(InstCls cls) {
		var (classDeclaration, typeArguments) = cls;
		if (typeArguments.any)
			throw TODO();

		return toClassType(classDeclaration);
	}

	internal FieldInfo getSlotField(SlotDeclaration slot) => slotToField[slot];
}
