using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using Model;
using static Utils;

/**
Note: We *lazily* compile modules. But we must compile all of a module's imports before we compile it.
*/
sealed class ILEmitter {
	readonly AssemblyName assemblyName = new AssemblyName(nameof(ILEmitter));
	readonly AssemblyBuilder assemblyBuilder;
	readonly ModuleBuilder moduleBuilder;
	readonly EmitterMapsBuilder maps = new EmitterMapsBuilder();

	readonly /*nullable*/ Dictionary<Model.Module, string> logs;
	bool shouldLog => logs != null;

	ILEmitter(bool shouldLog) {
		assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndCollect);
		moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);
		if (shouldLog) this.logs = new Dictionary<Model.Module, string>();

		//??? https://stackoverflow.com/questions/17995945/how-to-debug-dynamically-generated-method
		/*var daType = typeof(DebuggableAttribute);
		var ctorInfo = daType.GetConstructor(new Type[] { typeof(DebuggableAttribute.DebuggingModes) });
		var caBuilder = new CustomAttributeBuilder(ctorInfo, new object[] {
			DebuggableAttribute.DebuggingModes.DisableOptimizations |
			DebuggableAttribute.DebuggingModes.Default
		});
		assemblyBuilder.SetCustomAttribute(caBuilder);*/
	}

	internal static Type emit(Model.Module module) {
		var e = new ILEmitter(shouldLog: false);
		return e.emitModule(module);
	}

	internal static (Type emittedRoot, Dict<Model.Module, string> logs) emitWithLogs(Model.Module module) {
		var e = new ILEmitter(shouldLog: true);
		var emittedRoot = e.emitModule(module);
		return (emittedRoot, new Dict<Model.Module, string>(e.logs));
	}

	Type emitModule(Model.Module module) {
		if (maps.tryGetAlreadyEmittedTypeForKlass(module.klass, out var b))
			return b;

		foreach (var import in module.imports)
			switch (import) {
				case Model.Module m:
					emitModule(m);
					break;
				case BuiltinClass _:
					break;
				default:
					throw unreachable();
			}

		return doCompileModule(module);
	}

	Type doCompileModule(Model.Module module) {
		var logger = logs != null ? new LogWriter() : null;

		var klass = module.klass;
		var typeBuilder = initType(klass);
		maps.beginTypeBuilding(klass, typeBuilder.GetTypeInfo());

		fillHead(typeBuilder, klass, logger);
		fillMethodsAndSupers(typeBuilder, klass, logger);

		if (logger != null) logs[module] = logger.finish();

		var type = typeBuilder.CreateTypeInfo();
		maps.finishTypeBuilding(klass, type);
		return type;
	}

	TypeBuilder initType(ClassDeclaration klass) {
		Type superClass;
		Type[] interfaces;
		if (klass.supers.find(out var es, s => s.superClass.classDeclaration == BuiltinClass.Exception)) {
			superClass = BuiltinClass.Exception.dotNetType;
			interfaces = klass.supers.mapDefinedToArray<Type>(s => s.superClass.classDeclaration == BuiltinClass.Exception ? Op<Type>.None : Op.Some(maps.toType(s.superClass)));
		} else {
			superClass = null;
			interfaces = klass.supers.mapToArray<Type>(s => maps.toType(s.superClass));
		}

		return moduleBuilder.DefineType(klass.name.str, TypeAttributes.Public | typeFlags(klass.head), parent: null, interfaces: interfaces);
	}

	static TypeAttributes typeFlags(ClassHead head) {
		switch (head) {
			case ClassHead.Static s:
				return TypeAttributes.Sealed;
			case ClassHead.Abstract a:
				return TypeAttributes.Interface | TypeAttributes.Abstract;
			case ClassHead.Slots s:
				return TypeAttributes.Sealed;
			default:
				throw unreachable();
		}
	}

	void fillHead(TypeBuilder tb, ClassDeclaration klass, LogWriter logger) {
		switch (klass.head) {
			case ClassHead.Static s:
				// Nothing to do for a static class.
				return;

			case ClassHead.Abstract a:
				foreach (var abstractMethod in a.abstractMethods) {
					var m = defineMethod(tb, abstractMethod, Attributes.@abstract, needsSyntheticThis: false, logger: logger);
					maps.methodInfos.add(abstractMethod, m);
				}
				return;

			case ClassHead.Slots slots:
				var fields = slots.slots.map<FieldInfo>(slot => {
					var field = tb.DefineField(slot.name.str, maps.toType(slot.ty), FieldAttributes.Public);
					maps.slotToField.add(slot, field);
					return field;
				});
				logger?.beginConstructor();
				generateConstructor(tb, klass, fields, logger);
				logger?.endConstructor();
				return;

			default:
				throw unreachable();
		}
	}

	void generateConstructor(TypeBuilder tb, ClassDeclaration klass, Arr<FieldInfo> fields, /*nullable*/ InstructionLogger logger) {
		//Constructor can't call any other methods, so we generate it eagerly.
		var parameterTypes = fields.mapToArray(f => f.FieldType);
		var ctr = tb.DefineConstructor(MethodAttributes.Private, CallingConventions.Standard, parameterTypes);
		var il = new ILWriter(ctr, logger);
		for (uint index = 0; index < fields.length; index++) {
			var field = fields[index];
			il.getThis();
			il.getParameter(index);
			il.setField(field);
		}
		il.ret();

		maps.classToConstructor.add(klass, ctr);
	}

	void fillMethodsAndSupers(TypeBuilder tb, ClassDeclaration klass, /*nullable*/ LogWriter logger) {
		foreach (var method in klass.methods) {
			// Methods in abstract classes are implemented as static methods on interfaces.
			// Noze abstract classes map to IL interfaces.
			// So any instance methods on them need to become static methods.
			var needsSyntheticThis = !method.isStatic && method.klass.isAbstract;
			var attr = method.isStatic || needsSyntheticThis ? Attributes.@static : Attributes.instance;
			maps.methodInfos.add(method, defineMethod(tb, method, attr, needsSyntheticThis, logger));
		}

		foreach (var super in klass.supers) {
			foreach (var impl in super.impls) {
				if (klass.isAbstract) throw TODO(); // Will have to define a static method and invoke it from an instance method in all implementing classes.

				var mb = defineMethod(tb, impl.implemented, Attributes.impl, needsSyntheticThis: false, logger: logger);
				logger?.beginMethod(mb);
				ILExprEmitter.emitMethodBody(maps, mb, impl.body, logger);
				logger?.endMethod();
			}
		}

		foreach (var method in klass.methods) {
			var mb = (MethodBuilder)maps.methodInfos[method];
			logger?.beginMethod(mb);
			ILExprEmitter.emitMethodBody(maps, mb, method.body, logger);
			logger?.endMethod();
		}
	}

	MethodBuilder defineMethod(TypeBuilder tb, MethodDeclaration m, MethodAttributes attrs, bool needsSyntheticThis, /*nullable*/ LogWriter logger) {
		var first = needsSyntheticThis ? Op.Some(maps.toClassType(m.klass)) : Op<Type>.None;

		var mb = tb.DefineMethod(m.name.str, attrs);

		Op<GenericTypeParameterBuilder[]> ilTypeParameters;
		if (m.typeParameters.any) {
			var ilTypeParameterz = mb.DefineGenericParameters(m.typeParameters.mapToArray(t => t.name.str));
			maps.associateTypeParameters(m.typeParameters, ilTypeParameterz);
			ilTypeParameters = Op.Some(ilTypeParameterz);
		} else
			ilTypeParameters = Op<GenericTypeParameterBuilder[]>.None;

		var @params = m.parameters.mapToArrayWithFirst(first, p => maps.toType(p.ty));
		var returnTy = maps.toType(m.returnTy);
		mb.SetParameters(@params);
		mb.SetReturnType(returnTy);
		logger?.methodHead(m.name.str, ilTypeParameters, attrs, returnTy, @params);

		return mb;
	}
}

static class Attributes {
	internal const MethodAttributes @static = MethodAttributes.Public | MethodAttributes.Static;
	internal const MethodAttributes instance = MethodAttributes.Public | MethodAttributes.Final;
	internal const MethodAttributes @abstract = MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual;
	internal const MethodAttributes impl = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final;
}

sealed class LogWriter : InstructionLogger {
	internal readonly StringMaker s = StringMaker.create();

	internal void beginConstructor() =>
		s.add("constructor");

	internal void endConstructor() =>
		s.add("\n\n");

	internal void methodHead(string name, Op<GenericTypeParameterBuilder[]> typeParameters, MethodAttributes attrs, Type returnType, Type[] parameters) {
		s.add(returnType.Name)
			.add(' ')
			.add(name);

		if (typeParameters.get(out var tps)) {
			s.add('<');
			s.join(tps, (ss, t) => ss.add(t.Name));
			s.add('>');
		}

		s.add('(')
			.join(parameters, p => p.Name)
			.add(") [")
			.add(attrs.ToString())
			.add("]\n\n");
	}

	internal void beginMethod(MethodBuilder m) =>
		s.add(m.Name);
	internal void endMethod() =>
		s.add("\n\n");

	StringMaker InstructionLogger.log() =>
		// Begin each instruction with "\n\t"
		s.add("\n\t");

	internal string finish() =>
		s.finish();
}
