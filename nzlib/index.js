class Exception extends Error {
	constructor() {
		super();
		this.message = this.description();
	}
}
exports.Exception = Exception;

class AssertionException extends Exception {
	description() {
		return "Assertion failed.";
	}
}
exports.AssertionException = AssertionException;

exports.divInt = (a, b) => {
	return ((a | 0) / (b | 0)) | 0;
}

exports.parseInt = (s) => { throw new Error("TODO"); }
exports.parseFloat = (s) => { throw new Error("TODO"); }

exports.mixin = (...classes) => {
	const protos = classes.map(cls => cls.prototype);
	function mixed() {}
	mixed.prototype = mixProto(protos);
	return mixed;
};

function mixProto(protos) {
	const t = { cache: null, protos };
	const p = new Proxy(t, protoProxyHandler);
	const cache = Object.create(p);
	t.cache = cache;
	return cache;
}

const protoProxyHandler = {
	get(target, name) {
		const { cache, protos } = target;
		for (const proto of protos) {
			const got = proto[name];
			if (got !== undefined) {
				//Object.defineProperty(cache, name, { configurable: false, enumerable: false, writable: false, value: got });
				cache[name] = got;
				return got;
			}
		}
		throw new Error(`No such method ${JSON.stringify(name)}`);
	},
};
