using System;
using System.Text;

using static Utils;

#pragma warning disable CC0068 // Lots of unused stuff here.

class SourceMapSpan {
	internal readonly uint emittedLine;
	internal readonly uint emittedColumn;
	internal uint sourceLine;
	internal uint sourceColumn;

	internal SourceMapSpan(uint emittedLine, uint emittedColumn, uint sourceLine, uint sourceColumn) {
		this.emittedLine = emittedLine;
		this.emittedColumn = emittedColumn;
		this.sourceLine = sourceLine;
		this.sourceColumn = sourceColumn;
	}

	internal static readonly SourceMapSpan defaultLastEncoded = new SourceMapSpan(
		emittedLine: 1,
		emittedColumn: 1,
		sourceLine: 1,
		sourceColumn: 1);
}

interface SourceMapSource {
	string fileName { get; }
	string text { get; }
	LineColumnGetter lineMap { get; }
}

struct SourceMapDataBuilder {
	internal string sourceMapFilePath; // Where the sourcemap file is written
	internal string jsSourceMappingURL; // source map URL written in the .js file
	internal string sourceMapFile; // Source map's file field - .js file name
	internal string sourceMapSourceRoot; // Source map's sourceRoot field - location where the sources will be present if not ""
	internal Arr<string> sourceMapSources; // Source map's sources field - list of sources that can be indexed in this source map
	//internal Op<Arr<string>> sourceMapSourcesContent; // Source map's sourcesContent field - list of the sources' text to be embedded in the source map
	internal Arr<string> inputSourceFileNames; // Input source file (which one can use on program to get the file), 1:1 mapping with the sourceMapSources list
	internal Arr.Builder<string> sourceMapNames; // Source map's names field - list of names that can be indexed in this source map
	internal StringBuilder sourceMapMappings; // Source map's mapping field - encoded source map spans
}

interface EmitTextWriter {
	uint curLine { get; }
	uint curColumn { get; }
}

struct SourceMap : ToData<SourceMap> {
	readonly uint version;
	readonly string file;
	readonly string sourceRoot;
	readonly Arr<string> sources;
	readonly Arr<string> names;
	readonly string mappings;

	internal SourceMap(uint version, string file, string sourceRoot, Arr<string> sources, Arr<string> names, string mappings) {
		this.version = version;
		this.file = file;
		this.sourceRoot = sourceRoot;
		this.sources = sources;
		this.names = names;
		this.mappings = mappings;
	}

	public bool deepEqual(SourceMap sm) => throw new NotSupportedException();
	public Dat toDat() => Dat.of(this,
		nameof(version), Dat.num(version),
		nameof(file), Dat.str(file),
		nameof(sourceRoot), Dat.str(sourceRoot),
		nameof(sources), Dat.arr(sources),
		nameof(names), Dat.arr(names),
		nameof(mappings), Dat.str(mappings));
}

class SourceMapWriter {
	readonly EmitTextWriter writer;
	readonly string sourceMapDir;
	readonly SourceMapSource currentSource; // TODO: write to!

	Op<SourceMapSpan> lastRecordedSourceMapSpan;
	Op<SourceMapSpan> lastEncodedSourceMapSpan;

	readonly SourceMapDataBuilder smd;

	internal SourceMapWriter(EmitTextWriter writer, Path filePath, string fileText, string sourceMapFilePath, Estree.Program sourceFile) {
		unused(sourceFile);

		this.writer = writer;

		currentSource = null;
		if (Math.Abs(1) == 1) throw TODO();

		lastEncodedSourceMapSpan = Op.Some(SourceMapSpan.defaultLastEncoded);

		smd = new SourceMapDataBuilder {
			sourceMapFilePath = sourceMapFilePath,
			jsSourceMappingURL = null, // We will inline source maps.
			sourceMapFile = filePath.last,
			sourceMapSourceRoot = string.Empty,
			sourceMapSources = Arr.of(fileText),
			inputSourceFileNames = Arr.of(filePath.toPathString()),
			sourceMapNames = Arr.builder<string>(),
			sourceMapMappings = new StringBuilder()
		};

		sourceMapDir = filePath.directory().toPathString();

		//TODO: setSourceFile
	}

	void encodeLastRecordedSourceMapSpan() {
		if (lastRecordedSourceMapSpan.get(out var lastRecorded)) {
			return;
		}
		if (lastEncodedSourceMapSpan.get(out var lastEncoded) && lastEncoded == lastRecorded) {
			return;
		}

		var prevEncodedEmittedColumn = lastEncoded.emittedColumn;
		// Line/Comma delimiters
		if (lastEncoded.emittedLine == lastRecorded.emittedLine)
			smd.sourceMapMappings.Append(',');
		else {
			// Emit line delimiters
			for (var encodedLine = lastEncoded.emittedLine; encodedLine < lastRecorded.emittedLine; encodedLine++)
				smd.sourceMapMappings.Append(';');
			prevEncodedEmittedColumn = 1;
		}

		// 1. Relative Column 0 based
		base64VLQFormatEncode(smd.sourceMapMappings, lastRecorded.emittedColumn - prevEncodedEmittedColumn);
		// 2. Relative sourceIndex
		base64VLQFormatEncode(smd.sourceMapMappings, 0); //lastRecorded.sourceIndex - lastEncoded.sourceIndex);
		// 3. Relative sourceLine 0 based
		base64VLQFormatEncode(smd.sourceMapMappings, lastRecorded.sourceLine - lastEncoded.sourceLine);
		// 4. Relative sourceColumn 0 based
		base64VLQFormatEncode(smd.sourceMapMappings, lastRecorded.sourceColumn - lastEncoded.sourceColumn);

		lastEncodedSourceMapSpan = lastRecordedSourceMapSpan;
	}

	void emitPos(Pos pos) {
		var sourceLinePos = currentSource.lineMap.lineAndColumnAtPos(pos);
		// Convert to one-based.
		sourceLinePos = new LineAndColumn(sourceLinePos.line + 1, sourceLinePos.column + 1);

		var emittedLine = writer.curLine;
		var emittedColumn = writer.curColumn;

		if (!lastRecordedSourceMapSpan.get(out var lastRecorded) ||
			lastRecorded.emittedLine != emittedLine ||
			lastRecorded.emittedColumn != emittedColumn ||
			lastRecorded.sourceLine > sourceLinePos.line ||
			lastRecorded.sourceLine == sourceLinePos.line && lastRecorded.sourceColumn > sourceLinePos.column) {
			// Encode the last recordedSpan before assigning new
			encodeLastRecordedSourceMapSpan();

			// New span
			lastRecordedSourceMapSpan = Op.Some(new SourceMapSpan(emittedLine, emittedColumn, sourceLinePos.line, sourceLinePos.column));
		}
		else {
			// Take the new pos instead since there is no change in emittedLine and column since last location
			lastRecorded.sourceLine = sourceLinePos.line;
			lastRecorded.sourceColumn = sourceLinePos.column;
		}
	}

	/**
	emitCallback is the callback that actually emits the node.
	*/
	void emitNodeWithSourceMap(EmitHint hint, Estree.Node node, Action<EmitHint, Estree.Node> emitCallback) {
		emitPos(node.loc.start);
		emitCallback(hint, node);
		emitPos(node.loc.end);
	}

	SourceMap finish() {
		encodeLastRecordedSourceMapSpan();

		return new SourceMap(
			version: 3,
			file: smd.sourceMapFile,
			sourceRoot: smd.sourceMapSourceRoot,
			sources: smd.sourceMapSources,
			names: smd.sourceMapNames.finish(),
			mappings: smd.sourceMapMappings.ToString());
	}

	/*string getSourceMappingURL() {
		//inline source map
		var sourceMap = finish();
		var text = JsonWriter.write(sourceMap);
		var base64SourceMapText = convertToBase64(text);
		var url = $"data:application/json;base64,{base64SourceMapText}";
		smd.jsSourceMappingURL = url;
		return url;
	}*/

	static char base64FormatEncode(uint inValue) {
		const string base64Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
		assert(base64Chars.Length == 64);
		return base64Chars.at(inValue);
	}

	static void base64VLQFormatEncode(StringBuilder output, uint inValue) {
		// Add a new least significant bit that has the sign of the value.
		// if negative number the least significant bit that gets added to the number has value 1
		// else least significant bit value that gets added is 0
		// eg. -1 changes to binary : 01 [1] => 3
		//     +1 changes to binary : 01 [0] => 2
		// Don't think it's ever negative?
		inValue = inValue << 1;

		do {
			var currentDigit = inValue & 0b11111;
			inValue = inValue >> 5;
			if (inValue != 0) {
				// There are still more digits to decode, set the msb (6th bit)
				currentDigit = currentDigit | 0b100000;
			}
			output.Append(base64FormatEncode(currentDigit));
		} while (inValue != 0);
	}

	static char base64Digit(uint someByte) {
		const string base64Digits = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=";
		return base64Digits.at(someByte);
	}

	static string convertToBase64(string input) {
		var result = new StringBuilder();
		var charCodes = getExpandedCharCodes(input);
		uint i = 0;
		var length = charCodes.length;
		uint byte1, byte2, byte3, byte4;

		while (i < length) {
			// Convert every 6-bits in the input 3 character points
			// into a base64 digit
			byte1 = charCodes[i] >> 2;
			byte2 = (charCodes[i] & 0b00000011) << 4 | charCodes[i + 1] >> 4;
			byte3 = (charCodes[i + 1] & 0b00001111) << 2 | charCodes[i + 2] >> 6;
			byte4 = charCodes[i + 2] & 0b00111111;

			// We are out of characters in the input, set the extra
			// digits to 64 (padding character).
			if (i + 1 >= length) {
				byte3 = byte4 = 64;
			}
			else if (i + 2 >= length) {
				byte4 = 64;
			}

			// Write to the output
			result.Append(base64Digit(byte1));
			result.Append(base64Digit(byte2));
			result.Append(base64Digit(byte3));
			result.Append(base64Digit(byte4));

			i += 3;
		}

		return result.ToString();
	}

	//TODO: C# may be encoding strings strangely.
	static Arr<uint> getExpandedCharCodes(string input) {
		var res = Arr.builder<uint>();

		foreach (var charCodeChar in input) {
			var ch = (uint)charCodeChar;
			if (ch < 0x80) {
				res.add(ch);
			} else if (ch < 0x800) {
				res.add((ch >> 6) | 0b11000000);
				res.add((ch & 0b00111111) | 0b10000000);
			} else if (ch < 0x10000) {
				res.add((ch >> 12) | 0b11100000);
				res.add(((ch >> 6) & 0b00111111) | 0b10000000);
				res.add((ch & 0b00111111) | 0b10000000);
			} else {
				assert(ch < 0x20000);
				res.add((ch >> 18) | 0b11110000);
				res.add(((ch >> 12) & 0b00111111) | 0b10000000);
				res.add(((ch >> 6) & 0b00111111) | 0b10000000);
				res.add((ch & 0b00111111) | 0b10000000);
			}
		}

		return res.finish();
	}
}

enum EmitHint {
	SourceFile,         // Emitting a SourceFile
	Expression,         // Emitting an Expression
	IdentifierName,     // Emitting an IdentifierName
	Unspecified,        // Emitting an otherwise unspecified node
}
