using System;

namespace BuiltinAttributes {
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
	/** Attribute for types that don't need to be imported, such as 'Bool'. */
	sealed class NoImportAttribute : Attribute {}

	// Note that constructors are always hidden, so don't need this attribute.
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Field)]
	sealed class HidAttribute : Attribute {}

	// Shorthand for setting self and all parameters Pure.
	[AttributeUsage(AttributeTargets.Method)]
	sealed class AllPureAttribute : Attribute {}

	abstract class EffectLikeAttribute : Attribute {
		internal abstract Model.Effect effect { get; }
	}

	[AttributeUsage(AttributeTargets.Method)]
	abstract class SelfEffectAttribute : EffectLikeAttribute {}
	sealed class SelfPureAttribute : SelfEffectAttribute {
		internal override Model.Effect effect => Model.Effect.pure;
	}
	sealed class SelfGetAttribute : SelfEffectAttribute {
		internal override Model.Effect effect => Model.Effect.get;
	}
	sealed class SelfSetAttribute : SelfEffectAttribute {
		internal override Model.Effect effect => Model.Effect.set;
	}
	sealed class SelfIoAttribute : SelfEffectAttribute {
		internal override Model.Effect effect => Model.Effect.io;
	}

	[AttributeUsage(AttributeTargets.Method)]
	abstract class ReturnEffectAttribute : EffectLikeAttribute {}
	sealed class ReturnPureAttribute : ReturnEffectAttribute {
		internal override Model.Effect effect => Model.Effect.pure;
	}
	sealed class ReturnGetAttribute : ReturnEffectAttribute {
		internal override Model.Effect effect => Model.Effect.get;
	}
	sealed class ReturnSetAttribute : ReturnEffectAttribute {
		internal override Model.Effect effect => Model.Effect.set;
	}
	sealed class ReturnIoAttribute : ReturnEffectAttribute {
		internal override Model.Effect effect => Model.Effect.io;
	}

	[AttributeUsage(AttributeTargets.Parameter)]
	abstract class ParameterEffectAttribute : EffectLikeAttribute {}
	sealed class PureAttribute : ParameterEffectAttribute {
		internal override Model.Effect effect => Model.Effect.pure;
	}
	sealed class GetAttribute : ParameterEffectAttribute {
		internal override Model.Effect effect => Model.Effect.get;
	}
	sealed class SetAttribute : ParameterEffectAttribute {
		internal override Model.Effect effect => Model.Effect.set;
	}
	sealed class IoAttribute : ParameterEffectAttribute {
		internal override Model.Effect effect => Model.Effect.io;
	}


	[AttributeUsage(AttributeTargets.Method)]
	sealed class InstanceAttribute : Attribute {}

	[AttributeUsage(AttributeTargets.Class)]
	sealed class HidSuperClassAttribute : Attribute {}

	/**
	Attribute for types that are implemented by JS primitives, not by a JS class.
	For all other classes, there should be an equivalent version in nzlib.

	Methods in a JSPrimitive class will be implemented by functions in `nzlib/primitive`, or have a JsTranslate annotation for special handling.
	*/
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
	sealed class JsPrimitiveAttribute : Attribute {}

	[AttributeUsage(AttributeTargets.Method)]
	abstract class AnyJsTranslateAttribute : Attribute {}

	sealed class JsSpecialTranslateAttribute : AnyJsTranslateAttribute {
		internal readonly string builtinMethodName; // Name of a method in JsBuiltins
		internal JsSpecialTranslateAttribute(string builtinMethodName) { this.builtinMethodName = builtinMethodName; }
	}

	sealed class JsBinaryAttribute : AnyJsTranslateAttribute {
		internal readonly string @operator;
		internal JsBinaryAttribute(string @operator) { this.@operator = @operator; }
	}
}
