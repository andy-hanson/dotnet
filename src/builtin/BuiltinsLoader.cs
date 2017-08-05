using System;
using System.Collections.Generic;
using System.Reflection;

using BuiltinAttributes;
using Model;
using static Utils;

static class BuiltinsLoader {
	static readonly Dictionary<Type, BuiltinClass> cache = new Dictionary<Type, BuiltinClass>();

	internal static readonly Arr<BuiltinClass> all = typeof(Builtins).GetNestedTypes().map(loadBuiltinClass);
	static readonly Dict<Sym, BuiltinClass> noImportBuiltins =
		all.mapDefinedToDict(cls =>
			!cls.dotNetType.hasAttribute<NoImportAttribute>() ? Op<(Sym, BuiltinClass)>.None : Op.Some((cls.name, cls)));

	static readonly Dict<Path, BuiltinClass> importBuiltins =
		all.mapDefinedToDict(cls =>
			cls.dotNetType.hasAttribute<NoImportAttribute>() ? Op<(Path, BuiltinClass)>.None : Op.Some((Path.fromParts(cls.name.str), cls)));

	internal static bool tryGetNoImportBuiltin(Sym name, out BuiltinClass b) => noImportBuiltins.get(name, out b);
	internal static bool tryImportBuiltin(Path path, out BuiltinClass b) => importBuiltins.get(path, out b);

	internal static InstCls instClsFromDotNetType(Type dotNetType) {
		if (dotNetType.IsGenericType)
			throw TODO(); // Error: Should not get a generic type here.
		if (dotNetType.IsConstructedGenericType)
			throw TODO();
		return InstCls.of(loadBuiltinClass(dotNetType), Arr.empty<Ty>());
	}

	/** Safe to call this twice on the same type. */
	internal static BuiltinClass loadBuiltinClass(Type dotNetType) {
		if (cache.TryGetValue(dotNetType, out var old)) {
			return old;
		}

		assert(dotNetType.DeclaringType == typeof(Builtins));
		assert(dotNetType == typeof(Builtins.Exception) || !dotNetType.IsAbstract || dotNetType.IsInterface, "Use an interface instead of an abstract class.");

		var name = NameEscaping.unescapeTypeName(dotNetType.Name);

		if (dotNetType.IsGenericType)
			throw TODO();
		var typeParameters = Arr.empty<TypeParameter>();

		var klass = new BuiltinClass(name, typeParameters, dotNetType);
		// Important that we put this in the map *before* initializing it, so a type's methods can refer to itself.
		cache.Add(dotNetType, klass);

		foreach (var field in dotNetType.GetFields()) {
			if (field.hasAttribute<HidAttribute>())
				continue;
			throw TODO();
		}

		var abstracts = Arr.builder<AbstractMethodLike>();

		var dotNetMethods = dotNetType.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
		var overrides = Arr.builder<MethodInfo>();
		klass.setMembersMap(dotNetMethods.mapToDict<MethodInfo, Sym, MemberDeclaration>(method => {
			if (method.hasAttribute<HidAttribute>())
				return Op<(Sym, MemberDeclaration)>.None;

			if (method.GetBaseDefinition() != method) {
				overrides.add(method);
				return Op<(Sym, MemberDeclaration)>.None;
			}

			if (method.IsVirtual && !method.IsAbstract) {
				// Must be an override. Don't add to the table.
				assert(method.GetBaseDefinition() != method);
				return Op<(Sym, MemberDeclaration)>.None;
			}

			var m2 = BuiltinMethod.of(klass, method);
			if (m2 is BuiltinAbstractMethod b)
				abstracts.add(b);
			return Op.Some<(Sym, MemberDeclaration)>((m2.name, m2));
		}));

		klass.overrides = overrides.finish();
		klass.abstractMethods = abstracts.finish();

		return klass;
	}
}
