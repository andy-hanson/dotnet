const { readSync } = require("fs");

// @ts-ignore
const stdinFd = process.stdin.fd;

/**
 * @param {number} bufferSize
 * @return {string | undefined}
 */
exports.readStdinSync = (bufferSize) => {
	const buff = new Buffer(bufferSize);

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

