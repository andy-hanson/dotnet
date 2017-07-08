using System;
using System.Reflection;
using System.Reflection.Emit;

using static Utils;

namespace Test {
	static class DynamicImplementUtils {
		static readonly ModuleBuilder moduleBuilder;

		static DynamicImplementUtils() {
			var assemblyName = new AssemblyName(nameof(DynamicImplementUtils));
			var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndCollect);
			moduleBuilder = assemblyBuilder.DefineDynamicModule(nameof(DynamicImplementUtils));
		}

		//mv
		//Stores a reference to 'o' and redirects all calls to it.
		//'o' must match the signatures of the abstract methods on the type we're implementing.
		internal static object implementType(Type typeToImplement, object o) {
			assert(typeToImplement.IsInterface);

			const TypeAttributes attr = TypeAttributes.Public | TypeAttributes.Sealed;
			var implementer = moduleBuilder.DefineType($"{typeToImplement.Name}_implementer", attr, parent: null, interfaces: new Type[] { typeToImplement });

			var fieldType = o.GetType();
			assert(fieldType.IsSealed);

			var field = implementer.DefineField("implementation", fieldType, FieldAttributes.Public);

			addConstructor(implementer, field);

			var overrides = fieldType.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance);
			foreach (var implementationMethod in overrides) {
				var methodToOverride = typeToImplement.GetMethod(implementationMethod.Name);
				addOverriderMethod(implementer, implementationMethod, methodToOverride, field);
			}

			var type = implementer.CreateType();
			return Activator.CreateInstance(type, o);
		}

		static void addConstructor(TypeBuilder implementer, FieldInfo field) {
			var ctrBuilder = implementer.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new Type[] { field.FieldType });

			var il = new ILWriter(ctrBuilder);
			// `this.field = field;`
			il.getThis();
			il.getParameter(0);
			il.setField(field);
			il.ret();
		}

		static void addOverriderMethod(TypeBuilder implementer, MethodInfo implementationMethod, MethodInfo methodToOverride, FieldInfo field) {
			assert(methodToOverride != null);
			assert(methodToOverride.IsAbstract);

			assert(implementationMethod.ReturnType == methodToOverride.ReturnType);
			var paramTypes = implementationMethod.GetParameters().zip(methodToOverride.GetParameters(), (imParam, oParam) => {
				assert(imParam.ParameterType == oParam.ParameterType);
				return oParam.ParameterType;
			});

			var mb = implementer.DefineMethod(
				implementationMethod.Name,
				// Virtual *and* final. Virtual means: overrides something. Final means: Can't be overridden itself.
				MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final,
				methodToOverride.ReturnType,
				paramTypes);

			var il = new ILWriter(mb);
			var nParameters = unsigned(methodToOverride.GetParameters().Length);
			doTimes(nParameters, il.getParameter);

			il.getThis();
			il.getField(field);
			il.callNonVirtual(implementationMethod);
			il.ret();
		}
	}
}
