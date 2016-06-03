using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace TypeBuilders
{
    public class ArgumentProxy<T> : ConstrainedType<T>
    {
        public static ArgumentProxy<T> Default { get; } = new ArgumentProxy<T>();

        public override void ImplementInterfaceMethod(MethodInfo declaration, MethodBuilder implement, FieldInfo input)
        {
            var @params = declaration.GetParameters();
            var callee = ThisType.GetMethod(declaration.Name, BindingFlags.Public | BindingFlags.Instance,
                null, @params.Select(p => p.ParameterType).ToArray(), null);

            var ilGen = implement.GetILGenerator();
            ilGen.Emit(OpCodes.Ldarg_0);
            ilGen.Emit(OpCodes.Ldfld, input);
            for (var i = 0; i < @params.Length;)
                ilGen.Emit(OpCodes.Ldarg_S, (byte)++i);
            ilGen.Emit(OpCodes.Call, callee);
            ilGen.Emit(OpCodes.Ret);
        }
    }
}
