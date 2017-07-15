/**
 * @param {Function[]} classes
 * @return {Function}
*/
module.exports = (...classes) => {
	const protos = classes.map(cls => cls.prototype);
	function mixed() {}
	mixed.prototype = mixProto(protos);
	return mixed;
};

/**
 * @param {Object[]} protos
 * @return {Object}
 */
function mixProto(protos) {
	const t = { cache: null, protos };
	const p = new Proxy(t, protoProxyHandler);
	const cache = Object.create(p);
	t.cache = cache;
	return cache;
}

/** @type {ProxyHandler<Object>} */
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
