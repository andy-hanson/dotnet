using System;
using System.Net.Sockets;
using System.IO;
using System.Net;

using Json;
using static Utils;

namespace Lsp.Server {
	abstract class AbstractTcpServer : IDisposable {
		internal readonly uint port;
		readonly TcpListener server;
		readonly TcpClient client;
		readonly NetworkStream stream; //TODO: BufferedStream

		internal AbstractTcpServer(uint port) {
			this.port = port;

			var localAddr = IPAddress.Parse("127.0.0.1");
			server = new TcpListener(localAddr, signed(port));

			server.Start();

			Console.WriteLine("waiting for connection...");

			client = server.AcceptTcpClient();

			Console.WriteLine("Connected!");

			stream = client.GetStream();
		}

		internal void loop() {
			init();
			while (true) {
				var inputMessage = read();
				var (response, shutDown) = getResponse(inputMessage);
				if (shutDown) {
					assert(!response.has);
					break;
				}

				if (response.get(out var r))
					send(r);
			}
		}

		protected string read() => TcpUtils.readTcpString(stream);

		protected void send(string s) => TcpUtils.writeTcpString(stream, s);

		protected abstract void init();
		protected abstract (Op<string> response, bool shutDown) getResponse(string input);

		void IDisposable.Dispose() {
			stream.Close();
			client.Close();
			server.Stop();
		}
	}

	sealed class LspServer : AbstractTcpServer {
		readonly Op<Logger> logger;
		readonly LspImplementation smartness;

		internal LspServer(uint port, Op<Logger> logger, LspImplementation smartness) : base(port) {
			this.logger = logger;
			this.smartness = smartness;
		}

		protected override void init() {
			var msg = read();
			logger.each(l => l.received(msg));

			var pms = JsonParser.parseInitialize(msg);

			// "interface ServerCapabilities" in https://github.com/Microsoft/language-server-protocol/blob/master/protocol.md#initialize
			var sentMsg = getResponseText(pms.rqId, new InitResponse(true));
			logger.each(l => l.sent(sentMsg));
			send(sentMsg);
		}

		protected override (Op<string> response, bool shutDown) getResponse(string inputMessage) {
			logger.each(l => l.received(inputMessage));
			var message = JsonParser.parseMessage(inputMessage);
			switch (message) {
				case LspMethod.Shutdown sd:
					return (Op<string>.None, shutDown: true);
				case LspMethod.Notification nt:
					handleNotification(nt);
					return (Op<string>.None, shutDown: false);
				case LspMethod.Request rq:
					var response = getRequestResponse(rq);
					return (Op.Some(getResponseText(rq.id, response)), shutDown: false);
				default:
					throw unreachable();
			}
		}

		void handleNotification(LspMethod.Notification n) {
			if (n == LspMethod.Notification.Initialized)
				return; // OK
			if (n == LspMethod.Notification.Ignore)
				return; // OK
			if (n == LspMethod.Notification.DidChangeConfiguration)
				return; // TODO

			switch (n) {
				case LspMethod.Notification.TextDocumentDidOpen didOpen:
					smartness.textDocumentDidOpen(didOpen.uri, didOpen.languageId, didOpen.version, didOpen.text);
					return;
				case LspMethod.Notification.TextDocumentDidChange didChange:
					smartness.textDocumentDidChange(didChange.uri, didChange.version, didChange.text);
					//TODO: don't do this here...
					var diagnostics = smartness.diagnostics(didChange.uri);
					notifyServer("testDocument/publishDiagnostics", new PublishDiagnostics(didChange.uri, diagnostics));
					return;
				default:
					throw TODO();
			}
		}

		void notifyServer<T>(string methodName, T body) where T : ToData<T> {
			var msg = JsonWriter.write(new NotifyWrapper<T>(methodName, body));
			logger.each(l => l.sent(msg));
			send(msg);
		}

		Response getRequestResponse(LspMethod.Request rq) {
			throw TODO();
		}

		string getResponseText<T>(uint requestId, T result) where T : ToData<T> => JsonWriter.write(new ResponseWrapper<T>(requestId, result));
	}

	struct NotifyWrapper<T> : ToData<NotifyWrapper<T>> where T : ToData<T> {
		readonly string jsonrpc;
		readonly string methodName;
		readonly T @params;

		internal NotifyWrapper(string methodName, T @params) {
			this.jsonrpc = "2.0";
			this.methodName = methodName;
			this.@params = @params;
		}

		public bool Equals(NotifyWrapper<T> n) => throw TODO();
		public Dat toDat() => Dat.of(this, nameof(jsonrpc), Dat.str(jsonrpc), nameof(methodName), Dat.str(methodName), nameof(@params), @params);
	}

	//mv
	struct ResponseWrapper<T> : ToData<ResponseWrapper<T>> where T : ToData<T> {
		readonly string jsonrpc;
		readonly uint id;
		readonly T result;

		internal ResponseWrapper(uint id, T result) {
			this.jsonrpc = "2.0";
			this.id = id;
			this.result = result;
		}

		public bool Equals(ResponseWrapper<T> r) => throw TODO();
		public Dat toDat() => Dat.of(this, nameof(jsonrpc), Dat.str(jsonrpc), nameof(id), Dat.num(id), nameof(result), result);
	}

	interface Logger {
		void received(string message);
		void sent(string message);
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
		if (!Json.JsonScanner.toDigit(fst, out var res))
			throw new Exception($"Expected a digit, got: {fst}");

		while (true) {
			var ch = (char) stream.ReadByte();
			if (!Json.JsonScanner.toDigit(ch, out var d)) {
				readNewline(stream, ch);
				return res;
			}
			res *= 10;
			res += d;
		}
	}

	internal static void readNewline(Stream stream, int ch) {
		var ch2 = ch == '\r' ? stream.ReadByte() : ch;
		assert(ch2 == '\n');
	}
}
