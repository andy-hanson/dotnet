using System;

// https://github.com/estree/estree
namespace Estree {
	abstract class Node {
		internal readonly Loc loc;
		public override bool Equals(object o) => throw new NotImplementedException();
		public override int GetHashCode() => throw new NotImplementedException();
		internal Node(Loc loc) { this.loc = loc; }
	}

	interface BlockStatementOrExpression {}
	interface Expression : BlockStatementOrExpression, DeclarationOrExpression {}
	interface Pattern {}

	sealed class Identifier : Node, Expression, Pattern {
		internal readonly Sym name;
		internal Identifier(Loc loc, Sym name) : base(loc) { this.name = name; }
	}

	sealed class Literal : Node, Expression {
		internal readonly Model.Expr.Literal.LiteralValue value;
		internal Literal(Loc loc, Model.Expr.Literal.LiteralValue value) : base(loc) { this.value = value; }
	}

	sealed class Program : Node {
		internal readonly Arr<Statement> body;
		internal Program(Loc loc, Arr<Statement> body) : base(loc) { this.body = body; }
	}

	abstract class Function : Node {
		internal readonly Arr<Pattern> @params;
		internal Function(Loc loc, Arr<Pattern> @params) : base(loc) {
			this.@params = @params;
		}
	}

	abstract class FunctionDeclarationOrExpression : Function {
		internal readonly BlockStatement body;
		internal FunctionDeclarationOrExpression(Loc loc, Arr<Pattern> @params, BlockStatement body) : base(loc, @params) {
			this.body = body;
		}
	}

	sealed class FunctionExpression : FunctionDeclarationOrExpression, Expression {
		internal FunctionExpression(Loc loc, Arr<Pattern> @params, BlockStatement body) : base(loc, @params, body) {}
	}

	sealed class FunctionDeclaration : FunctionDeclarationOrExpression, Declaration {
		internal readonly Identifier id;
		internal FunctionDeclaration(Loc loc, Identifier id, Arr<Pattern> @params, BlockStatement body) : base(loc, @params, body) {
			this.id = id;
		}
	}

	sealed class ArrowFunctionExpression : Function, Expression {
		internal readonly BlockStatementOrExpression body;
		internal ArrowFunctionExpression(Loc loc, Arr<Pattern> @params, BlockStatementOrExpression body) : base(loc, @params) {
			this.body = body;
		}
	}

	interface Statement {}

	sealed class ExpressionStatement : Node, Statement {
		internal readonly Expression expression;
		internal ExpressionStatement(Loc loc, Expression expression) : base(loc) {
			this.expression = expression;
		}
	}

	sealed class BlockStatement : Node, Statement, BlockStatementOrExpression {
		internal readonly Arr<Statement> body;
		internal BlockStatement(Loc loc, Arr<Statement> body) : base(loc) {
			this.body = body;
		}

		internal static BlockStatement empty(Loc loc) => new BlockStatement(loc, Arr.empty<Statement>());
	}

	sealed class ReturnStatement : Node, Statement {
		internal readonly Expression argument;
		internal ReturnStatement(Loc loc, Expression argument) : base(loc) {
			this.argument = argument;
		}
	}

	interface DeclarationOrExpression {}

	interface Declaration : Statement, DeclarationOrExpression {}

	sealed class VariableDeclaration : Node, Declaration {
		internal readonly Arr<VariableDeclarator> declarations;
		internal string kind; // "var" | "let" | "const"
		internal VariableDeclaration(Loc loc, Arr<VariableDeclarator> declarations, string kind) : base(loc) {
			this.declarations = declarations;
			this.kind = kind;
		}
		internal static VariableDeclaration var(Loc loc, Identifier id, Expression init) =>
			new VariableDeclaration(loc, Arr.of(new VariableDeclarator(loc, id, Op.Some(init))), "var");
		internal static VariableDeclaration var(Loc loc, Sym id, Expression init) =>
			var(loc, new Identifier(loc, id), init);
	}

	sealed class VariableDeclarator : Node {
		internal readonly Pattern id;
		internal readonly Op<Expression> init;
		internal VariableDeclarator(Loc loc, Pattern id, Op<Expression> init) : base(loc) {
			this.id = id;
			this.init = init;
		}
	}

	sealed class ThisExpression : Node, Expression {
		internal ThisExpression(Loc loc) : base(loc) {}
	}

	sealed class MemberExpression : Node, Expression, Pattern {
		internal readonly Expression @object;
		internal readonly Identifier property;
		internal readonly bool computed;
		internal MemberExpression(Loc loc, Expression @object, Identifier property) : base(loc) {
			this.@object = @object;
			this.property = property;
			this.computed = false;
		}
		internal static MemberExpression simple(Loc loc, Sym left, Sym right) =>
			new MemberExpression(loc, new Identifier(loc, left), new Identifier(loc, right));
		internal static MemberExpression simple(Loc loc, Sym a, Sym b, Sym c) =>
			new MemberExpression(loc, new MemberExpression(loc, new Identifier(loc, a), new Identifier(loc, b)), new Identifier(loc, c));
	}

	sealed class ConditionalExpression : Node, Expression {
		internal readonly Expression test;
		internal readonly Expression alternate;
		internal readonly Expression consequent;
		internal ConditionalExpression(Loc loc, Expression test, Expression alternate, Expression consequent) : base(loc) {
			this.test = test;
			this.alternate = alternate;
			this.consequent = consequent;
		}
	}

	abstract class CallOrNewExpression : Node, Expression {
		internal readonly Expression callee;
		internal readonly Arr<Expression> arguments;
		internal CallOrNewExpression(Loc loc, Expression callee, Arr<Expression> arguments) : base(loc) {
			this.callee = callee;
			this.arguments = arguments;
		}
	}

	sealed class CallExpression : CallOrNewExpression {
		internal CallExpression(Loc loc, Expression callee, Arr<Expression> arguments) : base(loc, callee, arguments) {}
	}

	sealed class NewExpression : CallOrNewExpression {
		internal NewExpression(Loc loc, Expression callee, Arr<Expression> arguments) : base(loc, callee, arguments) {}
	}

	sealed class ObjectExpression : Node, Expression {
		internal readonly Arr<Property> properties;
		internal ObjectExpression(Loc loc, Arr<Property> properties) : base(loc) { this.properties = properties; }
	}

	sealed class Property : Node {
		internal readonly Identifier key;
		internal readonly Expression value;
		internal readonly string kind; // "init" | "get" | "set"
		internal Property(Loc loc, Identifier key, Expression value, string kind) : base(loc) {
			this.key = key;
			this.value = value;
			this.kind = kind;
		}
	}

	abstract class Class : Node {
		internal readonly Identifier id;
		internal readonly Op<Expression> superClass;
		internal readonly ClassBody body;
		internal Class(Loc loc, Identifier id, Op<Expression> superClass, ClassBody body) : base(loc) {
			this.id = id;
			this.superClass = superClass;
			this.body = body;
		}
	}

	sealed class ClassDeclaration : Class, Declaration {
		internal ClassDeclaration(Loc loc, Identifier id, Op<Expression> superClass, ClassBody body) : base(loc, id, superClass, body) {}
	}

	sealed class ClassExpression : Class, Expression {
		internal ClassExpression(Loc loc, Identifier id, Op<Expression> superClass, ClassBody body) : base(loc, id, superClass, body) {}
	}

	sealed class ClassBody : Node {
		internal readonly Arr<MethodDefinition> body;
		internal ClassBody(Loc loc, Arr<MethodDefinition> body) : base(loc) {
			this.body = body;
		}
	}

	sealed class MethodDefinition : Node {
		internal readonly Identifier key; // If kind == "constructor", must be "constructor"
		internal readonly FunctionExpression value;
		internal readonly string kind; // "constructor" | "method" | "get" | "set"
		internal readonly bool computed;
		internal readonly bool @static;
		internal MethodDefinition(Loc loc, Identifier key, FunctionExpression value, string kind, bool @static) : base(loc) {
			this.key = key;
			this.value = value;
			this.kind = kind;
			this.computed = false;
			this.@static = @static;
		}
	}

	abstract class ModuleDeclaration : Node {
		internal ModuleDeclaration(Loc loc) : base(loc) {}
	}

	sealed class AssignmentExpression : Node, Expression {
		internal readonly string @operator;
		internal readonly Pattern left;
		internal readonly Expression right;
		internal AssignmentExpression(Loc loc, Pattern left, Expression right) : base(loc) {
			this.@operator = "=";
			this.left = left;
			this.right = right;
		}
	}

	/*sealed class ImportDeclaration : ModuleDeclaration {
		internal readonly Arr<ImportDeclarationSpecifier> specifiers;
		internal readonly Literal source;
		internal ImportDeclaration(Loc loc, Arr<ImportDeclarationSpecifier> specifiers, Literal source) : base(loc) {
			this.specifiers = specifiers;
			this.source = source;
		}
	}

	abstract class ImportDeclarationSpecifier : Node {
		internal readonly Identifier local;
		internal ImportDeclarationSpecifier(Loc loc, Identifier local) : base(loc) { this.local = local; }
	}

	sealed class ImportSpecifier : ImportDeclarationSpecifier {
		internal readonly Identifier imported; //same
		internal ImportSpecifier(Loc loc, Identifier local) : base(loc, local) {
			this.imported = local;
		}
	}

	sealed class ImportDefaultSpecifier : ImportDeclarationSpecifier {
		internal ImportDefaultSpecifier(Loc loc, Identifier local) : base(loc, local) {}
	}

	sealed class ExportDefaultDeclaration : ModuleDeclaration {
		internal readonly DeclarationOrExpression declaration;
		internal ExportDefaultDeclaration(Loc loc, DeclarationOrExpression declaration) : base(loc) {
			this.declaration = declaration;
		}
	}*/
}
