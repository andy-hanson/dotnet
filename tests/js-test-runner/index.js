/*
This file is meant to be consumed by the C# test runner.
It receives test names on stdin and returns results on stdout.
*/

// @ts-check

const assert = require("assert");
const { readSync } = require("fs");

const runBaseline = require("./runBaseline");

/*
Receives commands to run certain modules.
*/
process.stdin.resume();
const maxNameLength = 64;
const buff = new Buffer(maxNameLength)
// @ts-ignore
const stdinFd = process.stdin.fd;

while (true) {
	const bytesRead = readSync(stdinFd, buff, 0, maxNameLength, null);
	if (bytesRead === 0) break;
	if (bytesRead === maxNameLength) throw new Error(); // Must increase maxNameLength

	assert(buff[bytesRead] === '\n'.charCodeAt(0));
	const testName = buff.toString("utf-8", 0, bytesRead - 1);
	try {
		const output = runBaseline(testName);
		console.log(output === undefined ? "" : output);
	} catch (error) {
		console.log(JSON.stringify(error.stack));
	}
}
