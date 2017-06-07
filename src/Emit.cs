using System;
using System.Reflection;
using System.Reflection.Emit;

using Model;
using static Utils;

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
			var a = e as Expr.Access;
			if (a != null) emitAccess(a);
			var l = e as Expr.Let;
			if (l != null) emitLet(l);
			var s = e as Expr.Seq;
			if (s != null) emitSeq(s);
			var li = e as Expr.Literal;
			if (li != null) emitLiteral(li);
			var sm = e as Expr.StaticMethodCall;
			if (sm != null) emitStaticMethodCall(sm);
			var g = e as Expr.GetSlot;
			if (g != null) emitGetSlot(g);
			throw TODO();
		}

		void emitAccess(Expr.Access e) {
			throw TODO();
		}

		void emitLet(Expr.Let l) {
			throw TODO();
		}

		void emitSeq(Expr.Seq s) {
			emitAny(s.action);
			emitAny(s.then);
			throw TODO();
		}

		void emitLiteral(Expr.Literal li) {
			throw TODO();
		}

		void emitStaticMethodCall(Expr.StaticMethodCall s) {
			throw TODO();
		}

		void emitGetSlot(Expr.GetSlot g) {
			throw TODO();
		}

		void op(OpCode op) {
			il.Emit(op);
		}
	}
}
