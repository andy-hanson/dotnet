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

		foreach (var im in module.imports)
			emitModule(im);

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

	TypeBuilder initType(Klass klass) {
		Type superClass;
		Type[] interfaces;
		if (klass.supers.find(out var es, s => s.superClass == BuiltinClass.Exception)) {
			superClass = BuiltinClass.Exception.dotNetType;
			interfaces = klass.supers.mapDefinedToArray<Type>(s => s.superClass == BuiltinClass.Exception ? Op<Type>.None : Op.Some(maps.toType(s.superClass)));
		} else {
			superClass = null;
			interfaces = klass.supers.mapToArray<Type>(s => maps.toType(s.superClass));
		}

		return moduleBuilder.DefineType(klass.name.str, TypeAttributes.Public | typeFlags(klass.head), parent: null, interfaces: interfaces);
	}

	static TypeAttributes typeFlags(KlassHead head) {
		switch (head) {
			case KlassHead.Static s:
				return TypeAttributes.Sealed;
			case KlassHead.Abstract a:
				return TypeAttributes.Interface | TypeAttributes.Abstract;
			case KlassHead.Slots s:
				return TypeAttributes.Sealed;
			default:
				throw unreachable();
		}
	}

	void fillHead(TypeBuilder tb, Klass klass, LogWriter logger) {
		switch (klass.head) {
			case KlassHead.Static s:
				// Nothing to do for a static class.
				return;

			case KlassHead.Abstract a:
				foreach (var abstractMethod in a.abstractMethods) {
					var m = defineMethod(tb, abstractMethod, Attributes.@abstract, needsSyntheticThis: false, logger: logger);
					maps.methodInfos.add(abstractMethod, m);
				}
				return;

			case KlassHead.Slots slots:
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

	void generateConstructor(TypeBuilder tb, Klass klass, Arr<FieldInfo> fields, /*nullable*/ Logger logger) {
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

	void fillMethodsAndSupers(TypeBuilder tb, Klass klass, /*nullable*/ LogWriter logger) {
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

	MethodBuilder defineMethod(TypeBuilder tb, Method m, MethodAttributes attrs, bool needsSyntheticThis, /*nullable*/ LogWriter logger) {
		var first = needsSyntheticThis ? Op.Some(maps.toType(m.klass)) : Op<Type>.None;
		var @params = m.parameters.mapToArrayWithFirst(first, p => maps.toType(p.ty));
		var returnTy = maps.toType(m.returnTy);
		logger?.methodHead(m.name.str, attrs, returnTy, @params);
		return tb.DefineMethod(m.name.str, attrs, returnTy, @params);
	}
}

static class Attributes {
	internal const MethodAttributes @static = MethodAttributes.Public | MethodAttributes.Static;
	internal const MethodAttributes instance = MethodAttributes.Public | MethodAttributes.Final;
	internal const MethodAttributes @abstract = MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual;
	internal const MethodAttributes impl = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final;
}

sealed class LogWriter : Logger {
	readonly StringMaker s = StringMaker.create();

	internal void beginConstructor() =>
		s.add("constructor");

	internal void endConstructor() =>
		s.add("\n\n");

	internal void methodHead(string name, MethodAttributes attrs, Type returnType, Type[] parameters) {
		s.add(returnType.Name).add(' ').add(name).add('(');
		new Arr<Type>(parameters).join(", ", s, (ss, p) => ss.add(p.Name));
		s.add(") [");
		s.add(attrs.ToString());
		s.add("]\n\n");
	}

	internal void beginMethod(MethodBuilder m) =>
		s.add(m.Name);
	internal void endMethod() =>
		s.add("\n\n");

	void Logger.log(string str) =>
		s.add("\n\t").add(str);

	internal string finish() =>
		s.finish();
}
