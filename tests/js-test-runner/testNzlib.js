const assert = require("assert");
// @ts-ignore
const nzlib = require("nzlib");
const { handleSingleLineFromStdin } = require("./utils");

if (module.parent === null)
	main();

/** @typedef {{ [key: string]: ReadonlyArray<string> }} Primitives */
/** @typedef {{ [key: string]: { statics: ReadonlyArray<string>, instance: ReadonlyArray<string> } }} Classes */
/** @typedef {{ primitives: Primitives, classes: Classes, other: ReadonlyArray<string> }} J */

function main() {
	handleSingleLineFromStdin(str => {
		try {
			check(str);
			console.log("OK");
		} catch (error) {
			console.log(error.stack ? JSON.stringify(error.stack) : `ERROR: ${JSON.stringify(error)}`);
		}
	});
}

/**
 * @param {Set<string>} set
 * @param {string} key
 * @param {function(): string} msg
 * @return {void}
 */
function mustRemove(set, key, msg) {
	const didDelete = set.delete(key);
	if (!didDelete) {
		throw new Error(msg());
	}
}

/** @param {string} str */
function check(str) {
	/** @type {J} */
	const j = JSON.parse(str);

	/** @type {string[]} */ //TODO: Array<keyof J>
	const expectedKeys = ["primitives", "classes", "other"]
	assert.deepEqual(Object.keys(j), expectedKeys);

	const nzlibRemaining = new Set(Object.keys(nzlib));

	checkPrimitives(j.primitives, markSeen);
	checkClasses(j.classes, markSeen);
	checkOther(j.other, markSeen);

	for (const s of nzlibRemaining)
		throw new Error(`Nzlib provides unused ${s}`);

	/**
	 * @param {string} name
	 * @return {void}
	 */
	function markSeen(name) {
		mustRemove(nzlibRemaining, name, () => `nzlib does not provide ${name}`);
	}
}

/**
 * @param {Primitives} primitives
 * @param {function(string): void} markSeen
 * @return {void}
 */
function checkPrimitives(primitives, markSeen) {
	for (const [pname, primitiveMethodsList] of Object.entries(primitives)) {
		markSeen(pname);

		const prim = nzlib[pname];
		const expecteds = new Set(primitiveMethodsList);

		for (const [actualName, actualValue] of Object.entries(prim)) {
			if (typeof actualValue !== "function") {
				throw new Error(`Expected primitive operation implementation to be a function, got ${typeof actualValue}: ${actualValue}`);
			}
			mustRemove(expecteds, actualName, () => `Nzlib provides an unused function ${pname}.${actualName}.`);
		}

		for (const e of expecteds)
			throw new Error(`Nzlib does not provide a function ${pname}.${e}`);
	}
}

/**
 * @param {Classes} classes
 * @param {function(string): void} markSeen
 * @return {void}
 */
function checkClasses(classes, markSeen) {
	const classDefaultProperties = new Set(["length", "prototype", "name"]);

	for (const [cname, cobj] of Object.entries(classes)) {
		markSeen(cname);

		assert.deepEqual(Object.keys(cobj), ["statics", "instance"]);
		const expectedStatics = new Set(cobj.statics);
		const expectedInstance = new Set(cobj.instance);

		const cls = nzlib[cname];
		assert.equal(cls.name, cname);
		assert(Object.getPrototypeOf(cls.prototype) !== Object.prototype, `Avoid inheriting Object.prototype`);

		for (const staticName of Object.getOwnPropertyNames(cls)) {
			const staticMethod = cls[staticName];

			if (classDefaultProperties.has(staticName) && typeof staticMethod !== "function")
				// These are default properties of all classes, ignroe.
				continue;

			if (typeof staticMethod !== "function")
				throw new Error(`Expected ${cname}.${staticName} to be a function, got a ${typeof staticMethod}: ${staticMethod}`);

			mustRemove(expectedStatics, staticName, () => `Nzlib provides an unused static method ${cname}.${staticName}`);
		}

		for (const e of expectedStatics)
			throw new Error(`Nzlib does not provide static method ${cname}.${e}`);

		for (const instanceName of Object.getOwnPropertyNames(cls.prototype)) {
			if (instanceName === "constructor")
				continue;

			const instanceMethod = cls.prototype[instanceName];
			if (typeof instanceMethod !== "function")
				throw new Error(`Expected ${cname}.prototype.${instanceName} to be a function, got a ${typeof instanceMethod}: ${instanceMethod}`);

			mustRemove(expectedInstance, instanceName, () => `Nzlib provides an unused instance method ${cname}.${instanceName}`);
		}

		for (const e of expectedInstance)
			throw new Error(`Nzlib does not provide instance method ${cname}.${e}`);
	}
}

/**
 * @param {ReadonlyArray<string>} other
 * @param {function(string): void} markSeen
 */
function checkOther(other, markSeen) {
	for (const oname of other) {
		markSeen(oname);
		const o = nzlib[oname];
		assert.equal(typeof o, "function");
	}
}
