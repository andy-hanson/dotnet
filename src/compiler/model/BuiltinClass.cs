using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using static Utils;

namespace Model {
	sealed class BuiltinClass : ClassLike, ToData<BuiltinClass> {
		internal readonly Type dotNetType;
		readonly Late<Arr<Method>> _abstractMethods;
		// These will all be BuiltinMethods of course. But Arr is invariant and we want a type compatible with Klass.Head.Abstract.
		internal Arr<Method> abstractMethods {
			get { assert(isAbstract); return _abstractMethods.get; }
			private set => _abstractMethods.set(value);
		}

		internal override Arr<Super> supers {
			get {
				// TODO: handle builtins with supertypes
				// (TODO: Super has location information, may have to abstract over that)
				if (dotNetType.BaseType != typeof(object)) throw TODO();
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

		static Dict<Sym, string> operatorEscapes = Dict.of(
			("==", "_eq"),
			("+", "_add"),
			("-", "_sub"),
			("*", "_mul"),
			("/", "_div"),
			("^", "_pow")).mapKeys(Sym.of);
		static Dict<string, Sym> operatorUnescapes = operatorEscapes.reverse();

		internal static readonly BuiltinClass Void = fromDotNetType(typeof(Builtins.Void));
		internal static readonly BuiltinClass Bool = fromDotNetType(typeof(Builtins.Bool));
		internal static readonly BuiltinClass Int = fromDotNetType(typeof(Builtins.Int));
		internal static readonly BuiltinClass Float = fromDotNetType(typeof(Builtins.Float));
		internal static readonly BuiltinClass String = fromDotNetType(typeof(Builtins.String));

		/** Get an already-registered type by name. */
		internal static bool tryGet(Sym name, out BuiltinClass b) => byName.TryGetValue(name, out b);

		/** Safe to call this twice on the same type. */
		internal static BuiltinClass fromDotNetType(Type dotNetType) {
			assert(dotNetType.DeclaringType == typeof(Builtins));

			var name = unescapeName(dotNetType.Name);

			if (byName.TryGetValue(name, out var old)) {
				assert(old.dotNetType == dotNetType);
				return old;
			}

			var klass = new BuiltinClass(name, dotNetType);
			// Important that we put this in the map *before* initializing it, so a type's methods can refer to itself.
			byName[name] = klass;

			foreach (var field in dotNetType.GetFields()) {
				if (field.GetCustomAttribute<HidAttribute>(inherit: true) != null)
					continue;
				throw TODO();
			}

			var abstracts = Arr.builder<Method>();

			var dotNetMethods = dotNetType.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
			klass._membersMap = dotNetMethods.mapToDict<MethodInfo, Sym, Member>(method => {
				if (method.GetCustomAttribute<HidAttribute>(inherit: true) != null)
					return Op<(Sym, Member)>.None;

				if (method.IsVirtual && !method.IsAbstract) {
					// Must be an override. Don't add to the table.
					assert(method.GetBaseDefinition() != method);
					return Op<(Sym, Member)>.None;
				}

				var m2 = Method.BuiltinMethod.of(klass, method);
				if (m2.isAbstract)
					abstracts.add(m2);
				return Op.Some<(Sym, Member)>((m2.name, m2));
			});

			klass.abstractMethods = abstracts.finish();

			return klass;
		}

		private BuiltinClass(Sym name, Type dotNetType) : base(name) { this.dotNetType = dotNetType; }

		public override ClassLike.Id getId() => ClassLike.Id.ofBuiltin(name);

		public override bool deepEqual(Ty t) => throw new NotSupportedException();
		public bool deepEqual(BuiltinClass b) => throw new NotSupportedException(); // This should never happen.
		public override int GetHashCode() => name.GetHashCode();
		public override Dat toDat() => Dat.of(this, nameof(name), name);

		/*internal static string escapeName(Sym name) {
			if (operatorEscapes.get(name, out var sym))
				return sym;

			var str = name.str;

			foreach (var ch in str)
				if (CharUtils.isLetter(ch))
					unreachable();

			return str;
		}*/

		internal static Sym unescapeName(string name) {
			if (operatorUnescapes.get(name, out var v))
				return v;

			var sb = new StringBuilder();
			foreach (var ch in name) {
				if (ch == '_')
					sb.Append('-');
				else {
					assert(CharUtils.isNameChar(ch));
					sb.Append(ch);
				}
			}
			return Sym.of(sb);
		}

		public override string ToString() => $"BuiltinClass({name})";
	}
}
