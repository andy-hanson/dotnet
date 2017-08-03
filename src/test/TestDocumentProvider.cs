using System.Collections.Generic;

using static Utils;

sealed class TestDocumentProvider : DocumentProvider {
	readonly FileInput fileInput;
	readonly Dictionary<Path, Arr<(LineAndColumnLoc, string)>> errorsDict = new Dictionary<Path, Arr<(LineAndColumnLoc, string)>>();

	internal TestDocumentProvider(Path rootDir) {
		fileInput = new NativeFileInput(rootDir);
	}

	internal Dictionary<Path, Arr<(LineAndColumnLoc, string)>> getExpectedDiagnostics() => errorsDict;

	Sym DocumentProvider.rootName => fileInput.rootName;

	bool DocumentProvider.getDocument(Path path, out DocumentInfo di) {
		if (!fileInput.read(path, out var content)) {
			di = default(DocumentInfo);
			return false;
		}

		var (textWithoutErrors, errors) = parseExpectedErrors(content);
		if (errors.any)
			errorsDict.Add(path, errors);
		di = DocumentInfo.parse(textWithoutErrors, version: 0);
		return true;
	}

	static (string textWithoutErrors, Arr<(LineAndColumnLoc, string)> errors) parseExpectedErrors(string code) {
		var goodLines = StringMaker.create();
		var expectedDiagnostics = Arr.builder<(LineAndColumnLoc, string)>();
		uint goodLineNumber = 0;
		uint lastGoodStartIndex = 0;
		uint lastGoodLineEnd = 0; // To index *past* the '\n'.
		uint lastGoodLineIndent = 0;
		uint i = 0;
		while (i != code.Length) {
			uint tabIndent = 0;
			uint spacesIndent = 0;
			// This switch statement only runs until we know whether we're on an error line or not.
			switch (code.at(i)) {
				case '\t':
					if (spacesIndent != 0) throw TODO();
					// Should match previous line's indentation.
					tabIndent++;
					i++;
					break;

				case ' ':
					spacesIndent++;
					i++;
					break;

				case '~':
					if (tabIndent != lastGoodLineIndent)
						throw TODO(); // error: Error line should match tab indent of previous line. (Other indent should be spaces.)

					var start = i;
					i++;
					#pragma warning disable S108 // empty block
					for (; i != code.Length && code.at(i) == '~'; i++) {}
					#pragma warning restore
					if (i == code.Length)
						throw TODO(); // Error: Should be followed by a `!` line.
					if (code.at(i) != '\n')
						throw TODO(); // error: If there is `~` on a line, that should be all there is.
					var diagnosticWidth = i - start;
					i++; // Past the '\n'

					goodLines.addSlice(code, lastGoodStartIndex, lastGoodLineEnd);
					var totalIndent = tabIndent + spacesIndent;
					if (goodLineNumber == 0) throw TODO(); // error: Shouldn't begin with a `~` line!
					// The error will apply to the *previous* line.
					var errLc = LineAndColumnLoc.singleLine(goodLineNumber - 1, totalIndent, totalIndent + diagnosticWidth);

					// Next line must contain error text.
					if (code.at(i) != '!') throw TODO();
					i++;
					if (code.at(i) != ' ') throw TODO();
					i++;

					var errorText = StringMaker.create();
					for (; i != code.Length && code.at(i) != '\n'; i++)
						errorText.add(code.at(i));
					if (i != code.Length) i++;

					while (i != code.Length && code.at(i) == '!') {
						// Collect more lines of diagnostic text.
						i++;
						if (code.at(i) != ' ') throw TODO(); // error: Must always begin with `! `
						errorText.add('\n');
						for (; i != code.Length && code.at(i) != '\n'; i++)
							errorText.add(code.at(i));
						if (i != code.Length) i++;
					}

					expectedDiagnostics.add((errLc, errorText.finish()));

					lastGoodStartIndex = i;
					lastGoodLineEnd = i;
					break;

				default:
					#pragma warning disable S108 // empty block
					for (; code.at(i) != '\n' && i != code.Length; i++) {}
					#pragma warning restore
					if (i != code.Length) {
						i++;
						lastGoodLineEnd = i;
						goodLineNumber++;
					}
					lastGoodLineIndent = tabIndent;
					break;
			}
		}

		// Optimization -- if we never saw a '~' just return the original string.
		if (lastGoodStartIndex == 0) {
			assert(goodLines.isEmpty);
			return (code, Arr.empty<(LineAndColumnLoc, string)>());
		} else {
			goodLines.addSlice(code, lastGoodStartIndex, unsigned(code.Length));
			return (goodLines.finish(), expectedDiagnostics.finish());
		}
	}
}
