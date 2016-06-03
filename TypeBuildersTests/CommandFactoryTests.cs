using System;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using Xunit;
using Npgsql;
using TypeBuilders.TransADO;

public class CommandFactoryTests
{
    public interface DbMapInterface
    {
        NpgsqlCommand Method(object o1, int? i1, byte b1, Type t1);
    }

    static readonly Type TypeObject = typeof(object);

    [Fact]
    public void TestInterfaceMethod()
    {
        var asmName = new AssemblyName(Guid.NewGuid().ToString());
        var dynAsm = AppDomain.CurrentDomain.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.RunAndSave);
        var dynMod = dynAsm.DefineDynamicModule(asmName.Name);

        var tb = CommandFactory.Default.DefineType(dynMod, typeof(DbMapInterface), Guid.NewGuid().ToString(), TypeAttributes.Public);
        var type = tb.CreateType();

        var conn = new NpgsqlConnection();
        var factory = (DbMapInterface)Activator.CreateInstance(type, conn);

        {
            var cmd = factory.Method(null, null, default(byte), null);
            Assert.Equal(CommandType.StoredProcedure, cmd.CommandType);
            Assert.Equal(nameof(DbMapInterface.Method), cmd.CommandText);

            var @params = cmd.Parameters;
            Assert.Equal("@o1", @params[0].ParameterName);
            Assert.Equal(null, @params[0].Value);
            Assert.Equal("@i1", @params[1].ParameterName);
            Assert.Equal(DBNull.Value, @params[1].Value);
            Assert.Equal("@b1", @params[2].ParameterName);
            Assert.Equal((byte)0, @params[2].Value);
            Assert.Equal("@t1", @params[3].ParameterName);
            Assert.Equal(DBNull.Value, @params[3].Value);
        }
        {
            var cmd = factory.Method(CommandFactory.Default, 233, 234, TypeObject);
            Assert.Equal(CommandType.StoredProcedure, cmd.CommandType);
            Assert.Equal(nameof(DbMapInterface.Method), cmd.CommandText);

            var @params = cmd.Parameters;
            Assert.Equal("@o1", @params[0].ParameterName);
            Assert.Equal(CommandFactory.Default, @params[0].Value);
            Assert.Equal("@i1", @params[1].ParameterName);
            Assert.Equal(233, @params[1].Value);
            Assert.Equal("@b1", @params[2].ParameterName);
            Assert.Equal((byte)234, @params[2].Value);
            Assert.Equal("@t1", @params[3].ParameterName);
            Assert.Equal(TypeObject, @params[3].Value);
        }
    }
}
