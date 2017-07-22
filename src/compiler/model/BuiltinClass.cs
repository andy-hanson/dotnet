using System;
using System.Collections.Generic;
using System.Reflection;

using static Utils;

namespace Model {
	sealed class BuiltinClass : ClassLike, ToData<BuiltinClass>, IEquatable<BuiltinClass> {
		internal readonly Type dotNetType;
		readonly Late<Arr<AbstractMethodLike>> _abstractMethods;
		// Use AbstractMethodLike instead of BuiltinAbstractMethod so this type will be compatible with KlassHead.Abstract's abstractMethods
		internal Arr<AbstractMethodLike> abstractMethods {
			get { assert(isAbstract); return _abstractMethods.get; }
			private set => _abstractMethods.set(value);
		}

		//kill?
		internal Late<Arr<MethodInfo>> _overrides;
		internal Arr<MethodInfo> overrides { get => _overrides.get; set => _overrides.set(value); }

		//kill?
		internal override Arr<Super> supers {
			get {
				if (dotNetType.hasAttribute<HidSuperClassAttribute>())
					return Arr.empty<Super>();

				if (!dotNetType.IsValueType) {
					var baze = dotNetType.BaseType;
					if (baze != null && baze != typeof(object)) {
						// TODO: handle builtins with supertypes
						// (TODO: Super has location information, may have to abstract over that)
						//var baseType = fromDotNetType(baze);
						throw TODO();
					}
				}

				foreach (var iface in dotNetType.GetInterfaces()) {
					var gen = iface.GetGenericTypeDefinition();
					if (gen != typeof(ToData<>) && gen != typeof(DeepEqual<>))
						throw TODO();
				}
				return Arr.empty<Super>();
			}
		}
		internal override bool isAbstract => dotNetType.IsAbstract;

		Dict<Sym, Member> _membersMap;
		internal override Dict<Sym, Member> membersMap => _membersMap;

		static readonly Dictionary<Sym, BuiltinClass> byName = new Dictionary<Sym, BuiltinClass>();

		internal static IEnumerable<BuiltinClass> all() =>
			byName.Values;

		static BuiltinClass() {
			foreach (var klass in Builtins.allTypes)
				fromDotNetType(klass);
		}

		internal static readonly BuiltinClass Void = fromDotNetType(typeof(Builtins.Void));
		internal static readonly BuiltinClass Bool = fromDotNetType(typeof(Builtins.Bool));
		internal static readonly BuiltinClass Nat = fromDotNetType(typeof(Builtins.Nat));
		internal static readonly BuiltinClass Int = fromDotNetType(typeof(Builtins.Int));
		internal static readonly BuiltinClass Real = fromDotNetType(typeof(Builtins.Real));
		internal static readonly BuiltinClass String = fromDotNetType(typeof(Builtins.String));
		internal static readonly BuiltinClass Exception = fromDotNetType(typeof(Builtins.Exception));

		/** Get an already-registered type by name. */
		internal static bool tryGet(Sym name, out BuiltinClass b) => byName.TryGetValue(name, out b);

		/** Safe to call this twice on the same type. */
		internal static BuiltinClass fromDotNetType(Type dotNetType) {
			assert(dotNetType.DeclaringType == typeof(Builtins));
			assert(dotNetType == typeof(Builtins.Exception) || !dotNetType.IsAbstract || dotNetType.IsInterface, "Use an interface instead of an abstract class.");

			var name = NameEscaping.unescapeTypeName(dotNetType.Name);

			if (byName.TryGetValue(name, out var old)) {
				assert(old.dotNetType == dotNetType);
				return old;
			}

			var klass = new BuiltinClass(name, dotNetType);
			// Important that we put this in the map *before* initializing it, so a type's methods can refer to itself.
			byName[name] = klass;

			foreach (var field in dotNetType.GetFields()) {
				if (field.hasAttribute<HidAttribute>())
					continue;
				throw TODO();
			}

			var abstracts = Arr.builder<AbstractMethodLike>();

			var dotNetMethods = dotNetType.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
			var overrides = Arr.builder<MethodInfo>();
			klass._membersMap = dotNetMethods.mapToDict<MethodInfo, Sym, Member>(method => {
				if (method.hasAttribute<HidAttribute>())
					return Op<(Sym, Member)>.None;

				if (method.GetBaseDefinition() != method) {
					overrides.add(method);
					return Op<(Sym, Member)>.None;
				}

				if (method.IsVirtual && !method.IsAbstract) {
					// Must be an override. Don't add to the table.
					assert(method.GetBaseDefinition() != method);
					return Op<(Sym, Member)>.None;
				}

				var m2 = BuiltinMethod.of(klass, method);
				if (m2 is BuiltinAbstractMethod b)
					abstracts.add(b);
				return Op.Some<(Sym, Member)>((m2.name, m2));
			});

			klass.overrides = overrides.finish();
			klass.abstractMethods = abstracts.finish();

			return klass;
		}

		private BuiltinClass(Sym name, Type dotNetType) : base(name) { this.dotNetType = dotNetType; }

		public override ClassLike.Id getId() => ClassLike.Id.ofBuiltin(name);

		bool IEquatable<BuiltinClass>.Equals(BuiltinClass other) => object.ReferenceEquals(this, other);
		public override bool deepEqual(ClsRef c) => throw new NotSupportedException();
		public bool deepEqual(BuiltinClass b) => throw new NotSupportedException(); // This should never happen.
		public override int GetHashCode() => name.GetHashCode();
		public override Dat toDat() => Dat.of(this, nameof(name), name);

		public override string ToString() => $"BuiltinClass({name})";
	}
}
