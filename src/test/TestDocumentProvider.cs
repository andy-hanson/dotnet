using System.Collections.Generic;

using static Utils;

sealed class TestDocumentProvider : DocumentProvider {
	readonly FileInput fileInput;
	readonly Dictionary<Path, Arr<(LineAndColumnLoc, string)>> errorsDict = new Dictionary<Path, Arr<(LineAndColumnLoc, string)>>();

	internal TestDocumentProvider(Path rootDir) {
		fileInput = new NativeFileInput(rootDir);
	}

	internal Dictionary<Path, Arr<(LineAndColumnLoc, string)>> getDiagnostics() => errorsDict;

	Sym DocumentProvider.rootName => fileInput.rootName;

	bool DocumentProvider.getDocument(Path path, out DocumentInfo di) {
		if (!fileInput.read(path, out var content)) {
			di = default(DocumentInfo);
			return false;
		}

		var (textWithoutErrors, errors) = parseExpectedErrors(content);
		errorsDict.Add(path, errors);
		di = DocumentInfo.parse(textWithoutErrors, version: 0);
		return true;
	}

	static (string textWithoutErrors, Arr<(LineAndColumnLoc, string)> errors) parseExpectedErrors(string code) {
		/*
		E.g.:
			fun Nat f()
				true
				~~~~
			!!! This is not a Nat.
			!!! Why would you think that's a Nat?
		We require that error spans take up only 1 line. (Error text can take up many lines.)
		*/

		var lines = code.split('\n');
		var goodLines = StringMaker.create();
		var errs = Arr.builder<(LineAndColumnLoc, string)>();

		assert(lines.last == string.Empty);
		// Don't bother with last line, it's empty.
		for (uint lineNumber = 0; lineNumber < lines.length - 1; lineNumber++) {
			var line = lines[lineNumber];
			for (uint i = 0; i < line.Length; i++) {
				switch (line.at(i)) {
					case '\t':
						break;

					case '~':
						var errLc = LineAndColumnLoc.singleLine(lineNumber, i, unsigned(line.Length));
						for (; i < line.Length; i++)
							assert(line.at(i) == '~', "Line starting with '~' must constist of *only* '~'");

						// The next line *must* contain error text.
						var errorText = StringMaker.create();
						lineNumber++;
						var eLineFirst = lines[lineNumber];
						const string errorTextStart = "!!! ";
						assert(eLineFirst.StartsWith(errorTextStart));
						errorText.addSlice(eLineFirst, unsigned(errorTextStart.Length));

						while (true) {
							var eLine = lines[lineNumber + 1];
							if (eLine.StartsWith(errorTextStart)) {
								lineNumber++;
								errorText.addSlice(eLine, unsigned(errorTextStart.Length));
							} else
								break;
						}

						errs.add((errLc, errorText.finish()));
						goto nextLine;

					default:
						goto thisLineNotAnErrorLine;
				}
			}

			// Not an error line.
			thisLineNotAnErrorLine:
			goodLines.add(line).add('\n');

			#pragma warning disable S108 // Allow empty block, need something to label
			nextLine: {}
			#pragma warning restore
		}

		return (goodLines.finish(), errs.finish());
	}
}
