"use strict";
module.exports = class Console_App_User {
	static async main(app) {
		return await await app.stdout().write_line("Hello world!");
	}
};
