using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

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

	internal ILEmitter(bool shouldLog) {
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

	internal string getLogs(Model.Module module) => logs[module];

	internal Type emitModule(Model.Module module) {
		if (maps.tryGetAlreadyEmittedTypeForKlass(module.klass, out var b))
			return b;

		foreach (var im in module.imports)
			emitModule(im);

		return doCompileModule(module);
	}

	Type doCompileModule(Model.Module module) {
		var logger = logs != null ? new LogWriter() : null;

		var klass = module.klass;

		var interfaces = klass.supers.mapToArray<Type>(s => maps.toType(s.superClass));
		var typeBuilder = moduleBuilder.DefineType(klass.name.str, TypeAttributes.Public | typeFlags(klass.head), parent: null, interfaces: interfaces);

		maps.beginTypeBuilding(klass, typeBuilder.GetTypeInfo());

		fillHead(typeBuilder, klass, logger);
		fillMethodsAndSupers(typeBuilder, klass, logger);

		if (logger != null) logs[module] = logger.finish();

		var type = typeBuilder.CreateTypeInfo();
		maps.finishTypeBuilding(klass, type);
		return type;
	}

	static TypeAttributes typeFlags(Klass.Head head) {
		switch (head) {
			case Klass.Head.Static s:
				return TypeAttributes.Sealed;
			case Klass.Head.Abstract a:
				return TypeAttributes.Interface | TypeAttributes.Abstract;
			case Klass.Head.Slots s:
				return TypeAttributes.Sealed;
			default:
				throw unreachable();
		}
	}

	void fillHead(TypeBuilder tb, Klass klass, LogWriter logger) {
		switch (klass.head) {
			case Klass.Head.Static s:
				// Nothing to do for a static class.
				return;

			case Klass.Head.Abstract a:
				// Nothing to do.
				return;

			case Klass.Head.Slots slots:
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
		foreach (var method in klass.methods)
			maps.methodInfos.add(method, defineMethod(tb, method, methodAttributes(method), logger));

		foreach (var super in klass.supers) {
			foreach (var impl in super.impls) {
				if (klass.isAbstract) throw TODO(); // Will have to define a static method and invoke it from an instance method in all implementing classes.

				var mb = defineMethod(tb, impl.implemented, implAttributes, logger);
				logger?.beginMethod(mb);
				ILExprEmitter.emitMethodBody(maps, mb, impl.body, logger);
				logger?.endMethod();
			}
		}

		foreach (var method in klass.methods) {
			switch (method) {
				case Method.MethodWithBody mwb:
					var mb = (MethodBuilder)maps.methodInfos[mwb];
					logger?.beginMethod(mb);
					ILExprEmitter.emitMethodBody(maps, mb, mwb.body, logger);
					logger?.endMethod();
					break;
				case Method.AbstractMethod a:
					break;
				default:
					throw unreachable();
			}
		}
	}

	static bool needsSyntheticThis(Method m) =>
		m is Method.MethodWithBody mwb && needsSyntheticThis(mwb);
	static bool needsSyntheticThis(Method.MethodWithBody mwb) =>
		// Noze abstract classes map to IL interfaces.
		// So any instance methods on them need to become static methods.
		!mwb.isStatic && mwb.klass.isAbstract;

	MethodBuilder defineMethod(TypeBuilder tb, Method m, MethodAttributes attrs, /*nullable*/ LogWriter logger) {
		var first = needsSyntheticThis(m) ? Op.Some(maps.toType(m.klass)) : Op<Type>.None;
		var pms = m.parameters.mapToArrayWithFirst(first, p => maps.toType(p.ty));
		var rt = maps.toType(m.returnTy);
		logger?.methodHead(m.name.str, attrs, rt, pms);
		return tb.DefineMethod(m.name.str, attrs, rt, pms);
	}

	const MethodAttributes implAttributes = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final;

	static MethodAttributes methodAttributes(Method method) {
		var attr = MethodAttributes.Public;
		switch (method) {
			case Method.MethodWithBody mwb:
				if (mwb.isStatic || needsSyntheticThis(mwb)) // Abstract methods implemented as static methods.
					attr |= MethodAttributes.Static;
				else
					attr |= MethodAttributes.Final;
				return attr;
			case Method.AbstractMethod a:
				return attr | MethodAttributes.Abstract | MethodAttributes.Virtual;
			default:
				throw unreachable();
		}
	}
}

sealed class LogWriter : Logger {
	readonly StringBuilder sb = new StringBuilder();

	internal void beginConstructor() =>
		sb.Append("constructor");

	internal void endConstructor() =>
		sb.Append("\n\n");

	internal void methodHead(string name, MethodAttributes attrs, Type returnType, Type[] parameters) {
		sb.Append(returnType.Name);
		sb.Append(' ');
		sb.Append(name);
		sb.Append('(');
		sb.Append(string.Join<string>(", ", parameters.mapToArray(p => p.Name)));
		sb.Append(") [");
		sb.Append(attrs);
		sb.Append("]\n\n");
	}

	internal void beginMethod(MethodBuilder m) {
		sb.Append(m.Name);
	}
	internal void endMethod() =>
		sb.Append("\n\n");

	void Logger.log(string s) {
		sb.Append("\n\t");
		sb.Append(s);
	}

	internal string finish() =>
		sb.ToString();
}
