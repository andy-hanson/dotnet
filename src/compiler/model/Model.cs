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
		Late<Klass> _klass;
		internal Klass klass { get => _klass.get; set => _klass.set(value); }
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
			logicalPath.deepEqual(m.logicalPath) &&
			isIndex == m.isIndex &&
			document.deepEqual(m.document) &&
			imports.eachEqualId<Module, Path>(m.imports) &&
			klass.deepEqual(m.klass);

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
		internal abstract bool isAbstract { get; } //kill
		internal abstract Sym name { get; }
		internal abstract Arr<Super> supers { get; }

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
			public bool deepEqual(Id i) => id == i.id;
			public Dat toDat() => Dat.str(id);
		}

		readonly Sym _name;
		internal override Sym name => _name;
		internal abstract Dict<Sym, Member> membersMap { get; }

		protected ClassLike(Sym name) { _name = name; }
	}

	sealed class BuiltinClass : ClassLike, ToData<BuiltinClass> {
		internal readonly Type dotNetType;

		internal override Arr<Super> supers {
			get {
				// TODO: handle builtins with supertypes
				// (TODO: Super has location information, may have to abstract over that)
				if (dotNetType.BaseType != null) throw TODO();
				if (dotNetType.GetInterfaces().Length != 0) throw TODO();
				return Arr.empty<Super>();
			}
		}
		internal override bool isAbstract => dotNetType.IsAbstract;

		Dict<Sym, Member> _membersMap;
		internal override Dict<Sym, Member> membersMap => _membersMap;

		static readonly Dictionary<Sym, BuiltinClass> byName = new Dictionary<Sym, BuiltinClass>();

		static BuiltinClass() {
			Builtins.register();
		}

		static Dict<Sym, string> operatorEscapes = Dict.of(
			("+", "_add"),
			("-", "_sub"),
			("*", "_mul"),
			("/", "_div"),
			("^", "_pow")).mapKeys(Sym.of);
		static Dict<string, Sym> operatorUnescapes = operatorEscapes.reverse();

		//void is OK for builtins, but we shouldn't attempt to create a class for it.
		static readonly ISet<Type> badTypes = new HashSet<Type> { typeof(void), typeof(object), typeof(string), typeof(char), typeof(uint), typeof(int), typeof(bool) };

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
		public override Dat toDat() => Dat.of(this, nameof(name), name);

		internal static string escapeName(Sym name) {
			if (operatorEscapes.get(name, out var str))
				return str;

			foreach (var ch in name.str)
				if (CharUtils.isLetter(ch))
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

		internal override bool isAbstract => head is Head.Abstract;

		internal Klass(Module module, Loc loc, Sym name) : base(name) {
			this.module = module;
			this.loc = loc;
		}

		public override ClassLike.Id getId() => ClassLike.Id.ofPath(module.logicalPath);

		Late<Head> _head;
		internal Head head { get => _head.get; set => _head.set(value); }

		internal Late<Arr<Super>> _supers;
		internal override Arr<Super> supers => _supers.get;
		internal void setSupers(Arr<Super> value) => _supers.set(value);

		Late<Arr<Method>> _methods;
		internal Arr<Method> methods { get => _methods.get; set => _methods.set(value); }

		Late<Dict<Sym, Member>> _membersMap;
		internal override Dict<Sym, Member> membersMap => _membersMap.get;
		internal void setMembersMap(Dict<Sym, Member> value) => _membersMap.set(value);

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
				public override bool deepEqual(Head h) => h is Static s && deepEqual(s);
				public bool deepEqual(Static s) => loc.deepEqual(s.loc);
				public override Dat toDat() => Dat.of(this, nameof(loc), loc);
			}

			// Abstract class: Never instantiated.
			internal class Abstract : Head, ToData<Abstract> {
				internal readonly Arr<Method.AbstractMethod> abstractMethods;

				internal Abstract(Loc loc, Arr<Method.AbstractMethod> abstractMethods) : base(loc) { this.abstractMethods = abstractMethods; }
				public override bool deepEqual(Head h) => h is Abstract a && deepEqual(a);
				public bool deepEqual(Abstract a) => loc.deepEqual(a.loc);
				public override Dat toDat() => Dat.of(this, nameof(loc), loc);
			}

			internal class Slots : Head, ToData<Slots> {
				[ParentPointer] internal readonly Klass klass;
				Late<Arr<Slot>> _slots;
				internal Arr<Slot> slots { get => _slots.get; set => _slots.set(value); }

				internal Slots(Loc loc, Klass klass) : base(loc) {
					this.klass = klass;
				}

				public override bool deepEqual(Head h) => h is Slots s && deepEqual(s);
				public bool deepEqual(Slots s) => slots.deepEqual(s.slots);
				public override Dat toDat() => Dat.of(this, nameof(slots), Dat.arr(slots));

				internal sealed class Slot : Member, ToData<Slot>, Identifiable<Slot.Id>, IEquatable<Slot> {
					internal struct Id : ToData<Id> {
						internal readonly ClassLike.Id klass;
						internal readonly Sym name;
						internal Id(ClassLike.Id klass, Sym name) { this.klass = klass; this.name = name; }
						public bool deepEqual(Id i) => klass.deepEqual(i.klass) && name.deepEqual(i.name);
						public Dat toDat() => Dat.of(this, nameof(klass), klass, nameof(name), name);
					}

					[ParentPointer] internal readonly Slots slots;
					internal readonly bool mutable;
					internal readonly Ty ty;

					internal Slot(Slots slots, Loc loc, bool mutable, Ty ty, Sym name) : base(loc, name) {
						this.slots = slots;
						this.mutable = mutable;
						this.ty = ty;
					}

					bool IEquatable<Slot>.Equals(Slot s) => deepEqual(s);
					public override int GetHashCode() => name.GetHashCode();
					public override bool deepEqual(Member m) => m is Slot s && deepEqual(s);
					public bool deepEqual(Slot s) => loc.deepEqual(s.loc) && name.deepEqual(s.name) && mutable == s.mutable && ty.equalsId<Ty, ClassLike.Id>(s.ty);
					public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(name), name, nameof(mutable), Dat.boolean(mutable), nameof(ty), ty);
					public Id getId() => new Id(slots.klass.getId(), name);
				}
			}
		}
	}

	sealed class Super : M, ToData<Super>, Identifiable<Super.Id> {
		internal readonly Loc loc;
		[ParentPointer] internal readonly Klass klass;
		[UpPointer] internal readonly Ty superClass;
		//internal readonly Arr<Impl> impls;
		Late<Arr<Impl>> _impls;
		internal Arr<Impl> impls { get => _impls.get; set => _impls.set(value); }

		internal Super(Loc loc, Klass klass, Ty superClass) {
			this.loc = loc;
			this.klass = klass;
			this.superClass = superClass;
		}

		public bool deepEqual(Super s) =>
			klass.equalsId<Klass, ClassLike.Id>(s.klass) &&
			superClass.equalsId<Ty, ClassLike.Id>(s.superClass) &&
			impls.deepEqual(s.impls);
		public Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(superClass), superClass.getId(), nameof(impls), Dat.arr(impls));
		public Id getId() => new Id(klass.getId(), superClass.getId());

		internal struct Id : ToData<Id> {
			internal readonly ClassLike.Id classId;
			internal readonly ClassLike.Id superClassId;
			internal Id(ClassLike.Id classId, ClassLike.Id superClassId) {
				this.classId = classId;
				this.superClassId = superClassId;
			}

			public bool deepEqual(Id i) =>
				classId.deepEqual(i.classId) &&
				superClassId.deepEqual(i.superClassId);
			public Dat toDat() => Dat.of(this, nameof(classId), classId, nameof(superClassId), superClassId);
		}
	}

	internal sealed class Impl : M, ToData<Impl> {
		internal readonly Loc loc;
		[ParentPointer] internal readonly Super super;
		[UpPointer] internal readonly Method.AbstractMethod implemented;
		internal readonly Expr body;

		internal Impl(Super super, Loc loc, Method.AbstractMethod implemented, Expr body) {
			this.super = super;
			this.loc = loc;
			this.implemented = implemented;
			this.body = body;
		}

		public bool deepEqual(Impl i) =>
			loc.deepEqual(i.loc) &&
			implemented.equalsId<Method.AbstractMethod, Method.Id>(i.implemented) &&
			body.deepEqual(i.body);
		public Dat toDat() => Dat.of(this,
			nameof(loc), loc,
			nameof(super), super.getId(),
			nameof(implemented), implemented.getId(),
			nameof(body), body);
	}

	// Slot or Method
	abstract class Member : M, ToData<Member> {
		internal readonly Loc loc;
		internal readonly Sym name;
		internal Member(Loc loc, Sym name) { this.loc = loc; this.name = name; }
		public abstract bool deepEqual(Member m);
		public abstract Dat toDat();
	}

	// `fun` or `def` or `impl`, or a builtin method.
	abstract class Method : Member, ToData<Method>, Identifiable<Method.Id> {
		internal struct Id : ToData<Id> {
			internal readonly ClassLike.Id klassId;
			internal readonly Sym name;
			internal Id(ClassLike.Id klass, Sym name) { this.klassId = klass; this.name = name; }
			public bool deepEqual(Id m) => klassId.deepEqual(m.klassId) && name.deepEqual(m.name);
			public Dat toDat() => Dat.of(this, nameof(klassId), klassId, nameof(name), name);
		}

		// Method is used in a Dictionary in ILEmitter.cs
		public sealed override bool Equals(object o) => object.ReferenceEquals(o, this);
		public sealed override int GetHashCode() => name.GetHashCode();

		[ParentPointer] internal readonly ClassLike klass;
		internal abstract bool isStatic { get; }
		[UpPointer] internal readonly Ty returnTy;
		internal readonly Arr<Parameter> parameters;

		public sealed override bool deepEqual(Member m) => m is Method mt && deepEqual(mt);
		public abstract bool deepEqual(Method m);
		public Id getId() => new Id(klass.getId(), name);

		private Method(ClassLike klass, Loc loc, Ty returnTy, Sym name, Arr<Parameter> parameters) : base(loc, name) {
			this.klass = klass;
			this.returnTy = returnTy;
			this.parameters = parameters;
		}

		internal uint arity => parameters.length;

		// Since there's no shadowing allowed, parameters can be identified by just their name.
		internal sealed class Parameter : M, ToData<Parameter>, Identifiable<Sym> {
			internal readonly Loc loc;
			[UpPointer] internal readonly Ty ty;
			internal readonly Sym name;
			internal readonly uint index;

			internal Parameter(Loc loc, Ty ty, Sym name, uint index) {
				this.loc = loc;
				this.ty = ty;
				this.name = name;
				this.index = index;
			}

			public bool deepEqual(Parameter p) => loc.deepEqual(p.loc) && ty.equalsId<Ty, ClassLike.Id>(p.ty) && name.deepEqual(p.name) && index == p.index;
			public Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(ty), ty.getId(), nameof(name), name, nameof(index), Dat.num(index));
			Sym Identifiable<Sym>.getId() => name;
		}

		internal sealed class BuiltinMethod : Method, ToData<BuiltinMethod> {
			internal readonly MethodInfo methodInfo;

			internal BuiltinMethod(BuiltinClass klass, MethodInfo methodInfo)
				: base(klass, Loc.zero, BuiltinClass.fromDotNetType(methodInfo.ReturnType), getName(methodInfo), mapParams(methodInfo)) {
				this.methodInfo = methodInfo;
			}

			internal override bool isStatic => methodInfo.IsStatic;

			public override bool deepEqual(Method m) => m is BuiltinMethod b && deepEqual(b);
			public bool deepEqual(BuiltinMethod m) =>
				loc.deepEqual(m.loc) && name.deepEqual(m.name) && isStatic == m.isStatic && returnTy.equalsId<Ty, ClassLike.Id>(m.returnTy) && parameters.deepEqual(m.parameters);
			public override Dat toDat() => Dat.of(this,
				nameof(name), name,
				nameof(isStatic), Dat.boolean(isStatic),
				nameof(returnTy), returnTy.getId(),
				nameof(parameters), Dat.arr(parameters));

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
			internal readonly bool _isStatic;
			internal override bool isStatic => _isStatic;

			internal MethodWithBody(Klass klass, Loc loc, bool isStatic, Ty returnTy, Sym name, Arr<Parameter> parameters)
				: base(klass, loc, returnTy, name, parameters) {
				this._isStatic = isStatic;
			}

			public override bool deepEqual(Method m) => m is MethodWithBody mb && deepEqual(mb);
			public bool deepEqual(MethodWithBody m) => throw TODO(); // TODO: should methods with different (reference identity) parent be not equal?
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(isStatic), Dat.boolean(isStatic), nameof(returnTy), returnTy.getId(), nameof(name), name, nameof(parameters), Dat.arr(parameters));

			Late<Expr> _body;
			internal Expr body { get => _body.get; set => _body.set(value); }
		}

		internal sealed class AbstractMethod : Method, ToData<AbstractMethod> {
			internal override bool isStatic => false;

			internal AbstractMethod(Klass klass, Loc loc, Ty returnTy, Sym name, Arr<Parameter> parameters)
				: base(klass, loc, returnTy, name, parameters) {}

			public override bool deepEqual(Method m) => m is AbstractMethod a && deepEqual(a);
			public bool deepEqual(AbstractMethod a) => throw TODO();
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(returnTy), returnTy.getId(), nameof(name), name, nameof(parameters), Dat.arr(parameters));
		}
	}

	abstract class Pattern : M, ToData<Pattern> {
		internal readonly Loc loc;
		Pattern(Loc loc) { this.loc = loc; }

		public abstract bool deepEqual(Pattern p);
		public abstract Dat toDat();

		internal sealed class Ignore : Pattern, ToData<Ignore> {
			internal Ignore(Loc loc) : base(loc) { }
			public override bool deepEqual(Pattern p) => p is Ignore i && deepEqual(i);
			public bool deepEqual(Ignore i) => loc.deepEqual(i.loc);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc);
		}
		internal sealed class Single : Pattern, ToData<Single>, Identifiable<Sym>, IEquatable<Single> {
			internal readonly Ty ty;
			internal readonly Sym name;
			internal Single(Loc loc, Ty ty, Sym name) : base(loc) {
				this.ty = ty;
				this.name = name;
			}

			bool IEquatable<Single>.Equals(Single s) => object.ReferenceEquals(this, s);
			public override int GetHashCode() => name.GetHashCode();
			public override bool deepEqual(Pattern p) => p is Single s && deepEqual(s);
			public bool deepEqual(Single s) => loc.deepEqual(s.loc) && ty.equalsId<Ty, ClassLike.Id>(s.ty) && name.deepEqual(s.name);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(ty), ty, nameof(name), name);
			public Sym getId() => name;
		}
		internal sealed class Destruct : Pattern, ToData<Destruct> {
			internal readonly Arr<Pattern> destructuredInto;
			internal Destruct(Loc loc, Arr<Pattern> destructuredInto) : base(loc) {
				this.destructuredInto = destructuredInto;
			}

			public override bool deepEqual(Pattern p) => p is Destruct d && deepEqual(d);
			public bool deepEqual(Destruct d) => loc.deepEqual(d.loc) && destructuredInto.deepEqual(d.destructuredInto);
			public override Dat toDat() => Dat.of(this, nameof(loc), loc, nameof(destructuredInto), Dat.arr(destructuredInto));
		}
	}
}
