using System;
using System.Collections.Generic;
using System.Reflection;

using static Utils;

namespace Model {
	sealed class Module {
		/**
		For "a.nz" this is "a".
		For "a/main.nz" this is still "a".
		The difference is indicated by `isMain`.
		Use `fullPath` to get the full path.
		*/
		internal readonly Path logicalPath;
		internal readonly bool isMain;
		internal readonly LineColumnGetter lineColumnGetter; //TODO: does this belong here? Or somewhere else?
		internal readonly DocumentInfo document;
		internal readonly Arr<Module> imports;
		internal readonly Klass klass;

		internal Module(Path logicalPath, bool isMain, DocumentInfo document, Arr<Module> imports, Klass klass) {
			this.logicalPath = logicalPath;
			this.isMain = isMain;
			this.lineColumnGetter = new LineColumnGetter(document.text);
			this.document = document;
			this.imports = imports;
			this.klass = klass;
		}

		//internal Path fullPath => ModuleResolver.fullPath(logicalPath, isMain);
		internal Sym name => klass.name;

		//public bool Equals(Module other) => object.ReferenceEquals(this, other);
		//public override int GetHashCode() => logicalPath.GetHashCode();
	}

	// This is always a ClassLike currently. Eventually we'll add instantiated generic classes too.
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

		internal static readonly BuiltinClass Void = fromDotNetType(typeof(Builtins.Void));
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
					return Op<KeyValuePair<Sym, Member>>.None;

				Member m2 = new Method.BuiltinMethod(klass, m);
				return Op.Some(m2.name.to(m2));
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

		Op<Dict<Sym, Member>> _membersMap;
		internal override Dict<Sym, Member> membersMap => _membersMap.force;
		internal void setMembersMap(Dict<Sym, Member> membersMap) { assert(!_membersMap.has); _membersMap = Op.Some(membersMap); }

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

		private Method(ClassLike klass, Loc loc, bool isStatic, Ty returnTy, Sym name, Arr<Parameter> parameters) : base(loc, name) {
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
			internal readonly uint index;

			internal Parameter(Loc loc, Ty ty, Sym name, uint index) {
				this.loc = loc;
				this.ty = ty;
				this.name = name;
				this.index = index;
			}
		}

		internal sealed class BuiltinMethod : Method {
			internal BuiltinMethod(BuiltinClass klass, MethodInfo m)
				: base(klass, Loc.zero, m.IsStatic, BuiltinClass.fromDotNetType(m.ReturnType), getName(m), mapParams(m)) {}

			static Sym getName(MethodInfo m) {
				var customName = m.GetCustomAttribute<BuiltinName>(inherit: true);
				return customName != null ? customName.name : Sym.of(m.Name);
			}

			static Arr<Method.Parameter> mapParams(MethodInfo m) => m.GetParameters().map((p, index) => {
				assert(!p.IsIn);
				assert(!p.IsLcid);
				assert(!p.IsOut);
				assert(!p.IsOptional);
				assert(!p.IsRetval);
				var ty = BuiltinClass.fromDotNetType(p.ParameterType);
				return new Method.Parameter(Loc.zero, ty, Sym.of(p.Name), index);
			});
		}

		internal sealed class MethodWithBody : Method {
			internal MethodWithBody(Klass klass, Loc loc, bool isStatic, Ty returnTy, Sym name, Arr<Parameter> parameters)
				: base(klass, loc, isStatic, returnTy, name, parameters) { }

			Op<Expr> _body;
			internal Expr body {
				get { return _body.force; }
				set { assert(!_body.has); _body = Op.Some(value); }
			}
		}
	}

	abstract class Pattern {
		internal readonly Loc loc;
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
}
