using System;

using static Utils;

// https://github.com/estree/estree
namespace Estree {
	interface INode {
		Loc loc { get; }
	}

	abstract class Node : INode {
		internal readonly Loc loc;
		Loc INode.loc => loc;
		public sealed override bool Equals(object o) => throw new NotSupportedException();
		public sealed override int GetHashCode() => throw new NotSupportedException();
		protected Node(Loc loc) { this.loc = loc; }
	}

	interface BlockStatementOrExpression : INode {}
	interface ExpressionOrSuper : INode {}
	interface Expression : INode, ExpressionOrSuper, BlockStatementOrExpression, DeclarationOrExpression {}
	interface Pattern : INode {}

	sealed class Identifier : Node, Expression, Pattern {
		internal readonly string name;
		internal Identifier(Loc loc, string name) : base(loc) {
			assert(isSafeName(name));
			this.name = name;
		}

		static bool isSafeName(string s) {
			foreach (var ch in s) {
				if (ch != '_' && !CharUtils.isDigit(ch) && !CharUtils.isLetter(ch))
					return false;
			}
			return true;
		}
	}

	sealed class Literal : Node, Expression {
		internal readonly LiteralValue value;
		internal Literal(Loc loc, LiteralValue value) : base(loc) { this.value = value; }

		internal static Literal str(Loc loc, string s) =>
			new Literal(loc, LiteralValue.String.of(s));
	}

	sealed class Program : Node {
		internal readonly Arr<Statement> body;
		internal Program(Loc loc, Arr<Statement> body) : base(loc) { this.body = body; }
	}

	abstract class Function : Node {
		internal readonly bool @async;
		internal readonly Arr<Pattern> @params;
		protected Function(Loc loc, bool @async, Arr<Pattern> @params) : base(loc) {
			this.@async = @async;
			this.@params = @params;
		}
	}

	abstract class FunctionDeclarationOrExpression : Function {
		internal readonly BlockStatement body;
		protected FunctionDeclarationOrExpression(Loc loc, bool @async, Arr<Pattern> @params, BlockStatement body) : base(loc, @async, @params) {
			this.body = body;
		}
	}

	sealed class FunctionExpression : FunctionDeclarationOrExpression, Expression {
		internal FunctionExpression(Loc loc, bool @async, Arr<Pattern> @params, BlockStatement body) : base(loc, @async, @params, body) {}
	}

	sealed class FunctionDeclaration : FunctionDeclarationOrExpression, Declaration {
		internal readonly Identifier id;
		internal FunctionDeclaration(Loc loc, bool @async, Identifier id, Arr<Pattern> @params, BlockStatement body) : base(loc, @async, @params, body) {
			this.id = id;
		}
	}

	sealed class ArrowFunctionExpression : Function, Expression {
		internal readonly BlockStatementOrExpression body;
		internal ArrowFunctionExpression(Loc loc, bool @async, Arr<Pattern> @params, BlockStatementOrExpression body) : base(loc, @async, @params) {
			this.body = body;
		}
	}

	interface Statement {}

	sealed class ThrowStatement : Node, Statement {
		internal readonly Expression argument;
		internal ThrowStatement(Loc loc, Expression argument) : base(loc) {
			this.argument = argument;
		}
	}

	sealed class TryStatement : Node, Statement{
		internal readonly BlockStatement block;
		internal readonly Op<CatchClause> handler;
		internal readonly Op<BlockStatement> finalizer;
		internal TryStatement(Loc loc, BlockStatement block, Op<CatchClause> handler, Op<BlockStatement> finalizer) : base(loc) {
			assert(handler.has || finalizer.has);
			this.block = block;
			this.handler = handler;
			this.finalizer = finalizer;
		}
	}

	sealed class CatchClause : Node {
		internal readonly Pattern param;
		internal readonly BlockStatement body;
		internal CatchClause(Loc loc, Pattern param, BlockStatement body) : base(loc) {
			this.param = param;
			this.body = body;
		}
	}

	sealed class ExpressionStatement : Node, Statement {
		internal readonly Expression expression;
		ExpressionStatement(Loc loc, Expression expression) : base(loc) {
			this.expression = expression;
		}

		internal static ExpressionStatement of(Expression expression) =>
			new ExpressionStatement(expression.loc, expression);
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
		ReturnStatement(Loc loc, Expression argument) : base(loc) {
			this.argument = argument;
		}
		internal static ReturnStatement of(Expression argument) => new ReturnStatement(argument.loc, argument);
	}

	sealed class IfStatement : Node, Statement {
		internal readonly Expression test;
		internal readonly Statement consequent;
		internal readonly Op<Statement> alternate;
		IfStatement(Loc loc, Expression test, Statement consequent, Op<Statement> alternate) : base(loc) {
			this.test = test;
			if (this.consequent is IfStatement i && !i.alternate.has) {
				assert(i.consequent is BlockStatement);
			}
			this.consequent = consequent;
			this.alternate = alternate;
		}
		internal IfStatement(Loc loc, Expression test, Statement consequent, Statement alternate) : this(loc, test, consequent, Op.Some(alternate)) {}
		internal IfStatement(Loc loc, Expression test, Statement consequent) : this(loc, test, consequent, Op<Statement>.None) {}
	}

	interface DeclarationOrExpression {}

	interface Declaration : Statement, DeclarationOrExpression {}

	sealed class VariableDeclaration : Node, Declaration {
		internal readonly Arr<VariableDeclarator> declarations;
		internal Kind kind;
		internal VariableDeclaration(Loc loc, Arr<VariableDeclarator> declarations, Kind kind) : base(loc) {
			this.declarations = declarations;
			this.kind = kind;
		}

		internal static VariableDeclaration simple(Loc loc, Identifier id, Expression init) =>
			new VariableDeclaration(loc, Arr.of(new VariableDeclarator(loc, id, Op.Some(init))), Kind.Const);
		internal static VariableDeclaration simple(Loc loc, string id, Expression init) =>
			simple(loc, new Identifier(loc, id), init);

		internal enum Kind { Var, Let, Const }

		internal string kindStr() {
			switch (kind) {
				case VariableDeclaration.Kind.Var: return "var";
				case VariableDeclaration.Kind.Let: return "let";
				case VariableDeclaration.Kind.Const: return "const";
				default: throw unreachable();
			}
		}
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
		internal readonly ExpressionOrSuper @object;
		internal readonly Expression property;
		internal readonly bool computed;
		MemberExpression(Loc loc, ExpressionOrSuper @object, Expression property, bool computed) : base(loc) {
			this.@object = @object;
			this.property = property;
			this.computed = computed;
		}

		internal static MemberExpression notComputed(Loc loc, Expression lhs, Identifier property) =>
			new MemberExpression(loc, lhs, property, computed: false);

		internal static MemberExpression simple(Loc loc, Expression lhs, string name) => notComputed(loc, lhs, new Identifier(loc, name));

		internal static MemberExpression simple(Loc loc, string left, string name) =>
			simple(loc, new Identifier(loc, left), name);

		internal static MemberExpression simple(Loc loc, string a, string b, string c) =>
			simple(loc, simple(loc, a, b), c);

		internal static MemberExpression ofThis(Loc loc, string name) =>
			simple(loc, new ThisExpression(loc), name);
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
		internal readonly ExpressionOrSuper callee;
		internal readonly Arr<Expression> arguments;
		protected CallOrNewExpression(Loc loc, ExpressionOrSuper callee, Arr<Expression> arguments) : base(loc) {
			this.callee = callee;
			this.arguments = arguments;
		}
	}

	sealed class CallExpression : CallOrNewExpression {
		internal CallExpression(Loc loc, ExpressionOrSuper callee, Arr<Expression> arguments) : base(loc, callee, arguments) {}
		internal static CallExpression of(Loc loc, ExpressionOrSuper callee, params Expression[] arguments) =>
			new CallExpression(loc, callee, new Arr<Expression>(arguments));
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
		internal readonly Kind kind;
		internal Property(Loc loc, Identifier key, Expression value, Kind kind) : base(loc) {
			this.key = key;
			this.value = value;
			this.kind = kind;
		}

		internal enum Kind { Init, Get, Set }
	}

	abstract class Class : Node {
		internal readonly Identifier id;
		internal readonly Op<Expression> superClass;
		internal readonly ClassBody body;
		protected Class(Loc loc, Identifier id, Op<Expression> superClass, ClassBody body) : base(loc) {
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
		internal readonly Expression key; // If kind == "constructor", must be "constructor"
		internal readonly FunctionExpression value;
		internal readonly Kind kind;
		internal readonly bool computed;
		internal readonly bool @static;
		MethodDefinition(Loc loc, Expression key, FunctionExpression value, Kind kind, bool computed, bool @static) : base(loc) {
			this.key = key;
			this.value = value;
			this.kind = kind;
			this.computed = computed;
			this.@static = @static;
		}

		internal static MethodDefinition method(Loc loc, bool @async, string name, Arr<Pattern> @params, BlockStatement body, bool @static) =>
			new MethodDefinition(loc, new Identifier(loc, name), new FunctionExpression(loc, @async, @params, body), Kind.Method, computed: false, @static: @static);

		internal static MethodDefinition constructor(Loc loc, Arr<Pattern> @params, Arr<Statement> body) {
			var fn = new FunctionExpression(loc, /*async*/ false, @params, new BlockStatement(loc, body));
			return new MethodDefinition(loc, new Identifier(loc, nameof(constructor)), fn, Kind.Constructor, computed: false, @static: false);
		}

		internal enum Kind { Constructor, Method, Get, Set }
	}

	abstract class ModuleDeclaration : Node {
		protected ModuleDeclaration(Loc loc) : base(loc) {}
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

	sealed class BinaryExpression : Node, Expression {
		internal readonly string @operator;
		internal readonly Expression left;
		internal readonly Expression right;
		internal BinaryExpression(Loc loc, string @operator, Expression left, Expression right) : base(loc) {
			this.@operator = @operator;
			this.left = left;
			this.right = right;
		}
	}

	sealed class UnaryExpression : Node, Expression {
		internal readonly string @operator;
		internal readonly bool prefix;
		internal readonly Expression argument;
		UnaryExpression(Loc loc, string @operator, bool prefix, Expression argument) : base(loc) {
			this.@operator = @operator;
			this.prefix = prefix;
			this.argument = argument;
		}

		internal static UnaryExpression not(Loc loc, Expression argument) =>
			new UnaryExpression(loc, "!", prefix: true, argument: argument);
	}

	sealed class Super : Node, ExpressionOrSuper {
		internal Super(Loc loc) : base(loc) {}
	}

	sealed class AwaitExpression : Node, Expression {
		internal readonly Expression argument;
		internal AwaitExpression(Loc loc, Expression argument) : base(loc) {
			this.argument = argument;
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
