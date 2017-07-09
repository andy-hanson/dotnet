"use strict";
module.exports = class ConsoleApp {
	static async main(console) {
		return await console["write-line"]("Hello world!");
	}
};
