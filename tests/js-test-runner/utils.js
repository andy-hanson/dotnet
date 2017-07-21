const { execSync } = require("child_process");
const { readSync } = require("fs");
const readline = require("readline");

// @ts-ignore
const stdinFd = process.stdin.fd;

/**
 * @param {function(string): void} cb
 * @return {void}
 */
exports.onLineFromStdin = (cb) => {
	const rl = readline.createInterface({ input: process.stdin });
	rl.on("line", cb);
};

/**
 * @param {function(string): void} cb
 * @return {void}
 */
exports.handleSingleLineFromStdin = cb => {
	const rl = readline.createInterface({ input: process.stdin });
	rl.on("line", line => {
		cb(line);
		process.exit(0);
	});
};
