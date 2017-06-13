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
				var response = getResponse(inputMessage, out var shutDown);
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
		protected abstract Op<string> getResponse(string input, out bool shutDown);

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
			var sentMsg = getResponseText(pms.rqId, new InitResponse());
			logger.each(l => l.sent(sentMsg));
			send(sentMsg);
		}

		protected override Op<string> getResponse(string inputMessage, out bool shutDown) {
			logger.each(l => l.received(inputMessage));
			var message = JsonParser.parseMessage(inputMessage);

			var sd = message as LspMethod.Shutdown;
			if (sd != null) {
				shutDown = true;
				return Op<string>.None;
			}

			shutDown = false;

			var nt = message as LspMethod.Notification;
			if (nt != null) {
				handleNotification(nt);
				return Op<string>.None;
			}

			var rq = message as LspMethod.Request;
			if (rq != null) {
				var response = getRequestResponse(rq);
				return Op.Some(getResponseText(rq.id, response));
			}

			throw unreachable();
		}

		void handleNotification(LspMethod.Notification n) {
			if (n == LspMethod.Notification.Initialized)
				return; // OK
			if (n == LspMethod.Notification.Ignore)
				return; // OK
			if (n == LspMethod.Notification.DidChangeConfiguration)
				return; // TODO

			var didOpen = n as LspMethod.Notification.TextDocumentDidOpen;
			if (didOpen != null) {
				smartness.textDocumentDidOpen(didOpen.uri, didOpen.languageId, didOpen.version, didOpen.text);
			}

			var didChange = n as LspMethod.Notification.TextDocumentDidChange;
			if (didChange != null) {
				smartness.textDocumentDidChange(didChange.uri, didChange.version, didChange.text);
				//TODO: don't do this here...
				var diagnostics = smartness.diagnostics(didChange.uri);
				notifyServer("testDocument/publishDiagnostics", new PublishDiagnostics(didChange.uri, diagnostics));
			}
		}

		void notifyServer(string methodName, ToJson body) {
			var msg = JsonWriter.write(j => j.writeDict("jsonrpc", "2.0", "method", methodName, "params", body));
			logger.each(l => l.sent(msg));
			send(msg);
		}

		Response getRequestResponse(LspMethod.Request rq) {
			throw TODO();
		}

		string getResponseText(uint requestId, ToJson result) => JsonWriter.write(j => j.writeDict("jsonrpc", "2.0", "id", requestId, "result", result));
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
