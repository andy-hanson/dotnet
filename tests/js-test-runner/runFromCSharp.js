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
const { readSync } = require("fs"); //this is unused, wtf?

const { runBaseline } = require("./runBaseline");
const { readStdinSync } = require("./utils");

const bufferSize = 64;
const buff = new Buffer(bufferSize);

while (true) {
	const input = readStdinSync(/*bufferSize*/ 128);
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
