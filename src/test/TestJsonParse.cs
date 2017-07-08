using Json;
using Lsp;

using static Test.Assert;

#pragma warning disable CC0068 // Use me!
namespace Test {
	static class JsonParseTest {
		static void parse() {
			var source = @"{
				'jsonrpc': '2.0',
				'id': 0,
				'method': 'initialize',
				'params': {
				'processId': 27882,
				'rootPath': null,
				'rootUri': null,
				'capabilities': {
					'workspace': {
					'applyEdit': true,
					'didChangeConfiguration': {
						'dynamicRegistration': false
					},
					'didChangeWatchedFiles': {
						'dynamicRegistration': false
					},
					'symbol': {
						'dynamicRegistration': true
					},
					'executeCommand': {
						'dynamicRegistration': true
					}
					},
					'textDocument': {
					'synchronization': {
						'dynamicRegistration': true,
						'willSave': true,
						'willSaveWaitUntil': true,
						'didSave': true
					},
					'completion': {
						'dynamicRegistration': true,
						'completionItem': {
						'snippetSupport': true
						}
					},
					'hover': {
						'dynamicRegistration': true
					},
					'signatureHelp': {
						'dynamicRegistration': true
					},
					'references': {
						'dynamicRegistration': true
					},
					'documentHighlight': {
						'dynamicRegistration': true
					},
					'documentSymbol': {
						'dynamicRegistration': true
					},
					'formatting': {
						'dynamicRegistration': true
					},
					'rangeFormatting': {
						'dynamicRegistration': true
					},
					'onTypeFormatting': {
						'dynamicRegistration': true
					},
					'definition': {
						'dynamicRegistration': true
					},
					'codeAction': {
						'dynamicRegistration': true
					},
					'codeLens': {
						'dynamicRegistration': true
					},
					'documentLink': {
						'dynamicRegistration': true
					},
					'rename': {
						'dynamicRegistration': true
					}
					}
				},
				'trace': 'off'
				}
			}".Replace('\'', '"');
			var expected = new Initialize(
				rqId: 0,
				processId: 27882,
				rootPath: Op<string>.None,
				rootUri: Op<string>.None,
				trace: "off");
			var parsed = JsonParser.parseInitialize(source);
			mustEqual(expected, parsed);
		}
	}
}
