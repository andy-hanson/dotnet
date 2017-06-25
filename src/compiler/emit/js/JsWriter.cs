using System;
using System.Text;

using static Utils;

class JsWriter : EmitTextWriter {
	uint line = 0;
	uint column = 0;
	uint indent = 0;
	StringBuilder sb = new StringBuilder();

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

	//Remember to write ';'
	void writeStatement(Estree.Statement s) {
		switch (s) {
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
			default:
				throw TODO();
		}
	}

	void writeCommaSeparatedList<T>(Arr<T> xs, Action<T> write) {
		if (xs.length == 0) return;
		write(xs[0]);
		for (uint i = 1; i < xs.length; i++) {
			writeRaw(", ");
			write(xs[i]);
		}
	}

	void writeVariableDeclaration(Estree.VariableDeclaration v) {
		writeRaw(v.kind);
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

	void writeExpr(Estree.Expression e) {
		switch (e) {
			case Estree.Identifier i:
				writeId(i);
				break;
			case Estree.Literal l:
				writeLiteral(l);
				break;
			case Estree.MemberExpression m:
				writeMember(m);
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
			case Estree.ArrowFunctionExpression a:
				writeArrowFunction(a);
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
			default:
				throw unreachable();
		}
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

	void writeMember(Estree.MemberExpression m) {
		writeExpr(m.@object);
		writeRaw('.');
		writeId(m.property);
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

		switch (m.kind) {
			case "get":
			case "set":
				writeRaw(m.kind);
				writeRaw(' ');
				break;
			case "method":
			case "constructor":
				break;
			default:
				throw unreachable();
		}

		writeId(m.key);
		writeFunctionOrMethodCommon(m.value);
	}

	void writeCallOrNew(Estree.CallOrNewExpression c) {
		writeExpr(c.callee);
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
			case Model.Expr.Literal.LiteralValue.Bool b:
				writeRaw(b.value ? "true" : "false");
				break;
			case Model.Expr.Literal.LiteralValue.Int i:
				writeRaw(i.value.ToString());
				break;
			case Model.Expr.Literal.LiteralValue.Float f:
				writeRaw(f.value.ToString());
				break;
			case Model.Expr.Literal.LiteralValue.Str s:
				writeQuotedString(s.value, writeRaw);
				break;
			case Model.Expr.Literal.LiteralValue.Pass p:
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

	void writeArrowFunction(Estree.ArrowFunctionExpression a) {
		writeParameters(a.@params);
		writeRaw("=> ");
		writeBlockStatementOrExpression(a.body);
	}

	void writeBlockStatementOrExpression(Estree.BlockStatementOrExpression be) {
		switch (be) {
			case Estree.BlockStatement b:
				writeBlock(b);
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
		writeBlock(f.body);
	}

	void writeFunctionDeclarationOrExpression(Estree.FunctionDeclarationOrExpression f, Op<Estree.Identifier> id) {
		writeRaw("function ");
		id.each(writeId);
		writeFunctionOrMethodCommon(f);
	}

	void writeReturn(Estree.ReturnStatement r) {
		writeRaw("return ");
		writeExpr(r.argument);
		writeRaw(';');
	}

	void writeBlock(Estree.BlockStatement b) {
		if (b.body.length == 0) {
			writeRaw("{}");
			return;
		}

		writeRaw('{');
		doIndent();
		foreach (var statement in b.body)
			writeStatement(statement);
		doDedent();
		writeRaw('}');
	}

	void writePattern(Estree.Pattern p) {
		switch (p) {
			case Estree.Identifier i:
				writeId(i);
				break;
			case Estree.MemberExpression m:
				writeMember(m);
				break;
			default:
				throw unreachable();
		}
	}

	void writeId(Estree.Identifier i) {
		writeRaw(i.name.str);
	}
}