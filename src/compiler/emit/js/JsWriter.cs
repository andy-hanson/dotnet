using System;
using System.Text;

using static Utils;

class JsWriter : EmitTextWriter {
	readonly StringBuilder sb = new StringBuilder();
	uint line = 0;
	uint column = 0;
	uint indent = 0;

	internal static string writeToString(Estree.Program p) {
		var j = new JsWriter();
		j.writeProgram(p);
		return j.finish();
	}

	string finish() => sb.ToString();

	uint EmitTextWriter.curLine => line;
	uint EmitTextWriter.curColumn => column;

	void writeRaw(char ch) => sb.Append(ch);
	void writeRaw(string s) => sb.Append(s);

	void writeLine() {
		sb.Append('\n');
		doTimes(indent, () => sb.Append('\t'));
		line++;
		column = 0;
	}

	void doIndent() {
		indent++;
		writeLine();
	}

	void doDedent() {
		indent--;
		writeLine();
	}

	void writeProgram(Estree.Program program) {
		foreach (var statement in program.body) {
			writeStatement(statement);
			writeLine();
		}
	}

	//Remember to write ';' if necessary.
	void writeStatement(Estree.Statement s) {
		switch (s) {
			case Estree.BlockStatement b:
				writeBlockStatement(b);
				break;
			case Estree.ExpressionStatement e:
				writeExpr(e.expression);
				writeRaw(';');
				break;
			case Estree.FunctionDeclaration f:
				writeFunctionDeclarationOrExpression(f, Op.Some(f.id));
				break;
			case Estree.ReturnStatement r:
				writeReturn(r);
				break;
			case Estree.VariableDeclaration v:
				writeVariableDeclaration(v);
				break;
			case Estree.ClassDeclaration c:
				writeClass(c);
				break;
			case Estree.IfStatement i:
				writeIfStatement(i);
				break;
			case Estree.ThrowStatement t:
				writeThrowStatement(t);
				break;
			case Estree.TryStatement tr:
				writeTryStatement(tr);
				break;
			default:
				throw TODO();
		}
	}

	void writeCommaSeparatedList<T>(Arr<T> xs, Action<T> write) {
		if (xs.length == 0) return;
		write(xs.head);
		for (uint i = 1; i < xs.length; i++) {
			writeRaw(", ");
			write(xs[i]);
		}
	}

	void writeVariableDeclaration(Estree.VariableDeclaration v) {
		writeRaw(v.kindStr());
		writeRaw(' ');
		writeCommaSeparatedList(v.declarations, writeVariableDeclarator);
		writeRaw(';');
	}

	void writeVariableDeclarator(Estree.VariableDeclarator v) {
		writePattern(v.id);
		if (v.init.get(out var i)) {
			writeRaw(" = ");
			writeExpr(i);
		}
	}

	void writeIfStatement(Estree.IfStatement i) {
		writeRaw("if (");
		writeExpr(i.test);
		writeRaw(')');

		var consequentIsBlock = false;
		if (i.consequent is Estree.BlockStatement b) {
			consequentIsBlock = true;
			writeRaw(' ');
			writeBlockStatement(b);
		} else {
			this.doIndent();
			writeStatement(i.consequent);
			this.doDedent();
		}

		if (i.alternate.get(out var alt)) {
			if (consequentIsBlock)
				// Didn't finish in a newline, just `} else`
				writeRaw(' ');
			writeRaw("else ");
			writeStatement(alt);
		}
	}

	void writeThrowStatement(Estree.ThrowStatement t) {
		writeRaw("throw ");
		writeExpr(t.argument);
		writeRaw(';');
	}

	void writeTryStatement(Estree.TryStatement tr) {
		writeRaw("try ");
		writeBlockStatement(tr.block);
		if (tr.handler.get(out var h)) {
			writeRaw(" catch (");
			writePattern(h.param);
			writeRaw(") ");
			writeBlockStatement(h.body);
		}
		if (tr.finalizer.get(out var f)) {
			writeRaw(" finally ");
			writeBlockStatement(f);
		}
	}

	void writeExprOrSuper(Estree.ExpressionOrSuper e) {
		switch (e) {
			case Estree.Super s:
				writeRaw("super");
				break;
			case Estree.Expression ex:
				writeExpr(ex);
				break;
			default:
				throw unreachable();
		}
	}

	void writeExpr(Estree.Expression e) {
		switch (e) {
			case Estree.Identifier i:
				writeId(i);
				break;
			case Estree.Literal l:
				writeLiteral(l);
				break;
			case Estree.MemberExpression m:
				writeMemberExpression(m);
				break;
			case Estree.ThisExpression t:
				writeRaw("this");
				break;
			case Estree.ConditionalExpression c:
				writeConditional(c);
				break;
			case Estree.CallExpression c:
				writeCallOrNew(c);
				break;
			case Estree.NewExpression n:
				writeRaw("new ");
				writeCallOrNew(n);
				break;
			case Estree.FunctionExpression f:
				writeFunctionDeclarationOrExpression(f, Op<Estree.Identifier>.None);
				break;
			case Estree.ArrowFunctionExpression ar:
				writeArrowFunction(ar);
				break;
			case Estree.ClassExpression c:
				writeClass(c);
				break;
			case Estree.AssignmentExpression ae:
				writeAssignmentExpression(ae);
				break;
			case Estree.ObjectExpression o:
				writeObjectExpression(o);
				break;
			case Estree.UnaryExpression u:
				writeUnaryExpression(u);
				break;
			case Estree.BinaryExpression be:
				writeBinaryExpression(be);
				break;
			case Estree.AwaitExpression a:
				writeAwaitExpression(a);
				break;
			default:
				throw unreachable();
		}
	}

	void writeUnaryExpression(Estree.UnaryExpression e) {
		if (e.prefix) {
			writeRaw(e.@operator);
			writeParenthesized(e.argument);
		} else {
			writeParenthesized(e.argument);
			writeRaw(e.@operator);
		}
	}

	void writeParenthesized(Estree.Expression e) {
		writeRaw('(');
		writeExpr(e);
		writeRaw(')');
	}

	void writeParenthesizedIf(bool parenthesize, Estree.Expression e) {
		if (parenthesize) writeRaw('(');
		writeExpr(e);
		if (parenthesize) writeRaw(')');
	}

	void writeBinaryExpression(Estree.BinaryExpression e) {
		writeParenthesizedIf(e.left is Estree.BinaryExpression, e.left);
		writeRaw(" ");
		writeRaw(e.@operator);
		writeRaw(" ");
		writeParenthesizedIf(e.right is Estree.BinaryExpression, e.right);
	}

	void writeAwaitExpression(Estree.AwaitExpression a) {
		writeRaw("await ");
		writeExpr(a.argument);
	}

	void writeObjectExpression(Estree.ObjectExpression o) {
		if (o.properties.isEmpty) {
			writeRaw("{}");
			return;
		}

		writeRaw('{');
		indent++;
		foreach (var prop in o.properties) {
			writeLine();
			writeProperty(prop);
		}
		doDedent();
		writeRaw('}');
	}

	void writeProperty(Estree.Property p) {
		writeId(p.key);
		writeRaw(": ");
		writeExpr(p.value);
		writeRaw(',');
	}

	void writeMemberExpression(Estree.MemberExpression m) {
		writeExprOrSuper(m.@object);
		if (m.computed) {
			writeRaw('[');
			writeExpr(m.property);
			writeRaw(']');
		} else {
			writeRaw('.');
			writeId((Estree.Identifier)m.property);
		}
	}

	void writeAssignmentExpression(Estree.AssignmentExpression ae) {
		writePattern(ae.left);
		writeRaw(' ');
		writeRaw(ae.@operator);
		writeRaw(' ');
		writeExpr(ae.right);
	}

	void writeClass(Estree.Class c) {
		writeRaw("class ");
		writeId(c.id);
		if (c.superClass.get(out var sc)) {
			writeRaw(" extends ");
			writeExpr(sc);
		}
		writeRaw(" {");

		indent++;

		writeClassBody(c.body);

		doDedent();
		writeRaw("}");
	}

	void writeClassBody(Estree.ClassBody b) {
		foreach (var method in b.body) {
			writeLine();
			writeMethod(method);
		}
	}

	void writeMethod(Estree.MethodDefinition m) {
		if (m.@static)
			writeRaw("static ");

		if (m.value.async)
			writeRaw("async ");

		switch (m.kind) {
			case Estree.MethodDefinition.Kind.Get:
			case Estree.MethodDefinition.Kind.Set:
				writeRaw(m.kind == Estree.MethodDefinition.Kind.Get ? "get" : "set");
				writeRaw(' ');
				break;
			case Estree.MethodDefinition.Kind.Method:
			case Estree.MethodDefinition.Kind.Constructor:
				break;
			default:
				throw unreachable();
		}

		if (m.computed) {
			writeRaw('[');
			writeExpr(m.key);
			writeRaw(']');
		} else
			writeId((Estree.Identifier)m.key);

		writeFunctionOrMethodCommon(m.value);
	}

	void writeCallOrNew(Estree.CallOrNewExpression c) {
		writeExprOrSuper(c.callee);
		writeRaw('(');
		writeCommaSeparatedList(c.arguments, writeExpr);
		writeRaw(')');
	}

	void writeConditional(Estree.ConditionalExpression c) {
		writeExpr(c.test);
		doIndent();
		writeRaw("? ");
		writeExpr(c.alternate);
		writeLine();
		writeRaw(": ");
		writeExpr(c.consequent);
		doDedent();
	}

	void writeLiteral(Estree.Literal l) {
		switch (l.value) {
			case LiteralValue.Bool b:
				writeRaw(b.value ? "true" : "false");
				break;
			case LiteralValue.Int i:
				writeRaw(i.value.ToString());
				break;
			case LiteralValue.Float f:
				writeRaw(f.value.ToString());
				break;
			case LiteralValue.String s:
				writeQuotedString(s.value, writeRaw);
				break;
			case LiteralValue.Pass p:
				writeRaw("void 0");
				break;
		}
	}

	void writeParameters(Arr<Estree.Pattern> pms) {
		if (pms.length == 0) {
			writeRaw("()");
			return;
		}

		writeRaw('(');
		for (uint i = 0; i < pms.length - 1; i++) {
			writePattern(pms[i]);
			writeRaw(',');
		}
		writePattern(pms.last);
		writeRaw(") ");
	}

	void writeBlockStatementOrExpression(Estree.BlockStatementOrExpression be) {
		switch (be) {
			case Estree.BlockStatement b:
				writeBlockStatement(b);
				break;
			case Estree.Expression e:
				writeExpr(e);
				break;
			default:
				throw unreachable();
		}
	}

	void writeFunctionOrMethodCommon(Estree.FunctionDeclarationOrExpression f) {
		writeParameters(f.@params);
		writeBlockStatement(f.body);
	}

	void writeFunctionDeclarationOrExpression(Estree.FunctionDeclarationOrExpression f, Op<Estree.Identifier> id) {
		if (f.async)
			writeRaw("async ");
		writeRaw("function ");
		if (id.get(out var i)) writeId(i);
		writeFunctionOrMethodCommon(f);
	}

	void writeArrowFunction(Estree.ArrowFunctionExpression ar) {
		if (ar.async)
			writeRaw("async ");

		writeRaw('('); // Enclose in () for IIFE

		writeParameters(ar.@params);
		writeRaw(" => ");
		writeBlockStatementOrExpression(ar.body);

		writeRaw(')');
	}

	void writeReturn(Estree.ReturnStatement r) {
		writeRaw("return ");
		writeExpr(r.argument);
		writeRaw(';');
	}

	void writeBlockStatement(Estree.BlockStatement b) {
		if (b.body.length == 0) {
			writeRaw("{}");
			return;
		}

		writeRaw('{');
		indent++;
		foreach (var statement in b.body) {
			writeLine();
			writeStatement(statement);
		}
		doDedent();
		writeRaw('}');
	}

	void writePattern(Estree.Pattern p) {
		switch (p) {
			case Estree.Identifier i:
				writeId(i);
				break;
			case Estree.MemberExpression m:
				writeMemberExpression(m);
				break;
			default:
				throw unreachable();
		}
	}

	void writeId(Estree.Identifier i) {
		writeRaw(i.name.str);
	}
}
