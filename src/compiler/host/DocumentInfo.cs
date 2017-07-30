using System;

using Diag;

struct DocumentInfo : ToData<DocumentInfo> {
	internal readonly string text;
	internal readonly Either<Ast.Module, Diagnostic> parseResult;
	internal readonly uint version;

	internal static DocumentInfo parse(string text, uint version) =>
		new DocumentInfo(text, version, Parser.parse(text));

	DocumentInfo(string text, uint version, Either<Ast.Module, Diagnostic> parseResult) {
		this.text = text;
		this.version = version;
		this.parseResult = parseResult;
	}

	internal bool sameVersionAs(DocumentInfo document) =>
		version == document.version;

	public override bool Equals(object o) => throw new NotSupportedException();
	public override int GetHashCode() => throw new NotSupportedException();
	public bool deepEqual(DocumentInfo d) =>
		text == d.text &&
		parseResult.deepEqual(d.parseResult) &&
		version == d.version;
	public Dat toDat() => Dat.of(this, nameof(text), Dat.str(text), nameof(parseResult), Dat.either(parseResult), nameof(version), Dat.nat(version));
}
