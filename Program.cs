using System;
using System.Runtime.Loader;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using static Utils;
//dotnet add package System.Runtime
//dotnet add package System.Runtime.Loader
//dotnet add package System.Reflection.Emit
//dotnet add package System.Collections.Immutable

namespace dotnet
{
    class Program
    {
        static void AddCtr(TypeBuilder tb, FieldBuilder fbNumber) {
            Type[] parameterTypes = { typeof(int) };
            var ctor1 = tb.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                parameterTypes);
            var ctor1IL = ctor1.GetILGenerator();
            ctor1IL.Emit(OpCodes.Ldarg_0);
            ctor1IL.Emit(OpCodes.Call,
                typeof(object).GetConstructor(Type.EmptyTypes));
            ctor1IL.Emit(OpCodes.Ldarg_0);
            ctor1IL.Emit(OpCodes.Ldarg_1);
            ctor1IL.Emit(OpCodes.Stfld, fbNumber);
            ctor1IL.Emit(OpCodes.Ret);

            var ctor0 = tb.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                Type.EmptyTypes);

            ILGenerator ctor0IL = ctor0.GetILGenerator();
            // For a constructor, argument zero is a reference to the new
            // instance. Push it on the stack before pushing the default
            // value on the stack, then call constructor ctor1.
            ctor0IL.Emit(OpCodes.Ldarg_0);
            ctor0IL.Emit(OpCodes.Ldc_I4_S, 42);
            ctor0IL.Emit(OpCodes.Call, ctor1);
            ctor0IL.Emit(OpCodes.Ret);
        }

        static void Main(string[] args)
        {
            var aName = new AssemblyName("Example");
            var ab = AssemblyBuilder.DefineDynamicAssembly(aName, AssemblyBuilderAccess.RunAndCollect);
            var mb = ab.DefineDynamicModule(aName.Name);

            //var m = new Model.Module();

            //Model.Emit.writeBytecode(mb, klass, );
        }

        static void Test()
        {
            var aName = new AssemblyName("Example");
            //var ab = AppDomain.CurrentDomain.DefineDynamicAssembly(
            //    aName,
            //    AssemblyBuilderAccess.RunAndSave);

            var ab = AssemblyBuilder.DefineDynamicAssembly(aName, AssemblyBuilderAccess.RunAndCollect);
            var mb = ab.DefineDynamicModule(aName.Name);
            var tb = mb.DefineType("MyType", TypeAttributes.Public);
            var fbNumber = tb.DefineField("num", typeof(int), FieldAttributes.Private);

            AddCtr(tb, fbNumber);

            var pbNumber = tb.DefineProperty(
                "Number",
                PropertyAttributes.HasDefault,
                typeof(int),
                null);

            var getSetAttr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
            var mbNumberGetAccessor = tb.DefineMethod(
                "get_Number",
                getSetAttr,
                typeof(int),
                Type.EmptyTypes);

            ILGenerator numberGetIL = mbNumberGetAccessor.GetILGenerator();
            // For an instance property, argument zero is the instance. Load the
            // instance, then load the private field and return, leaving the
            // field value on the stack.
            numberGetIL.Emit(OpCodes.Ldarg_0);
            numberGetIL.Emit(OpCodes.Ldfld, fbNumber);
            numberGetIL.Emit(OpCodes.Ret);

            // Define the "set" accessor method for Number, which has no return
            // type and takes one argument of type int (Int32).
            MethodBuilder mbNumberSetAccessor = tb.DefineMethod(
                "set_Number",
                getSetAttr,
                null,
                new Type[] { typeof(int) });

            ILGenerator numberSetIL = mbNumberSetAccessor.GetILGenerator();
            // Load the instance and then the numeric argument, then store the
            // argument in the field.
            numberSetIL.Emit(OpCodes.Ldarg_0);
            numberSetIL.Emit(OpCodes.Ldarg_1);
            numberSetIL.Emit(OpCodes.Stfld, fbNumber);
            numberSetIL.Emit(OpCodes.Ret);

            // Last, map the "get" and "set" accessor methods to the
            // PropertyBuilder. The property is now complete.
            pbNumber.SetGetMethod(mbNumberGetAccessor);
            pbNumber.SetSetMethod(mbNumberSetAccessor);

            // Define a method that accepts an integer argument and returns
            // the product of that integer and the private field m_number. This
            // time, the array of parameter types is created on the fly.
            MethodBuilder meth = tb.DefineMethod(
                "MyMethod",
                MethodAttributes.Public,
                typeof(int),
                new Type[] { typeof(int) });

            ILGenerator methIL = meth.GetILGenerator();
            // To retrieve the private instance field, load the instance it
            // belongs to (argument zero). After loading the field, load the
            // argument one and then multiply. Return from the method with
            // the return value (the product of the two numbers) on the
            // execution stack.
            methIL.Emit(OpCodes.Ldarg_0);
            methIL.Emit(OpCodes.Ldfld, fbNumber);
            methIL.Emit(OpCodes.Ldarg_1);
            methIL.Emit(OpCodes.Mul);
            methIL.Emit(OpCodes.Ret);

            var t = tb.CreateTypeInfo();

            //ab.Save(aName.Name + ".dll");

            MethodInfo mi = t.GetMethod("MyMethod");
            PropertyInfo pi = t.GetProperty("Number");

            object o1 = Activator.CreateInstance(t.AsType());

            Console.WriteLine(pi.GetValue(o1, null));

            //AssemblyLoadContext c = AssemblyLoadContext.Default;
            //Assembly asm = c.LoadFromStream(ms);
            //Type t = asm.GetType("MyType");
        }
    }
}

static class Utils {
    public static T nonNull<T>(T t) {
        Debug.Assert(t != null);
        return t;
    }

    public static ImmutableArray<T> build<T>(Action<ImmutableArray<T>.Builder> builder) {
        var b = ImmutableArray.CreateBuilder<T>();
        builder(b);
        return b.ToImmutable();
    }

    /*class Builder<T> {
        List<T> _build = new List<T>();
        public void add(T t) {
            _build.Add(t);
        }
        internal ImmutableArray<T> finish() => ImmutableArray.CreateBuilder();
    }*/
}


namespace Model
{
    //TODO: its own file
    sealed class Path : IEquatable<Path> {
        private ImmutableArray<Sym> parts;

        public Path(ImmutableArray<Sym> parts) {
            this.parts = parts;
        }

        static Path empty = new Path(ImmutableArray.Create<Sym>());

        static Path resolveWithRoot(Path root, Path path) =>
            root.resolve(new RelPath(0, path));

        public Path from(params string[] elements) =>
            new Path(Arr.fromMappedArray(elements, Sym.of));

        public Path resolve(RelPath rel) {
            var nPartsToKeep = parts.Length - rel.nParents;
            if (nPartsToKeep < 0)
                throw new Exception($"Can't resolve: {rel}\nRelative to: {this}");
            var parent = parts.Slice(0, nPartsToKeep);
            return new Path(parent.Concat(rel.relToParent.parts));
        }

        public Path add(Sym next) => new Path(Arr.rcons(parts, next));

        public Path parent() => new Path(parts.rtail());

        public Sym last => parts.Last();

        public Path addExtension(string extension) {
            var b = parts.ToBuilder();
            b[parts.Length - 1] = Sym.of(parts[parts.Length - 1].str + extension);
            return new Path(b.ToImmutable());
        }

        public bool isEmpty => parts.IsEmpty;

        public Path directory() => new Path(parts.rtail());

        //kill?
        public Tuple<Path, Sym> directoryAndBasename() => Tuple.Create(directory(), last);

        public override bool Equals(object other) => other is Path && Equals(other as Path);
        bool IEquatable<Path>.Equals(Path other) => parts.SequenceEqual(other.parts);
        public override int GetHashCode() => parts.GetHashCode();
        public override string ToString() => string.Join("/", parts);
    }

    sealed class RelPath {
        public readonly int nParents;
        public readonly Path relToParent;

        public RelPath(int nParents, Path relToParent) {
            Debug.Assert(nParents > 0);
            this.nParents = nParents;
            this.relToParent = relToParent;
        }

        bool isParentsOnly => relToParent.isEmpty;
        Sym last => relToParent.last;
    }

    static class Arr {
        public static ImmutableArray<U> fromMappedArray<T, U>(T[] inputs, Func<T, U> mapper) {
            var b = ImmutableArray.CreateBuilder<U>(inputs.Length);
            for (var i = 0; i < inputs.Length; i++)
                b[i] = mapper(inputs[i]);
            return b.ToImmutable();
        }

        public static ImmutableArray<T> rcons<T>(ImmutableArray<T> inputs, T next) {
            var b = ImmutableArray.CreateBuilder<T>(inputs.Length + 1);
            for (var i = 0; i < inputs.Length; i++)
                b[i] = inputs[i];
            b[inputs.Length] = next;
            return b.ToImmutable();
        }

        public static ImmutableArray<T> rtail<T>(this ImmutableArray<T> imm) =>
            ImmutableArray.Create(imm, 0, imm.Length - 1);

        //mv
        public static U[] MapToArray<T, U>(this ImmutableArray<T> imm, Func<T, U> mapper) {
            U[] res = new U[imm.Length];
            for (var i = 0; i < imm.Length; i++) {
                res[i] = mapper(imm[i]);
            }
            return res;
        }

        public static ImmutableArray<T> Slice<T>(this ImmutableArray<T> imm, int start, int length) =>
            ImmutableArray.Create(imm, start, length);

        public static ImmutableArray<T> Concat<T>(this ImmutableArray<T> imm, ImmutableArray<T> other) {
            var b = ImmutableArray.CreateBuilder<T>(imm.Length + other.Length);
            for (var i = 0; i < imm.Length; i++)
                b[i] = imm[i];
            for (var i = 0; i < other.Length; i++)
                b[imm.Length + i] = other[i];
            return b.ToImmutable();
        }
    }

    abstract class Ty {
        public abstract Type toType();
    }

    sealed class Sym {
        private static ConcurrentDictionary<string, Sym> table = new ConcurrentDictionary<string, Sym>();
        public static Sym of(string s) => table.GetOrAdd(s, _ => new Sym(s));

        public readonly string str;
        private Sym(string str) {
            this.str = str;
        }
    }

    sealed class Module {

        public readonly string source;

        public Module(Path path, bool isMain, string source) {
            this.source = source;
        }

        private ImmutableArray<Module> _imports;
        public ImmutableArray<Module> imports { get { return nonNull(_imports); } set { _imports = value; } }

        bool importsAreResolved => _imports != null;

        private Klass _klass;
        public Klass klass { get { return nonNull(_klass); } set { _klass = value; } }

        Sym name => klass.name;
    }

    abstract class ClassLike {}

    sealed class Klass {
        public readonly Loc loc;
        public readonly Sym name;

        public Klass(Loc loc, Sym name) {
            this.loc = loc;
            this.name = name;
        }

        Head _head;
        public Head head { get { return nonNull(_head); } set { _head = value; } }

        private ImmutableDictionary<Sym, Member> _membersMap;
        public ImmutableDictionary<Sym, Member> membersMap {
            private get { return nonNull(_membersMap); }
            set { _membersMap = value; }
        }

        public Member this[Sym name] => membersMap[name];

        public IEnumerable<Member> members => membersMap.Values;

        //public IEnumerable<MethodWithBody> methods ...

        public abstract class Head {
            private Head() {}
            //TODO: isType, isGeneric

            public class Slots : Head {
                public readonly Loc loc;
                public readonly ImmutableArray<Slot> slots;
                public Slots(Loc loc, ImmutableArray<Slot> slots) {
                    this.loc = loc;
                    this.slots = slots;
                }
            }
        }
    }

    abstract class Member {
        public abstract Sym name { get; }
        public abstract Loc loc { get; }
    }

    abstract class NzMethod : Member {
        public readonly ClassLike klass;
        private readonly Loc _loc;
        public readonly bool isStatic;//TODO: just store static methods elsewhere?
        public readonly Ty returnTy;
        readonly Sym _name;
        public readonly ImmutableArray<Parameter> parameters;

        public NzMethod(ClassLike klass, Loc loc, bool isStatic, Ty returnTy, Sym name, ImmutableArray<Parameter> parameters) {
            this.klass = klass;
            this._loc = loc;
            this.isStatic = isStatic;
            this.returnTy = returnTy;
            this._name = name;
            this.parameters = parameters;
        }

        public override Loc loc => _loc;
        public override Sym name => _name;

        int arity => parameters.Length;

        public sealed class Parameter {
            public readonly Loc loc;
            public readonly Ty ty;
            public readonly Sym name;

            public Parameter(Loc loc, Ty ty, Sym name) {
                this.loc = loc;
                this.ty = ty;
                this.name = name;
            }
        }
    }
    sealed class MethodWithBody : NzMethod {
        public MethodWithBody(ClassLike klass, Loc loc, bool isStatic, Ty returnTy, Sym name, ImmutableArray<Parameter> parameters)
            : base(klass, loc, isStatic, returnTy, name, parameters) {}

        Expr _body;
        public Expr body { get { return nonNull(_body); } set { _body = value; } }
    }
    //BuiltinMethod, MethodWithBody

    sealed class Slot {
        public readonly ClassLike klass;
        public readonly Loc loc;
        public readonly bool mutable;
        public readonly Ty ty;
        public readonly Sym name;

        Slot(ClassLike klass, Loc loc, bool mutable, Ty ty, Sym name) {
            this.klass = klass;
            this.loc = loc;
            this.mutable = mutable;
            this.ty = ty;
            this.name = name;
        }
    }

    enum ExprKind {
        Access,
        Let,
        Seq,
    }

    abstract class Expr {
        public ExprKind kind { get; }
        public readonly Loc loc;
        public abstract Ty ty { get; }
        protected Expr(Loc loc) {
            this.loc = loc;
        }
    }

    abstract class Pattern {
        readonly Loc loc;
        private Pattern(Loc loc) { this.loc = loc; }

        public sealed class Ignore : Pattern {
            public Ignore(Loc loc) : base(loc) {}
        }
        public sealed class Single : Pattern {
            public readonly Ty ty;
            public readonly Sym name;
            public Single(Loc loc, Ty ty, Sym name) : base(loc) {
                this.ty = ty;
                this.name = name;
            }
        }
        public sealed class Destruct : Pattern {
            public readonly ImmutableArray<Pattern> destructuredInto;
            public Destruct(Loc loc, ImmutableArray<Pattern> destructuredInto) : base(loc) {
                this.destructuredInto = destructuredInto;
            }
        }
    }


    abstract class Access : Expr {
        public abstract Sym name { get; }
        private Access(Loc loc) : base(loc) {}

        //class Parameter : Access {
        //    constructor(Loc loc) : base(loc) {
        //    }
        //}

        public sealed class Local : Access {
            readonly Pattern.Single local;
            public Local(Loc loc, Pattern.Single local) : base(loc) {
                this.local = local;
            }

            public override Ty ty => local.ty;
            public override Sym name => local.name;
        }
    }

    sealed class Let : Expr {
        public readonly Expr value;
        public readonly Expr then;

        public Let(Loc loc, Pattern assigned, Expr value, Expr then) : base(loc) {
            this.value = value;
            this.then = then;
        }

        public override Ty ty => then.ty;
    }

    sealed class Seq : Expr {
        public readonly Expr action;
        public readonly Expr then;

        public Seq(Loc loc, Expr action, Expr then) : base(loc) {
            this.action = action;
            this.then = then;
        }

        public override Ty ty => then.ty;
    }

    //LiteralValue, Literal
    //StaticMethodCall, MethodCall

    /*sealed class GetSlot : Expr {
        readonly Expr target;
        readonly Slot slot;
        public GetSlot(Loc loc, Expr target, Slot slot) : base(loc) {
            assert(!slot.mutable);
            this.target = target;
            this.slot = slot;
        }
    }*/
}

//mv
class LineColumnGetter {
    readonly string text;
    private readonly ImmutableArray<int> lineToPos;

    public LineColumnGetter(string text) {
        this.text = text;
        lineToPos = build<int>(b => {
            b.Add(0);
            for (var pos = 0; pos < text.Length; pos++) {
                var ch = text[pos];
                if (ch == '\n')
                    b.Add(pos + 1);
            }
        });
    }

    public LineAndColumnLoc lineAndColumnAtLoc(Loc loc) =>
        new LineAndColumnLoc(lineAndColumnAtPos(loc.start), lineAndColumnAtPos(loc.end));

    public int lineAtPos(int pos) =>
        lineAndColumnAtPos(pos).line;

    public LineAndColumn lineAndColumnAtPos(int pos) {
		//binary search
		var lowLine = 0;
		var highLine = lineToPos.Length - 1;

		//Invariant:
		//start of lowLineNumber comes before pos
		//end of line highLineNumber comes after pos
		while (lowLine <= highLine) {
			var middleLine = mid(lowLine, highLine);
			var middlePos = lineToPos[middleLine];

			if (middlePos == pos)
				return new LineAndColumn(middleLine, 0);
			else if (pos < middlePos)
				highLine = middleLine - 1;
			else // pos > middlePos
				lowLine = middleLine + 1;
		}

		var line = lowLine - 1;
		return new LineAndColumn(line, pos - lineToPos[line]);
    }

    private static int mid(int a, int b) => (a + b) / 2;
}

struct LineAndColumnLoc
{
    public readonly LineAndColumn start;
    public readonly LineAndColumn end;
    public LineAndColumnLoc(LineAndColumn start, LineAndColumn end) {
        this.start = start;
        this.end = end;
    }

    public override string ToString() => $"{start}-{end}";
}

struct LineAndColumn
{
    public readonly int line;
    public readonly int column;
    public LineAndColumn(int line, int column) {
        this.line = line;
        this.column = column;
    }

    public override string ToString() => $"{line}:{column}";
}

struct Loc {
    public readonly int start;
    public readonly int end;

    public static Loc singleChar(int start) => new Loc(start, start + 1);
    //TODO: eventually get rid of this
    public static readonly Loc zero = new Loc(0, 0);

    public Loc(int start, int end) {
        this.start = start;
        this.end = end;
    }
}

namespace Model { //->Emit
    static class Emit {
        public static void writeBytecode(ModuleBuilder moduleBuilder, Klass klass, LineColumnGetter lineColumnGetter) {
            var tb = moduleBuilder.DefineType(klass.name.str, TypeAttributes.Public); //TODO: may need to store this with the class

            tb.CreateTypeInfo();
            //val bytes = classToBytecode(klass, lineColumnGetter);
            //set things on the klass

            //Create ourselves a class

        }

        static void foo(TypeBuilder tb, Klass klass) {
            var slots = (klass.head as Klass.Head.Slots).slots;
            foreach (var slot in slots) {
                var fb = tb.DefineField(slot.name.str, slot.ty.toType(), FieldAttributes.Public);
            }

            foreach (var member in klass.members) {
                var method = member as MethodWithBody; //todo: other members

                var mb = tb.DefineMethod(method.name.str, MethodAttributes.Public, method.returnTy.toType(),
                    method.parameters.MapToArray(p => p.ty.toType()));
                var methIl = mb.GetILGenerator();
                new ExprEmitter(methIl).emitAny(method.body);
            }

            //Fields, constructors, etc.
        }

    }

    class ExprEmitter {
        readonly ILGenerator il;
        public ExprEmitter(ILGenerator il) {
            this.il = il;
        }

        public void emitAny(Expr e) {
            switch (e.kind) {
                case ExprKind.Access:
                    emit(e as Access);
                    break;
                case ExprKind.Let:
                    emit(e as Let);
                    break;
                case ExprKind.Seq:
                    emit(e as Seq);
                    break;
            }
        }

        private void emit(Access e) {
            throw new NotImplementedException();
        }

        private void emit(Let l) {
            throw new NotImplementedException();
        }

        private void emit(Seq s) {
            emitAny(s.action);
            emitAny(s.then);
        }

        private void op(OpCode op) {
            il.Emit(op);
        }
    }
}

namespace ast
{

    abstract class Node
    {
        public abstract Loc loc { get; }
    }


}
