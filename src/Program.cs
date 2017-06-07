using System;
using System.Reflection;
using System.Reflection.Emit;
//dotnet add package System.Runtime
//dotnet add package System.Runtime.Loader
//dotnet add package System.Reflection.Emit
//dotnet add package System.Collections.Immutable

namespace main {
	class Program {
		static void Main(string[] args) {
			TestCompiler();
		}

		static void TestCompiler() {
			//var text = File.ReadAllText("sample/A.nz");
			//Parser.parse(Sym.of("A"), text);
			var rootDir = Path.empty;


			var cmp = new Compiler(new DefaultCompilerHost(rootDir));
			var x = cmp.compile(Path.from("sample", "A"));

			/*
			var aName = new AssemblyName("Example");
			var ab = AssemblyBuilder.DefineDynamicAssembly(aName, AssemblyBuilderAccess.RunAndCollect);
			var mb = ab.DefineDynamicModule(aName.Name);

			Console.WriteLine("main");
			//var m = new Model.Module();

			//Model.Emit.writeBytecode(mb, klass, );
			*/
		}


		static void AddCtr(TypeBuilder tb, FieldBuilder fbNumber) {
			Type[] parameterTypes = { typeof(int) };
			var ctor1 = tb.DefineConstructor(
				MethodAttributes.Public,
				CallingConventions.Standard,
				parameterTypes);
			var ctor1IL = ctor1.GetILGenerator();
			ctor1IL.Emit(OpCodes.Ldarg_0);
			ctor1IL.Emit(OpCodes.Call,
				typeof(object).GetConstructor(Type.EmptyTypes));
			ctor1IL.Emit(OpCodes.Ldarg_0);
			ctor1IL.Emit(OpCodes.Ldarg_1);
			ctor1IL.Emit(OpCodes.Stfld, fbNumber);
			ctor1IL.Emit(OpCodes.Ret);

			var ctor0 = tb.DefineConstructor(
				MethodAttributes.Public,
				CallingConventions.Standard,
				Type.EmptyTypes);

			ILGenerator ctor0IL = ctor0.GetILGenerator();
			// For a constructor, argument zero is a reference to the new
			// instance. Push it on the stack before pushing the default
			// value on the stack, then call constructor ctor1.
			ctor0IL.Emit(OpCodes.Ldarg_0);
			ctor0IL.Emit(OpCodes.Ldc_I4_S, 42);
			ctor0IL.Emit(OpCodes.Call, ctor1);
			ctor0IL.Emit(OpCodes.Ret);
		}

		static void Test() {
			var aName = new AssemblyName("Example");
			//var ab = AppDomain.CurrentDomain.DefineDynamicAssembly(
			//    aName,
			//    AssemblyBuilderAccess.RunAndSave);

			var ab = AssemblyBuilder.DefineDynamicAssembly(aName, AssemblyBuilderAccess.RunAndCollect);
			var mb = ab.DefineDynamicModule(aName.Name);
			var tb = mb.DefineType("MyType", TypeAttributes.Public);
			var fbNumber = tb.DefineField("num", typeof(int), FieldAttributes.Private);

			AddCtr(tb, fbNumber);

			var pbNumber = tb.DefineProperty(
				"Number",
				PropertyAttributes.HasDefault,
				typeof(int),
				null);

			var getSetAttr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
			var mbNumberGetAccessor = tb.DefineMethod(
				"get_Number",
				getSetAttr,
				typeof(int),
				Type.EmptyTypes);

			ILGenerator numberGetIL = mbNumberGetAccessor.GetILGenerator();
			// For an instance property, argument zero is the instance. Load the
			// instance, then load the private field and return, leaving the
			// field value on the stack.
			numberGetIL.Emit(OpCodes.Ldarg_0);
			numberGetIL.Emit(OpCodes.Ldfld, fbNumber);
			numberGetIL.Emit(OpCodes.Ret);

			// Define the "set" accessor method for Number, which has no return
			// type and takes one argument of type int (Int32).
			MethodBuilder mbNumberSetAccessor = tb.DefineMethod(
				"set_Number",
				getSetAttr,
				null,
				new Type[] { typeof(int) });

			ILGenerator numberSetIL = mbNumberSetAccessor.GetILGenerator();
			// Load the instance and then the numeric argument, then store the
			// argument in the field.
			numberSetIL.Emit(OpCodes.Ldarg_0);
			numberSetIL.Emit(OpCodes.Ldarg_1);
			numberSetIL.Emit(OpCodes.Stfld, fbNumber);
			numberSetIL.Emit(OpCodes.Ret);

			// Last, map the "get" and "set" accessor methods to the
			// PropertyBuilder. The property is now complete.
			pbNumber.SetGetMethod(mbNumberGetAccessor);
			pbNumber.SetSetMethod(mbNumberSetAccessor);

			// Define a method that accepts an integer argument and returns
			// the product of that integer and the private field m_number. This
			// time, the array of parameter types is created on the fly.
			MethodBuilder meth = tb.DefineMethod(
				"MyMethod",
				MethodAttributes.Public,
				typeof(int),
				new Type[] { typeof(int) });

			ILGenerator methIL = meth.GetILGenerator();
			// To retrieve the private instance field, load the instance it
			// belongs to (argument zero). After loading the field, load the
			// argument one and then multiply. Return from the method with
			// the return value (the product of the two numbers) on the
			// execution stack.
			methIL.Emit(OpCodes.Ldarg_0);
			methIL.Emit(OpCodes.Ldfld, fbNumber);
			methIL.Emit(OpCodes.Ldarg_1);
			methIL.Emit(OpCodes.Mul);
			methIL.Emit(OpCodes.Ret);

			var t = tb.CreateTypeInfo();

			//ab.Save(aName.Name + ".dll");

			MethodInfo mi = t.GetMethod("MyMethod");
			PropertyInfo pi = t.GetProperty("Number");

			object o1 = Activator.CreateInstance(t.AsType());

			Console.WriteLine(pi.GetValue(o1, null));

			//AssemblyLoadContext c = AssemblyLoadContext.Default;
			//Assembly asm = c.LoadFromStream(ms);
			//Type t = asm.GetType("MyType");
		}
	}
}
