using System;
using System.Collections.Generic;
using System.Reflection;

using static Utils;

namespace Model {
	abstract class M {
		public override bool Equals(object o) => throw new NotImplementedException();
		public override int GetHashCode() => throw new NotImplementedException();
	}

	// A module's identity is its path.
	sealed class Module : M, ToData<Module>, Identifiable<Path> {
		/**
		For "a.nz" this is "a".
		For "a/index.nz" this is still "a".
		The difference is indicated by `isIndex`.
		Use `fullPath` to get the full path.
		*/
		internal readonly Path logicalPath;
		internal readonly bool isIndex;
		internal readonly DocumentInfo document;
		// Technically these form a tree and thus aren't up-pointers, but don't want to serialize imports when serializing a module.
		[UpPointer] internal readonly Arr<Module> imports;
		Op<Klass> _klass;
		internal Klass klass {
			get => _klass.force;
			set {
				assert(!_klass.has);
				_klass = Op.Some(value);
			}
		}
		//TODO: does this belong here? Or somewhere else?
		[NotData] internal readonly LineColumnGetter lineColumnGetter;

		internal Module(Path logicalPath, bool isIndex, DocumentInfo document, Arr<Module> imports) {
			this.logicalPath = logicalPath;
			this.isIndex = isIndex;
			this.document = document;
			this.imports = imports;
			this.lineColumnGetter = new LineColumnGetter(document.text);
		}

		internal Path fullPath() => ModuleResolver.fullPath(logicalPath, isIndex);
		internal Sym name => klass.name;

		public bool deepEqual(Module m) =>
			logicalPath.Equals(m.logicalPath) &&
			isIndex == m.isIndex &&
			document.Equals(m.document) &&
			imports.deepEqual(m.imports, IdentifiableU.equalsId<Module, Path>) &&
			klass.Equals(m.klass);

		public Dat toDat() => Dat.of(this,
			nameof(logicalPath), logicalPath,
			nameof(isIndex), Dat.boolean(isIndex),
			nameof(document), document,
			nameof(imports), Dat.arr(imports),
			nameof(klass), klass);

		public Path getId() => logicalPath;
	}

	// This is always a ClassLike currently. Eventually we'll add instantiated generic classes too.
	abstract class Ty : M, ToData<Ty>, Identifiable<ClassLike.Id> {
		public abstract bool deepEqual(Ty ty);
		public override abstract int GetHashCode();
		public abstract Dat toDat();

		public bool fastEquals(Ty other) => object.ReferenceEquals(this, other);

		public abstract ClassLike.Id getId();
	}

	abstract class ClassLike : Ty, Identifiable<ClassLike.Id> {
		// For a builtin type, identified by the builtin name.
		// For a
		internal struct Id : ToData<Id> {
			// If this is a builtin, this will be missing.
			private string id;
			Id(string id) { this.id = id; }
			internal static Id ofPath(Path path) => new Id(path.ToString());
			internal static Id ofBuiltin(Sym name) => new Id(name.str);
			public bool deepEqual(Id i) => id.Equals(i.id);
			public Dat toDat() => Dat.str(id);

			/*internal readonly Op<Path> module;
			internal readonly Sym name;
			internal Id(Op<Path> module, Sym name) { this.module = module; this.name = name; }
			public bool Equals(Id c) => module.eq(c.module) && name.Equals(c.name);
			public Dat toDat() =>
				module.get(out var mod)
					? Dat.str(mod.ToString())
					: Dat.str(name.str);//Dat.of(this, nameof(module), Dat.op(module), nameof(name), name);
			*/
		}

		internal readonly Sym name;
		internal abstract Dict<Sym, Member> membersMap { get; }

		protected ClassLike(Sym name) { this.name = name; }
	}

	sealed class BuiltinClass : ClassLike, ToData<BuiltinClass> {
		internal readonly Type dotNetType;

		Dict<Sym, Member> _membersMap;
		internal override Dict<Sym, Member> membersMap => _membersMap;

		static Dictionary<Sym, BuiltinClass> byName = new Dictionary<Sym, BuiltinClass>();

		static BuiltinClass() {
			Builtins.register();
		}

		static Dict<Sym, string> operatorEscapes = Dict.of(
			("+", "_add"),
			("-", "_sub"),
			("*", "_mul"),
			("/", "_div"),
			("^", "_pow")
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
					return Op<(Sym, Member)>.None;

				Member m2 = new Method.BuiltinMethod(klass, m);
				return Op.Some((m2.name, m2));
			});

			return klass;
		}

		private BuiltinClass(Sym name, Type dotNetType) : base(name) { this.dotNetType = dotNetType; }

		public override ClassLike.Id getId() => ClassLike.Id.ofBuiltin(name);

		public override bool deepEqual(Ty t) => throw new NotImplementedException();
		public bool deepEqual(BuiltinClass b) => throw new NotImplementedException(); // This should never happen.
		public override int GetHashCode() => name.GetHashCode();
		public override Dat toDat() => Dat.of(this, "name", Dat.str(name.str));

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

	sealed class Klass : ClassLike, ToData<Klass>, IEquatable<Klass> {
		[UpPointer] internal readonly Module module;
		internal readonly Loc loc;

		internal Klass(Module module, Loc loc, Sym name) : base(name) {
			this.module = module;
			this.loc = loc;
		}

		public override ClassLike.Id getId() => ClassLike.Id.ofPath(module.logicalPath);

		Op<Head> _head;
		internal Head head {
			get => _head.force;
			set { assert(!_head.has); _head = Op.Some(value); }
		}

		Op<Arr<Method.MethodWithBody>> _methods;
		internal Arr<Method.MethodWithBody> methods {
			get => _methods.force;
			set { assert(!_methods.has); _methods = Op.Some(value); }
		}

		Op<Dict<Sym, Member>> _membersMap;
		internal override Dict<Sym, Member> membersMap => _membersMap.force;
		internal void setMembersMap(Dict<Sym, Member> membersMap) { assert(!_membersMap.has); _membersMap = Op.Some(membersMap); }

		public bool Equals(Klass k) => object.ReferenceEquals(this, k);
		public override int GetHashCode() => name.GetHashCode();
		public override bool deepEqual(Ty ty) => ty is Klass k && deepEqual(k);
		public bool deepEqual(Klass k) => throw TODO();
		public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(head), head, nameof(membersMap), Dat.dict(membersMap));

		internal abstract class Head : M, ToData<Head> {
			readonly Loc loc;
			Head(Loc loc) { this.loc = loc; }

			public abstract bool deepEqual(Head head);
			public abstract Dat toDat();

			// Static class: May only contain "fun"
			internal class Static : Head, ToData<Static> {
				internal Static(Loc loc) : base(loc) {}
				public override bool deepEqual(Head h) => h is Static s && Equals(s);
				public bool deepEqual(Static s) => loc.Equals(s.loc);
				public override Dat toDat() => Dat.of(this, nameof(loc), loc);
			}

			// Abstract class: Never instantiated.
			internal class Abstract : Head, ToData<Abstract> {
				internal Abstract(Loc loc) : base(loc) {}
				public override bool deepEqual(Head h) => h is Abstract a && Equals(a);
				public bool deepEqual(Abstract a) => loc.Equals(a.loc);
				public override Dat toDat() => Dat.of(this, nameof(loc), loc);
			}

			internal class Slots : Head, ToData<Slots> {
				internal readonly Arr<Slot> slots;
				internal Slots(Loc loc, Arr<Slot> slots) : base(loc) {
					this.slots = slots;
				}

				public override bool deepEqual(Head h) => h is Slots s && Equals(s);
				public bool deepEqual(Slots s) => slots.deepEqual(s.slots);
				public override Dat toDat() => Dat.of(this, nameof(slots), Dat.arr(slots));

				internal sealed class Slot : Member, ToData<Slot>, Identifiable<Slot.Id> {
					internal struct Id : ToData<Id> {
						internal readonly ClassLike.Id klass;
						internal readonly Sym name;
						internal Id(ClassLike.Id klass, Sym name) { this.klass = klass; this.name = name; }
						public bool deepEqual(Id i) => klass.Equals(i) && name.Equals(i.name);
						public Dat toDat() => Dat.of(this, nameof(klass), klass, nameof(name), name);
					}

					[ParentPointer] internal readonly ClassLike klass;
					internal readonly bool mutable;
					internal readonly Ty ty;

					internal Slot(ClassLike klass, Loc loc, bool mutable, Ty ty, Sym name) : base(loc, name) {
						this.klass = klass;
						this.mutable = mutable;
						this.ty = ty;
					}

					public override bool deepEqual(Member m) => m is Slot s && Equals(s);
					public bool deepEqual(Slot s) => loc.Equals(s.loc) && name.Equals(s.name) && mutable == s.mutable && ty.Equals(s.ty);
					public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(name), name, nameof(mutable), Dat.boolean(mutable), nameof(ty), ty);
					public Id getId() => new Id(klass.getId(), name);
				}
			}
		}
	}

	abstract class Member : M, ToData<Member> {
		internal readonly Loc loc;
		internal readonly Sym name;
		internal Member(Loc loc, Sym name) { this.loc = loc; this.name = name; }
		public abstract bool deepEqual(Member m);
		public abstract Dat toDat();
	}

	abstract class Method : Member, ToData<Method>, Identifiable<Method.Id> {
		internal struct Id : ToData<Id> {
			internal readonly ClassLike.Id klass;
			internal readonly Sym name;
			internal Id(ClassLike.Id klass, Sym name) { this.klass = klass; this.name = name; }
			public bool deepEqual(Id m) => klass.Equals(m.klass) && name.Equals(m.name);
			public Dat toDat() => Dat.of(this, nameof(klass), klass, nameof(name), name);
		}

		[ParentPointer] internal readonly ClassLike klass;
		internal readonly bool isStatic;//TODO: just store static methods elsewhere?
		[UpPointer] internal readonly Ty returnTy;
		internal readonly Arr<Parameter> parameters;

		public abstract bool deepEqual(Method m);
		public Id getId() => new Id(klass.getId(), name);

		private Method(ClassLike klass, Loc loc, bool isStatic, Ty returnTy, Sym name, Arr<Parameter> parameters) : base(loc, name) {
			this.klass = klass;
			this.isStatic = isStatic;
			this.returnTy = returnTy;
			this.parameters = parameters;
		}

		internal uint arity => parameters.length;

		internal sealed class Parameter : M, ToData<Parameter> {
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

			public bool deepEqual(Parameter p) => loc.Equals(p.loc) && ty.Equals(p.ty) && name.Equals(p.name) && index == p.index;
			public Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(ty), ty, nameof(name), name, nameof(index), Dat.num(index));
		}

		internal sealed class BuiltinMethod : Method, ToData<BuiltinMethod> {
			internal BuiltinMethod(BuiltinClass klass, MethodInfo m)
				: base(klass, Loc.zero, m.IsStatic, BuiltinClass.fromDotNetType(m.ReturnType), getName(m), mapParams(m)) {}

			public override bool deepEqual(Member m) => m is BuiltinMethod b && Equals(b);
			public override bool deepEqual(Method m) => m is BuiltinMethod b && Equals(b);
			public bool deepEqual(BuiltinMethod m) =>
				name.Equals(m.name) && isStatic == m.isStatic && returnTy.Equals(m.returnTy) && parameters.deepEqual(m.parameters);
			public override Dat toDat() => Dat.of(this, "name", name, nameof(isStatic), Dat.boolean(isStatic), nameof(returnTy), returnTy.getId(), nameof(parameters), Dat.arr(parameters));

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

		internal sealed class MethodWithBody : Method, ToData<MethodWithBody> {
			internal MethodWithBody(Klass klass, Loc loc, bool isStatic, Ty returnTy, Sym name, Arr<Parameter> parameters)
				: base(klass, loc, isStatic, returnTy, name, parameters) { }

			public override bool deepEqual(Member m) => m is MethodWithBody mb && Equals(mb);
			public override bool deepEqual(Method m) => m is MethodWithBody mb && Equals(mb);
			public bool deepEqual(MethodWithBody m) => throw TODO(); // TODO: should methods with different (reference identity) parent be not equal?
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(isStatic), Dat.boolean(isStatic), nameof(returnTy), returnTy.getId(), nameof(name), name, nameof(parameters), Dat.arr(parameters));

			Op<Expr> _body;
			internal Expr body {
				get => _body.force;
				set { assert(!_body.has); _body = Op.Some(value); }
			}
		}
	}

	abstract class Pattern : M, ToData<Pattern> {
		internal readonly Loc loc;
		Pattern(Loc loc) { this.loc = loc; }

		public abstract bool deepEqual(Pattern p);
		public abstract Dat toDat();

		internal sealed class Ignore : Pattern, ToData<Ignore> {
			internal Ignore(Loc loc) : base(loc) { }
			public override bool deepEqual(Pattern p) => p is Ignore i && Equals(i);
			public bool deepEqual(Ignore i) => loc.Equals(i.loc);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc);
		}
		internal sealed class Single : Pattern, ToData<Single>, Identifiable<Sym> {
			internal readonly Ty ty;
			internal readonly Sym name;
			internal Single(Loc loc, Ty ty, Sym name) : base(loc) {
				this.ty = ty;
				this.name = name;
			}

			public override bool deepEqual(Pattern p) => p is Single s && Equals(s);
			public bool deepEqual(Single s) => loc.Equals(s.loc) && ty.Equals(s.ty) && name.Equals(s.name);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(ty), ty, nameof(name), name);
			public Sym getId() => name;
		}
		internal sealed class Destruct : Pattern, ToData<Destruct> {
			internal readonly Arr<Pattern> destructuredInto;
			internal Destruct(Loc loc, Arr<Pattern> destructuredInto) : base(loc) {
				this.destructuredInto = destructuredInto;
			}

			public override bool deepEqual(Pattern p) => p is Destruct d && Equals(d);
			public bool deepEqual(Destruct d) => loc.Equals(d.loc) && destructuredInto.deepEqual(d.destructuredInto);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(destructuredInto), Dat.arr(destructuredInto));
		}
	}
}
