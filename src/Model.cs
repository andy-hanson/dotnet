using System;
using System.Collections.Generic;
using System.Reflection;

using static Utils;

namespace Model {
	//TODO: actual types
	abstract class Ty : IEquatable<Ty> {
		public override bool Equals(object o) {
			var t = o as Ty;
			return t != null && Equals(t);
		}
		public abstract bool Equals(Ty ty);
		public override abstract int GetHashCode();
		public static bool operator ==(Ty a, Ty b) => a.Equals(b);
		public static bool operator !=(Ty a, Ty b) => !a.Equals(b);
	}

	sealed class Module : IEquatable<Module> {
		internal readonly Path path;
		internal readonly bool isMain;
		internal readonly string source;
		internal readonly LineColumnGetter lineColumnGetter;

		internal Module(Path path, bool isMain, string source) {
			this.path = path;
			this.isMain = isMain;
			this.source = source;
			this.lineColumnGetter = new LineColumnGetter(source);
		}

		internal Path fullPath => Compiler.fullPath(path, isMain);

		Arr<Module>? _imports;
		internal Arr<Module> imports {
			get { return _imports.Value; }
			set { assert(!_imports.HasValue); _imports = value; }
		}

		internal bool importsAreResolved => _imports != null;

		Op<Klass> _klass;
		internal Klass klass {
			get { return _klass.force; }
			set { assert(!_klass.has); _klass = Op.Some(value); }
		}

		internal Sym name => klass.name;

		public bool Equals(Module other) => object.ReferenceEquals(this, other);
		public override int GetHashCode() => path.GetHashCode();
	}

	abstract class ClassLike : Ty {
		internal readonly Sym name;
		internal abstract Dict<Sym, Member> membersMap { get; }

		protected ClassLike(Sym name) { this.name = name; }
	}

	sealed class BuiltinClass : ClassLike {
		internal readonly Type dotNetType;

		Dict<Sym, Member> _membersMap;
		internal override Dict<Sym, Member> membersMap => _membersMap;

		static Dictionary<Sym, BuiltinClass> byName = new Dictionary<Sym, BuiltinClass>();

		static BuiltinClass() {
			Builtins.register();
		}

		static Dict<Sym, string> operatorEscapes = Dict.create(
			"+".to("_add"),
			"-".to("_sub"),
			"*".to("_mul"),
			"/".to("_div"),
			"^".to("_pow")
		).mapKeys(Sym.of);
		static Dict<string, Sym> operatorUnescapes = operatorEscapes.reverse();

		//void is OK for builtins, but we shouldn't attempt to create a class for it.
		static ISet<Type> badTypes = new HashSet<Type> { typeof(void), typeof(object), typeof(string), typeof(char), typeof(uint), typeof(int), typeof(bool) };

		internal static readonly BuiltinClass Bool = fromDotNetType(typeof(Builtins.Bool));
		internal static readonly BuiltinClass Int = fromDotNetType(typeof(Builtins.Int));
		internal static readonly BuiltinClass Float = fromDotNetType(typeof(Builtins.Float));
		internal static readonly BuiltinClass Str = fromDotNetType(typeof(Builtins.Str));

		/** Get an already-registered type by name. */
		internal static bool tryGet(Sym name, out BuiltinClass b) => byName.TryGetValue(name, out b);

		/** Safe to call this twice on the same type. */
		internal static BuiltinClass fromDotNetType(Type dotNetType) {
			if (badTypes.Contains(dotNetType)) {
				throw new Exception($"Should not attempt to use {dotNetType} as a builtin");
			}


			var customName = dotNetType.GetCustomAttribute<BuiltinName>(inherit: false);
			var name = customName != null ? customName.name : unescapeName(dotNetType.Name);

			if (byName.TryGetValue(name, out var old)) {
				assert(old.dotNetType == dotNetType);
				return old;
			}

			var klass = new BuiltinClass(name, dotNetType);
			// Important that we put this in the map *before* initializing it, so a type's methods can refer to itself.
			byName[name] = klass;

			//BAD! GetMethods() gets inherited methosd!
			klass._membersMap = dotNetType.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public).mapToDict<MethodInfo, Sym, Member>(m => {
				if (m.GetCustomAttribute<Hid>(inherit: true) != null)
					return null;

				Member m2 = new BuiltinMethod(klass, m);
				return m2.name.to(m2);
			});

			return klass;
		}

		private BuiltinClass(Sym name, Type dotNetType) : base(name) { this.dotNetType = dotNetType; }

		public override bool Equals(Ty ty) => object.ReferenceEquals(this, ty);
		public override int GetHashCode() => name.GetHashCode();

		internal static string escapeName(Sym name) {
			if (operatorEscapes.get(name, out var str))
				return str;

			foreach (var ch in name.str)
				if ('a' <= ch && ch <= 'z' || 'A' <= ch && ch <= 'Z')
					unreachable();

			return name.str;
		}

		internal static Sym unescapeName(string name) {
			if (operatorUnescapes.get(name, out var v))
				return v;
			return Sym.of(name);
		}

		public override string ToString() => $"BuiltinClass({name})";
	}

	sealed class Klass : ClassLike {
		internal readonly Loc loc;

		internal Klass(Loc loc, Sym name) : base(name) {
			this.loc = loc;
		}

		Op<Head> _head;
		internal Head head {
			get { return _head.force; }
			set { assert(!_head.has); _head = Op.Some(value); }
		}

		Dict<Sym, Member>? _membersMap;
		internal override Dict<Sym, Member> membersMap => _membersMap.Value;
		internal void setMembersMap(Dict<Sym, Member> membersMap) { assert(!_membersMap.HasValue); _membersMap = membersMap; }

		//internal IEnumerable<MethodWithBody> methods ...

		internal abstract class Head {
			readonly Loc loc;
			Head(Loc loc) { this.loc = loc; }

			// Static class: May only contain "fun"
			internal class Static : Head {
				internal Static(Loc loc) : base(loc) {}
			}

			// Abstract class: Never instantiated.
			internal class Abstract : Head {
				internal Abstract(Loc loc) : base(loc) {}
			}

			internal class Slots : Head {
				internal readonly Arr<Slot> slots;
				internal Slots(Loc loc, Arr<Slot> slots) : base(loc) {
					this.slots = slots;
				}

				internal sealed class Slot : Member {
					internal readonly Klass klass;
					internal readonly bool mutable;
					internal readonly Ty ty;

					internal Slot(Klass klass, Loc loc, bool mutable, Ty ty, Sym name) : base(loc, name) {
						this.klass = klass;
						this.mutable = mutable;
						this.ty = ty;
					}
				}
			}
		}

		public override bool Equals(Ty ty) => object.ReferenceEquals(this, ty);
		public override int GetHashCode() => name.GetHashCode();
	}

	abstract class Member {
		internal readonly Loc loc;
		internal readonly Sym name;
		internal Member(Loc loc, Sym name) { this.loc = loc; this.name = name; }
	}

	abstract class Method : Member {
		internal readonly ClassLike klass;
		internal readonly bool isStatic;//TODO: just store static methods elsewhere?
		internal readonly Ty returnTy;
		internal readonly Arr<Parameter> parameters;

		internal Method(ClassLike klass, Loc loc, bool isStatic, Ty returnTy, Sym name, Arr<Parameter> parameters) : base(loc, name) {
			this.klass = klass;
			this.isStatic = isStatic;
			this.returnTy = returnTy;
			this.parameters = parameters;
		}

		internal uint arity => parameters.length;

		internal sealed class Parameter {
			internal readonly Loc loc;
			internal readonly Ty ty;
			internal readonly Sym name;

			internal Parameter(Loc loc, Ty ty, Sym name) {
				this.loc = loc;
				this.ty = ty;
				this.name = name;
			}
		}
	}
	sealed class BuiltinMethod : Method {
		internal BuiltinMethod(BuiltinClass klass, MethodInfo m)
			: base(klass, Loc.zero, m.IsStatic, BuiltinClass.fromDotNetType(m.ReturnType), getName(m), mapParams(m)) {}

		static Sym getName(MethodInfo m) {
			var customName = m.GetCustomAttribute<BuiltinName>(inherit: true);
			return customName != null ? customName.name : Sym.of(m.Name);
		}

		static Arr<Method.Parameter> mapParams(MethodInfo m) => m.GetParameters().map(p => {
			assert(!p.IsIn);
			assert(!p.IsLcid);
			assert(!p.IsOut);
			assert(!p.IsOptional);
			assert(!p.IsRetval);
			var ty = BuiltinClass.fromDotNetType(p.ParameterType);
			return new Method.Parameter(Loc.zero, ty, Sym.of(p.Name));
		});
	}

	sealed class MethodWithBody : Method {
		internal MethodWithBody(Klass klass, Loc loc, bool isStatic, Ty returnTy, Sym name, Arr<Parameter> parameters)
			: base(klass, loc, isStatic, returnTy, name, parameters) { }

		Op<Expr> _body;
		internal Expr body {
			get { return _body.force; }
			set { assert(!_body.has); _body = Op.Some(value); }
		}
	}

	abstract class Pattern {
		readonly Loc loc;
		Pattern(Loc loc) { this.loc = loc; }

		internal sealed class Ignore : Pattern {
			internal Ignore(Loc loc) : base(loc) { }
		}
		internal sealed class Single : Pattern {
			internal readonly Ty ty;
			internal readonly Sym name;
			internal Single(Loc loc, Ty ty, Sym name) : base(loc) {
				this.ty = ty;
				this.name = name;
			}
		}
		internal sealed class Destruct : Pattern {
			internal readonly Arr<Pattern> destructuredInto;
			internal Destruct(Loc loc, Arr<Pattern> destructuredInto) : base(loc) {
				this.destructuredInto = destructuredInto;
			}
		}
	}

	abstract class Expr {
		internal readonly Loc loc;
		internal abstract Ty ty { get; }
		Expr(Loc loc) {
			this.loc = loc;
		}

		//TODO: don't think having a base class here helps...
		internal abstract class Access : Expr {
			internal abstract Sym name { get; }
			Access(Loc loc) : base(loc) { }

			internal sealed class Parameter : Access {
				internal readonly MethodWithBody.Parameter param;
				internal Parameter(Loc loc, MethodWithBody.Parameter param) : base(loc) {
					this.param = param;
				}

				internal override Ty ty => param.ty;
				internal override Sym name => param.name;
			}

			internal sealed class Local : Access {
				readonly Pattern.Single local;
				internal Local(Loc loc, Pattern.Single local) : base(loc) {
					this.local = local;
				}

				internal override Ty ty => local.ty;
				internal override Sym name => local.name;
			}
		}

		internal sealed class Let : Expr {
			internal readonly Expr value;
			internal readonly Expr then;

			internal Let(Loc loc, Pattern assigned, Expr value, Expr then) : base(loc) {
				this.value = value;
				this.then = then;
			}

			internal override Ty ty => then.ty;
		}

		internal sealed class Seq : Expr {
			internal readonly Expr action;
			internal readonly Expr then;

			internal Seq(Loc loc, Expr action, Expr then) : base(loc) {
				this.action = action;
				this.then = then;
			}

			internal override Ty ty => GetTy();

			Ty GetTy() {
				return then.ty;
			}
		}

		internal sealed class Literal : Expr {
			internal readonly LiteralValue value;
			internal Literal(Loc loc, LiteralValue value) : base(loc) { this.value = value; }
			internal override Ty ty => value.ty;

			internal abstract class LiteralValue {
				internal abstract Ty ty { get; }
				LiteralValue() {}

				internal sealed class Bool : LiteralValue {
					internal readonly bool value;
					internal Bool(bool value) { this.value = value; }
					internal override Ty ty => BuiltinClass.Bool;
				}

				internal sealed class Int : LiteralValue {
					internal readonly int value;
					internal Int(int value) { this.value = value; }
					internal override Ty ty => BuiltinClass.Int;
				}

				internal sealed class Float : LiteralValue {
					internal readonly double value;
					internal Float(double value) { this.value = value; }
					internal override Ty ty => BuiltinClass.Float;
				}

				internal sealed class Str : LiteralValue {
					internal readonly string value;
					internal Str(string value) { this.value = value; }
					internal override Ty ty => BuiltinClass.Str;
				}
			}
		}

		internal sealed class WhenTest : Expr {
			internal readonly Arr<Case> cases;
			internal readonly Expr elseResult;
			internal readonly Ty _ty; // Common type of all cases and elseResult.
			internal WhenTest(Loc loc, Arr<Case> cases, Expr elseResult, Ty ty) : base(loc) { this.cases = cases; this.elseResult = elseResult; this._ty = ty; }

			internal struct Case {
				internal readonly Loc loc;
				internal readonly Expr test;
				internal readonly Expr result;
				internal Case(Loc loc, Expr test, Expr result) { this.loc = loc; this.test = test; this.result = result; }
			}

			override internal Ty ty => ty;
		}

		internal sealed class StaticMethodCall : Expr {
			internal readonly Method method;
			internal readonly Arr<Expr> args;
			internal StaticMethodCall(Loc loc, Method method, Arr<Expr> args) : base(loc) {
				assert(method.isStatic);
				this.method = method;
				this.args = args;
			}

			internal override Ty ty => method.returnTy;
		}

		internal sealed class MethodCall : Expr {
			internal readonly Expr target;
			internal readonly Method method;
			internal readonly Arr<Expr> args;
			internal MethodCall(Loc loc, Expr target, Method method, Arr<Expr> args) : base(loc) {
				assert(!method.isStatic);
				this.target = target;
				this.method = method;
				this.args = args;
			}

			internal override Ty ty => method.returnTy;
		}

		//Share w/ GetSlot?
		//Note: this contains a pointer to the current class for convenience.
		//Note: this should only happen in a non-static method. Otherwise we have Expr.Error
		internal sealed class GetMySlot : Expr {
			internal readonly Klass klass; //This is just here for convenience. It *must* be the class of the method this expression is in.
			internal readonly Klass.Head.Slots.Slot slot;
			internal GetMySlot(Loc loc, Klass klass, Klass.Head.Slots.Slot slot) : base(loc) {
				this.klass = klass;
				this.slot = slot;
			}
			internal override Ty ty => slot.ty;
		}

		internal sealed class GetSlot : Expr {
			internal readonly Klass.Head.Slots.Slot slot;
			internal readonly Expr target;
			internal GetSlot(Loc loc, Expr target, Klass.Head.Slots.Slot slot) : base(loc) {
				this.target = target;
				this.slot = slot;
			}
			internal override Ty ty => slot.ty;
		}

		/*internal sealed class GetMethod : Get {
			internal readonly NzMethod method;
			internal readonly Expr target;
			internal GetMethod(Loc loc, Expr target, MethodWithBody method) : base(loc, target) {
				assert(!method.isStatic);
				this.target = target;
				this.method = method;
			}
			internal override Ty ty => TODO(); //Function type for the method
		}*/

		/*internal sealed class GetStaticMethod : Expr {
			internal readonly NzMethod method;
			internal GetStaticMethod(Loc loc, MethodWithBody method) : base(loc) {
				assert(method.isStatic);
				this.method = method;
			}
			internal override Ty ty => TODO();
		}*/
	}

	//LiteralValue, Literal
	//StaticMethodCall, MethodCall

	/*sealed class GetSlot : Expr {
		readonly Expr target;
		readonly Slot slot;
		internal GetSlot(Loc loc, Expr target, Slot slot) : base(loc) {
			assert(!slot.mutable);
			this.target = target;
			this.slot = slot;
		}
	}*/
}
