using System;
using System.Diagnostics;
using System.Reflection;

using BuiltinAttributes;
using static Utils;

namespace Model {
	[DebuggerDisplay(":{name.str}")]
	sealed class BuiltinClass : ClassDeclarationLike, Imported, ToData<BuiltinClass>, IEquatable<BuiltinClass> {
		internal readonly Type dotNetType;
		readonly Late<Arr<AbstractMethodLike>> _abstractMethods;
		// Use AbstractMethodLike instead of BuiltinAbstractMethod so this type will be compatible with KlassHead.Abstract's abstractMethods
		internal Arr<AbstractMethodLike> abstractMethods {
			get { assert(isAbstract); return _abstractMethods.get; }
			set => _abstractMethods.set(value);
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

		Late<Dict<Sym, MemberDeclaration>> _membersMap;
		internal void setMembersMap(Dict<Sym, MemberDeclaration> value) { _membersMap.set(value); }
		internal override Dict<Sym, MemberDeclaration> membersMap => _membersMap.get;

		static BuiltinClass _load<T>() => BuiltinsLoader.loadBuiltinClass(typeof(T));
		internal static readonly BuiltinClass Void = _load<Builtins.Void>();
		internal static readonly BuiltinClass Bool = _load<Builtins.Bool>();
		internal static readonly BuiltinClass Nat = _load<Builtins.Nat>();
		internal static readonly BuiltinClass Int = _load<Builtins.Int>();
		internal static readonly BuiltinClass Real = _load<Builtins.Real>();
		internal static readonly BuiltinClass String = _load<Builtins.String>();
		internal static readonly BuiltinClass Exception = _load<Builtins.Exception>();

		Sym Imported.name => name;
		ClassDeclarationLike Imported.importedClass => this;

		/** Only call this if you are BuiltinsLoader! */
		internal BuiltinClass(Sym name, Arr<TypeParameter> typeParameters, Type dotNetType) : base(name, typeParameters) { this.dotNetType = dotNetType; }

		public override ClassDeclarationLike.Id getId() => ClassDeclarationLike.Id.ofBuiltin(name);
		Dat Imported.getImportedId() => getId().toDat();

		bool IEquatable<BuiltinClass>.Equals(BuiltinClass other) => object.ReferenceEquals(this, other);
		bool DeepEqual<Imported>.deepEqual(Imported i) => object.ReferenceEquals(this, i);
		public override bool deepEqual(ClassDeclarationLike c) => throw new NotSupportedException(); // This should never happen.
		public bool deepEqual(BuiltinClass b) => throw new NotSupportedException(); // This should never happen.
		public override int GetHashCode() => name.GetHashCode();
		public override Dat toDat() => Dat.of(this, nameof(name), name);
	}
}
