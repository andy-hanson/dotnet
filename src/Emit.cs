using System;
using System.Reflection;
using System.Reflection.Emit;

using Model;

namespace Emit {
	static class Emit {
		internal static void writeBytecode(ModuleBuilder moduleBuilder, Klass klass, LineColumnGetter lineColumnGetter) {
			var tb = moduleBuilder.DefineType(klass.name.str, TypeAttributes.Public); //TODO: may need to store this with the class

			tb.CreateTypeInfo();
			//val bytes = classToBytecode(klass, lineColumnGetter);
			//set things on the klass

			//Create ourselves a class

		}

		static void foo(TypeBuilder tb, Klass klass) {
			var slots = ((Klass.Head.Slots) klass.head).slots;
			foreach (var slot in slots) {
				var fb = tb.DefineField(slot.name.str, slot.ty.toType(), FieldAttributes.Public);
			}

			foreach (var member in klass.members) {
				var method = (MethodWithBody) member; //todo: other members

				var mb = tb.DefineMethod(method.name.str, MethodAttributes.Public, method.returnTy.toType(),
					method.parameters.MapToArray(p => p.ty.toType()));
				var methIl = mb.GetILGenerator();
				new ExprEmitter(methIl).emitAny(method.body);
			}

			//Fields, constructors, etc.
		}

	}

	sealed class ExprEmitter {
		readonly ILGenerator il;
		internal ExprEmitter(ILGenerator il) {
			this.il = il;
		}

		internal void emitAny(Expr e) {
			switch (e.kind) {
				case ExprKind.Access:
					emit((Access) e);
					break;
				case ExprKind.Let:
					emit((Let) e);
					break;
				case ExprKind.Seq:
					emit((Seq) e);
					break;
				default:
					throw new Exception(e.kind.ToString());
			}
		}

		private void emit(Access e) {
			throw new NotImplementedException();
		}

		private void emit(Let l) {
			throw new NotImplementedException();
		}

		private void emit(Seq s) {
			emitAny(s.action);
			emitAny(s.then);
		}

		private void op(OpCode op) {
			il.Emit(op);
		}
	}
}
