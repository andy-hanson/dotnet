/*
This file is meant to be consumed by the C# test runner.
It receives test names on stdin and returns results on stdout.
E.g.:
	> normal Foo
	< OK
	> special Bar
	< Error: ...
*/

// @ts-check

const assert = require("assert");
const { readSync } = require("fs");

const { runBaseline } = require("./runBaseline");

/*
Receives commands to run certain modules.
*/
process.stdin.resume();
const bufferSize = 64;
const buff = new Buffer(bufferSize)
// @ts-ignore
const stdinFd = process.stdin.fd;

while (true) {
	const input = readStdinSync();
	if (input === undefined) break;

	const parts = input.split(" ");
	assert(parts.length == 2);
	const [kind, testPath] = parts;
	if (kind !== "normal" && kind !== "special")
		throw new Error(kind);
	try {
		runBaseline(kind, testPath);
		console.log("OK");
	} catch (error) {
		console.log(error.stack ? JSON.stringify(error.stack) : `ERROR: ${JSON.stringify(error)}`);
	}
}

/**
 * @return {string | undefined}
 */
function readStdinSync() {
	let bytesRead = 0;
	while (true) {
		try {
			bytesRead = readSync(stdinFd, buff, 0, bufferSize, null);
			break;
		} catch (e) {
			if (e.code === "EAGAIN") {
				continue;
			}
			throw e;
		}
	}

	if (bytesRead === 0) return undefined;
	if (bytesRead === bufferSize) throw new Error(); // bufferSize too small
	if (buff[bytesRead - 1] !== '\n'.charCodeAt(0)) {
		throw new Error(buff.toString("utf-8", 0, bytesRead));
	}
	return buff.toString("utf-8", 0, bytesRead - 1);
}
