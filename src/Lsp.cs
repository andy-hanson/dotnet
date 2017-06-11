using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.IO;
using System.Net;
using System.Text;

using static Utils;

namespace Lsp.Server {
	/*
	sealed class Server : IDisposable {
		readonly uint port;
		readonly Smartness smartness;
		readonly Op<Logger> logger;

		readonly Socket socket;
		//output;
		//input;

		internal Server(uint port, Smartness smartness, Op<Logger> logger) {
			this.port = port;
			this.smartness = smartness;
			this.logger = logger;

			//Open a socket at port.
			//TODO: not sure about thsi choice of SocketType...
			socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
			socket.Accept();

		}

		void IDisposable.Dispose() {
			socket.Dispose();
		}
	}*/

	//Layers!
	abstract class AbstractTcpServer : IDisposable {
		internal readonly uint port;
		readonly TcpListener server;
		readonly TcpClient client;
		readonly NetworkStream stream; //TODO: BufferedStream
		//readonly StreamReader reader;
		//readonly StreamWriter writer;

		internal AbstractTcpServer(uint port) {
			this.port = port;

			var localAddr = IPAddress.Parse("127.0.0.1");
			server = new TcpListener(localAddr, signed(port));

			server.Start();

			Console.WriteLine("waiting for connection...");

			client = server.AcceptTcpClient();

			Console.WriteLine("Connected!");

			stream = client.GetStream();

			//reader = new StreamReader(stream);
			//writer = new StreamWriter(stream);
		}

		internal void loop() {
			while (true) {
				single();
			}
		}

		protected abstract string getResponse(string input);

		void single() {
			var inputMessage = TcpUtils.readTcpString(stream);
			var response = getResponse(inputMessage);
			TcpUtils.writeTcpString(stream, response);
		}

		void IDisposable.Dispose() {
			client.Dispose();
		}
	}

	static class TcpUtils {
		internal static string readTcpString(Stream stream) {
			var contentLength = readContentLength(stream);
			var content = new byte[contentLength];
			var nBytesRead = stream.Read(content, 0, signed(contentLength));
			assert(nBytesRead == contentLength);
			return System.Text.Encoding.UTF8.GetString(content); //TODO: just directly parse the byte[]
		}

		const string contentLengthStr = "Content-Length: ";

		internal static void writeTcpString(Stream stream, string content) {
			var contentBytes = System.Text.Encoding.UTF8.GetBytes(content);
			var contentLengthBytes = System.Text.Encoding.UTF8.GetBytes($"{contentLengthStr}{contentBytes.Length}\r\n\r\n");
			stream.WriteAll(contentLengthBytes);
			stream.WriteAll(contentBytes);
		}

		static uint readContentLength(Stream stream) {
			StreamUtils.readExpected(stream, contentLengthStr);
			var contentLength = StreamUtils.readUintThenNewline(stream);
			StreamUtils.readNewline(stream, (char)stream.ReadByte()); // Two newlines!
			return contentLength;
		}
	}

	static class StreamUtils {
		internal static void WriteAll(this Stream stream, Byte[] bytes) {
			stream.Write(bytes, 0, bytes.Length);
		}

		internal static void readExpected(Stream s, string expected) {
			foreach (var ch in expected) {
				var b = s.ReadByte();
				assert(b == ch);
			}
		}

		internal static uint readUintThenNewline(Stream stream) {
			var fst = (char) stream.ReadByte();
			var x = toDigit(fst) ?? throw new Exception($"Expected a digit, got: {fst}");
			char ch;
			while (true) {
				ch = (char) stream.ReadByte();
				var d = toDigit(ch);
				if (!d.HasValue) {
					readNewline(stream, ch);
					break;
				}
				x *= 10;
				x += d.Value;
			}
			return x;
		}

		internal static void readNewline(Stream stream, int ch) {
			var ch2 = ch == '\r' ? stream.ReadByte() : ch;
			assert(ch2 == '\n');
		}

		//mv
		internal static uint? toDigit(char ch) {
			uint df = ((uint) ch) - '0';
			if (0 <= df && df <= 9) return df;
			return null;
		}
	}

	sealed class JsonRpcServer : AbstractTcpServer {
		readonly Op<Logger> logger;

		internal JsonRpcServer(uint port, Op<Logger> logger) : base(port) {
			this.logger = logger;
		}

		protected override string getResponse(string inputMessage) {
			logger.each(l => l.received(inputMessage));
			return "RESPONSEEE";
		}
	}

	struct Reader {
		readonly string str;
		uint line;
		uint lastLineStart;
		uint idx;

		internal Reader(string str) {
			this.str = str;
			this.line = 0;
			this.lastLineStart = 0;
			this.idx = 0;
		}

		internal string pos => $"{line}:{idx - lastLineStart}";

		internal char readAndSkipWhitespace() {
			var ch = readCh();
			while (isWhitespace(ch)) {
				if (ch == '\n') {
					line++;
					lastLineStart = idx + 1;
				}
				ch = readCh();
			}
			return ch;
		}

		internal char readNoWhitespace() {
			var ch = readCh();
			assert(ch != '\n');
			return ch;
		}

		internal void over() {
			while (idx != str.Length) {
				var ch = readCh();
				if (!isWhitespace(ch))
					throw new Exception($"Did not expect {ch}");
			}
		}

		char readCh() => str.at(idx);

		static bool isWhitespace(char ch) {
			switch (ch) {
				case ' ':
				case '\t':
				case '\r':
				case '\n':
					return true;
				default:
					return false;
			}
		}
	}

	struct JsonScanner {
		readonly Reader rdr;
		internal JsonScanner(string source) { this.rdr = new Reader(source); }

		internal void readDictSkipDict(string key, bool last) {
			readDictKey(key);
			skipDict();
			dictEntryEnd(rdr.readAndSkipWhitespace(), last);
		}

		internal void over() {
			rdr.over();
		}

		void skipDict() {
			readDictStart();
			var openBraces = 1;
			while (true) {
				switch (rdr.readAndSkipWhitespace()) {
					case '"':
						readStrBody();
						break;
					case '{':
						openBraces++;
						break;
					case '}':
						openBraces--;
						if (openBraces == 0)
							return;
						break;
				}
			}
		}

		internal void readArrayStart() { expect('['); }
		internal void readArrayEnd() { expect(']'); }
		internal void readDictKey(string key) { expectKey(key); } //Why have this fn?
		internal void readDictStart() { expect('{'); }
		/** USUALLY NOT NECESSARY (if read str with 'last') */
		internal void readDictEnd() { expect('}'); }
		internal void readNull() {
			expect('n');
			expect('u');
			expect('l');
			expect('l');
		}

		internal bool readBoolean() {
			var ch = rdr.readAndSkipWhitespace();
			switch (ch) {
				case 't':
					expect('r');
					expect('u');
					expect('e');
					return true;
				case 'f':
					expect('a');
					expect('l');
					expect('s');
					expect('e');
					return false;
				default:
					throw showError("'true' or 'false'", ch);
			}
		}

		internal void readEmptyDict() {
			readDictStart();
			readDictEnd();
		}

		void dictEntryEnd(char ch, bool last) {
			expect(last ? '}' : ',', ch);
		}

		/**
		Optionally reads 'key' with an int value.
		Next key should have a string value.
		*/
		//TODO:RENAME
		void mayReadDictUintThenString(string key, string nextKey, bool last, out int? intValue, out string strValue) {
			var actualKey = readKey();
			string actualNextKey;
			if (actualKey == key) {
				readInt(rdr.readAndSkipWhitespace(), out intValue, out var next);
				expect(',', next);
				actualNextKey = readKey();
			}
			else {
				intValue = null;
				actualNextKey = actualKey;
			}

			assertEqual(nextKey, actualNextKey);

			strValue = readStr();

			dictEntryEnd(rdr.readAndSkipWhitespace(), last);
		}

		internal Op<string> readDictStrOrNull(string key, bool last) {
			expectKey(key);
			var n = rdr.readAndSkipWhitespace();
			Op<string> x;
			if (n == 'n') {
				expect('u');
				expect('l');
				expect('l');
				x = Op<string>.None;
			} else
				x = Op.Some(readStr(n));
			dictEntryEnd(rdr.readAndSkipWhitespace(), last);
			return x;
		}

		internal string readDictStrEntry(string key, bool last = false) {
			expectKey(key);
			var res = readStr();
			dictEntryEnd(rdr.readAndSkipWhitespace(), last);
			return res;
		}

		//REANDME: readDictUintEntry
		internal uint readDictUintEntry(string key, bool last = false) {
			expectKey(key);
			readUint(rdr.readAndSkipWhitespace(), out var intValue, out var next);
			dictEntryEnd(next, last);
			return intValue;
		}

		private void readUint(char fst, out uint intValue, out char next) {
			//TODO: move toDigit
			var x = StreamUtils.toDigit(fst) ?? throw new Exception($"Expected a digit, got '{fst}");
			//TODO:share code with other readuint
			char ch;
			while (true) {
				ch = rdr.readNoWhitespace();
				var d = StreamUtils.toDigit(ch);
				if (!d.HasValue)
					break;
				x *= 10;
				x += d.Value;
			}
			intValue = x;
			next = ch;
		}

		private string readStr(char fst) {
			expect('"', fst);
			return readStrBody();
		}

		private string readStrBody() {
			var s = new StringBuilder();
			while (true) {
				var ch = rdr.readNoWhitespace();
				switch (ch) {
					case '\\': {
						var escaped = rdr.readNoWhitespace();
						switch (escaped) {
							case 'n': s.Append('\n'); break;
							case 't': s.Append('\t'); break;
							case '"': s.Append('"'); break;
							default: throw TODO();
						}
						break;
					}
					case '"':
						return s.ToString();
					default:
						s.Append(ch);
						break;
				}
			}
		}

		private string readKey() {
			var res = readStr();
			expect(':');
			return res;
		}

		void readComma() { expect(','); } //move near readArrayStart

		void expectKey(string key) {
			expect('"');
			foreach (var ch in key)
				expect(ch, rdr.readNoWhitespace());
			expect('"');
			expect(':');
		}

		private Arr<T> readList<T>(char fst, Func<Char, Tuple<T, Char>> readElement) {
			expect('[', fst);

			var ch = rdr.readAndSkipWhitespace();
			if (ch == ']')
				return Arr.empty<T>();

			var res = Arr.builder<T>();
			while (true) {
				var elt = readElement(ch);
				res.add(elt.Item1);
				switch (elt.Item2) {
					case ',':
						ch = rdr.readAndSkipWhitespace();
						break;
					case ']':
						return res.finish();
					default:
						throw showError("',' or ']'", elt.Item2);
				}
			}
		}

		//TODO: some of these shouldn't skip whitespace
		private void expect(char expected) {
			expect(expected, rdr.readAndSkipWhitespace());
		}

		private void expect(char expected, char actual) {
			if (expected != actual)
				throw showError($"'{expected}'", actual);
		}

		private Exception showError(string expected, char actual) =>
			new Exception($"At {rdr.pos}: Expected {expected}, got '{actual}'");
	}

	static class TryTryAgain {
		public static void go() {
			var port = 8124;
			var localAddr = IPAddress.Parse("127.0.0.1");
			var server = new TcpListener(localAddr, port);

			server.Start(); // starts listening

			Console.WriteLine("waiting for connection...");

			var client = server.AcceptTcpClient();

			Console.WriteLine("Connected!");

			var stream = client.GetStream(); //This is a two-way stream. dispose me

			var reader = new StreamReader(stream);
			var writer = new StreamWriter(stream);

			while (true) {
				var line = reader.ReadLine();

				//Can also write a response back using stream.
				Console.WriteLine("READ: " + line);

				writer.WriteLine("Stupid response");
				Console.WriteLine("Sent response");
			}

			//client.Close();
		}

		public static void dumbass() { //I want a server, not a client!
			var host = "localhost";
			var port = 8124;

			var tcpClient = new TcpClient(host, port); //dispose me!

			var stream = tcpClient.GetStream(); //dispose me!

			var reader = new StreamReader(stream); //dispose me!

			var line = reader.ReadLine();

			Console.WriteLine(line);
			throw TODO();

			//var data = new Byte[256];
			//var bytes = stream.Read(data, 0, data.Length);
		}
	}

	/*class TestItOut {
		static object _svc;
		internal static void go() {
			_svc = new MyService();

			var rpcResultHandler = new AsyncCallback(state => {
				var async = (JsonRpcStateAsync) state;
				var result = async.Result;
				var writer = (StreamWriter) async.AsyncState;
				writer.WriteLine(result);
				writer.FlushAsync();
			});

			SocketListener.start(8124, (writer, line) => {
				var async = new JsonRpcStateAsync(rpcResultHandler, writer) { JsonRpc = line };
				JsonRpcProcessor.Process(async, writer);
			});
		}
	}

	static class SocketListener {
		internal static void start(uint listenPort, Action<StreamWriter, string> handleRequest) {
			var server = new TcpListener(IPAddress.Parse("127.0.0.1"), signed(listenPort));
			server.Start();
			//while (true) {
			using (var client = server.AcceptTcpClient())
			using (var stream = client.GetStream()) {
				Console.WriteLine("Client connected...");
				var reader = new StreamReader(stream, Encoding.UTF8);
				var writer = new StreamWriter(stream, new UTF8Encoding(false));

				while (!reader.EndOfStream) {
					var line = reader.ReadLine();
					handleRequest(writer, line);
					Console.WriteLine($"REQUEST: {line}");
				}
			}
			//}
		}
	}

	//TODO: not public
	public sealed class MyService : JsonRpcService {
		[JsonRpcMethod]
		void initialized() {
			Console.WriteLine("INITIALIZED!");
		}

		[JsonRpcMethod] // handles JsonRpc like : {'method':'incr','params':[5],'id':1}
		int incr(int i) { return i + 1; }

		[JsonRpcMethod] // handles JsonRpc like : {'method':'decr','params':[5],'id':1}
		int decr(int i) { return i - 1; }
	}*/

	/*sealed class JsonRpcClient {
		readonly IPEndPoint serviceEndPoint;
		readonly Encoding encoding;

		internal JsonRpcClient(IPEndPoint serviceEndpoint, Encoding encoding) {
			this.serviceEndPoint = serviceEndpoint;
			this.encoding = encoding;
		}
	}*/

	struct JsonParser {
		JsonScanner j;
		internal JsonParser(JsonScanner j) { this.j = j; }



		Position readPositionEntry() {
			j.readDictKey("position");
			j.readDictStart();
			var line = j.readDictUintEntry("line");
			var character = j.readDictUintEntry("character", last: true);
			return new Position(line, character);
		}
	}

	interface Logger {
		void received(string message);
		void sent(string message);
	}
}

namespace Lsp {
	struct Initialize {
		internal readonly uint rqId;
		internal readonly uint processId;
		internal readonly Op<string> rootPath;
		internal readonly Op<string> rootUri;
		internal readonly string trace;
		internal Initialize(uint rqId, uint processId, Op<string> rootPath, Op<string> rootUri, string trace) {
			this.rqId = rqId;
			this.processId = processId;
			this.rootPath = rootPath;
			this.rootUri = rootUri;
			this.trace = trace;
		}
	}

	// 1 word
	struct Position {
		/** 0-indexed */
		internal readonly uint line;
		/** 0-indexed */
		internal readonly uint character;
		internal Position(uint line, uint character) { this.line = line; this.character = character; }
	}

	// 2 words
	struct Range {
		internal readonly Position start;
		internal readonly Position end;
		internal Range(Position start, Position end) { this.start = start; this.end = end; }
	}

	// 3 words
	struct Location {
		internal readonly string uri;
		internal readonly Range range;
		internal Location(string uri, Range range) { this.uri = uri; this.range = range; }
	}

	/** Warning: 7 words in size. Pass by reference. */
	struct Diagnostic {
		internal readonly Range range;
		internal readonly DiagnosticSeverity severity;
		internal readonly string code;
		internal readonly string source;
		internal readonly string message;
		internal Diagnostic(Range range, DiagnosticSeverity severity, string code, string source, string message) {
			this.range = range;
			this.severity = severity;
			this.code = code;
			this.source = source;
			this.message = message;
		}
	}

	enum DiagnosticSeverity {
		None,
		Error = 1,
		Warning = 2,
		Information = 3,
		Hint = 4
	}

	interface Smartness {
		//TODO: This isn't a request!!! So handle it a different way.
		//This should presumably be done after textDocumentDidOpen and textDocumentDidChange.
		//https://github.com/Microsoft/language-server-protocol/blob/master/protocol.md#publishdiagnostics-notification

		IEnumerable<Diagnostic> diagnostics(string uri);

		void textDocumentDidOpen(string uri, string languageId, uint version, string text);
		void textDocumentDidChange(string uri, uint version, string text);
		void textDocumentDidSave(string uri, uint version);

		void goToDefinition(TextDocumentPositionParams pms, out string uri, out Range range);
		IEnumerable<CompletionItem> getCompletion(TextDocumentPositionParams pms);
		string getHover(TextDocumentPositionParams pms);
		IEnumerable<DocumentHighlight> getDocumentHighlights(TextDocumentPositionParams pms);
		IEnumerable<Location> findAllReferences(TextDocumentPositionParams pms, bool includeDeclaration);
		SignatureHelpResponse signatureHelp(TextDocumentPositionParams pms);
	}

	struct DocumentHighlight {
		internal readonly Range range;
		internal readonly Kind kind;
		internal DocumentHighlight(Range range, Kind kind) { this.range = range; this.kind = kind; }

		internal enum Kind {
			Text = 1,
			Read = 2,
			Write = 3,
		}
	}

	struct TextDocumentPositionParams {
		internal readonly string textDocumentUri;
		internal readonly Position position;
		internal TextDocumentPositionParams(string textDocumentUri, Position position) {
			this.textDocumentUri = textDocumentUri;
			this.position = position;
		}
	}

	//TODO: more fields available (all optional)
	struct CompletionItem {
		internal readonly string label;
		CompletionItem(string label) { this.label = label; }
	}

	struct SignatureHelpResponse {
		internal readonly IEnumerable<SignatureInformation> signatures;
		internal readonly uint? activeSignature;
		internal readonly uint? activeParameter;
		internal SignatureHelpResponse(IEnumerable<SignatureInformation> signatures, uint? activeSignature, uint? activeParameter) {
			this.signatures = signatures;
			this.activeSignature = activeSignature;
			this.activeParameter = activeParameter;
		}
	}
	struct SignatureInformation {
		internal readonly string label;
		internal readonly Op<string> documentation;
		internal readonly IEnumerable<ParameterInformation> parameters;
		internal SignatureInformation(string label, Op<string> documentation, IEnumerable<ParameterInformation> parameters) {
			this.label = label;
			this.documentation = documentation;
			this.parameters = parameters;
		}
	}
	struct ParameterInformation {
		internal readonly string label;
		internal readonly Op<string> documentation;
		internal ParameterInformation(string label, Op<string> documentation) { this.label = label; this.documentation = documentation; }
	}
}
