using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;

using TypeBuilders;

public class ArgsBuilderTests
{
    #region testimony classes

    public class TargetClass0
    {
        public byte UInt8 { get; set; }
        public int Int32 { get; set; }
    }
    public class TargetClass1 : TargetClass0
    {
        public TargetClass1() { }
        public TargetClass1(TargetClass1 o)
        { }
    }

    #endregion

    #region testimony interfaces

    public interface IHasUInt8
    {
        byte UInt8 { get; set; }
    }
    public interface IHasInt32
    {
        int Int32 { get; set; }
    }
    public interface IHasInts
        : IHasUInt8, IHasInt32
    {
        new int Int32 { get; set; }
    }

    #endregion

    #region testimony methods

    #region no interface

    public int Method0<T>(T input)
    {
        throw new NotImplementedException();
    }
    public int Method0n<T>(T input)
        where T : new()
    {
        throw new NotImplementedException();
    }
    public int Method0s<T>(T input)
        where T : struct
    {
        throw new NotImplementedException();
    }

    #endregion

    #region one combined interface

    public int Method1<T>(T input)
        where T : IHasInts
     => input.Int32 + input.UInt8;
    public int Method1n<T>(T input)
        where T : IHasInts, new()
     => input.Int32 + input.UInt8;
    public int Method1s<T>(T input)
        where T : struct, IHasInts
     => input.Int32 + input.UInt8;

    #endregion

    #region two interfaces

    public int Method2<T>(T input)
        where T : IHasInt32, IHasUInt8
     => input.Int32 + input.UInt8;
    public int Method2n<T>(T input)
        where T : IHasInt32, IHasUInt8, new()
     => input.Int32 + input.UInt8;
    public int Method2s<T>(T input)
        where T : struct, IHasInt32, IHasUInt8
     => input.Int32 + input.UInt8;

    #endregion

    #endregion

    class MyBuilder : ArgsBuilder<TargetClass1>
    {
        public static new MyBuilder Default { get; } = new MyBuilder();
    }

    static readonly MyBuilder DefaultBuilder = MyBuilder.Default;

    [Fact]
    public void TestGeneration()
    {
        var asmName = new AssemblyName(Guid.NewGuid().ToString());
        var dynAsm = AppDomain.CurrentDomain.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.RunAndSave);
        var dynMod = dynAsm.DefineDynamicModule(asmName.Name);

        Assert.ThrowsAny<ArgumentException>(() => TestMethod(dynMod, typeof(ArgsBuilderTests).GetMethod(nameof(Method0))));
        Assert.ThrowsAny<ArgumentException>(() => TestMethod(dynMod, typeof(ArgsBuilderTests).GetMethod(nameof(Method0n))));
        Assert.ThrowsAny<ArgumentException>(() => TestMethod(dynMod, typeof(ArgsBuilderTests).GetMethod(nameof(Method0s))));

        TestMethod(dynMod, typeof(ArgsBuilderTests).GetMethod(nameof(Method1)));
        Assert.ThrowsAny<ArgumentException>(() => TestMethod(dynMod, typeof(ArgsBuilderTests).GetMethod(nameof(Method1n))));
        TestMethod(dynMod, typeof(ArgsBuilderTests).GetMethod(nameof(Method1s)));

        TestMethod(dynMod, typeof(ArgsBuilderTests).GetMethod(nameof(Method2)));
        Assert.ThrowsAny<ArgumentException>(() => TestMethod(dynMod, typeof(ArgsBuilderTests).GetMethod(nameof(Method2n))));
        TestMethod(dynMod, typeof(ArgsBuilderTests).GetMethod(nameof(Method2s)));

        dynAsm.Save(asmName.Name);
    }

    private void TestMethod(ModuleBuilder dynMod, MethodInfo method)
    {
        var constraint = method.GetGenericArguments().Single(t => t.IsGenericParameter);
        var tb = DefaultBuilder.MakeConstrainedType(dynMod, constraint, Guid.NewGuid().ToString(), TypeAttributes.Public);
        var type = tb.CreateType();

        method = method.MakeGenericMethod(type);
        var rnd = new Random();
        var o = new TargetClass1 { UInt8 = (byte)rnd.Next(), Int32 = rnd.Next() };
        Assert.Equal(o.UInt8 + o.Int32, method.Invoke(this, new[] { Activator.CreateInstance(type, o) }));
    }
}
