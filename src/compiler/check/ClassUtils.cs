using Model;

static class ClassUtils {
	internal static bool tryGetMemberFromClassDeclaration(ClassDeclarationLike classDeclaration, Sym memberName, out InstMember member) =>
		recur(classDeclaration, TyReplacer.doNothingReplacer, memberName, out member);

	internal static bool tryGetMemberOfInstCls(InstCls cls, Sym memberName, out InstMember member) =>
		recur(cls.classDeclaration, TyReplacer.ofInstCls(cls), memberName, out member);

	static bool recur(ClassDeclarationLike classDeclaration, TyReplacer replacer, Sym memberName, out InstMember member) {
		if (classDeclaration.membersMap.get(memberName, out var memberDecl)) {
			member = new InstMember(memberDecl, replacer);
			return true;
		}

		foreach (var super in classDeclaration.supers) {
			var superReplacer = replacer.combine(TyReplacer.ofInstCls(super.superClass));
			if (recur(super.superClass.classDeclaration, superReplacer, memberName, out member))
				return true;
		}

		member = default(InstMember);
		return false;
	}
}
