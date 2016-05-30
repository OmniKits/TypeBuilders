﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

public static class ArgsBuilder
{
    internal static readonly Type TypeValueType = typeof(ValueType);
    internal const MethodAttributes InterfaceMethodAttributes =
        MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual | MethodAttributes.Final;

    public static TypeBuilder For<T>(this ModuleBuilder module, Type constraint, string name, TypeAttributes attributes)
        => ArgsBuilder<T>.Generate(module, constraint, name, attributes);
}

public static class ArgsBuilder<T>
{
    static readonly Type ThisType = typeof(T);
    static readonly Type[] OwnInterfaces;
    //static readonly ConstructorInfo BaseConstructor;

    static ArgsBuilder()
    {
        if (ThisType.IsInterface)
            throw new NotSupportedException();

        if (ThisType.ContainsGenericParameters)
            throw new InvalidOperationException();

        if (ThisType.IsSealed && ThisType.IsAbstract)
            throw new InvalidOperationException();
        //if (ThisType.IsSealed)
        //{
        //    if (ThisType.IsAbstract)
        //        throw new InvalidOperationException();
        //}
        //else
        //{
        //    var q = from ci in ThisType.GetConstructors(TypeHelpers.InstanceMemberBindingFlags)
        //            where ci.IsVisibleToDerived()
        //            select ci;
        //    var candidates = q.ToArray();

        //    BaseConstructor = Type.DefaultBinder.SelectMethod(TypeHelpers.InstanceMemberBindingFlags,
        //        candidates, new[] { ThisType }, null) as ConstructorInfo;
        //}

        OwnInterfaces = ThisType.GetInterfaces();
    }

    public static TypeBuilder Generate(ModuleBuilder module, Type constraint, string name, TypeAttributes attributes)
    {
        //var noInheritance = BaseConstructor == null;

        #region ensure arguments

        if (module == null)
            throw new ArgumentNullException(nameof(module));

        if (constraint == null)
            throw new ArgumentNullException(nameof(constraint));

        if (!constraint.IsGenericParameter)
            throw NewException.ForInvalidArgument(nameof(constraint));
        var interfaces = constraint.GetInterfaces();
        if (interfaces.Length == 0)
            throw NewException.ForInvalidArgument(nameof(constraint));
        Type @base = constraint.BaseType;
        if (@base != null && @base != typeof(object))
        {
            if (@base != typeof(ValueType))
                throw new InvalidOperationException();

            //noInheritance = true;
        }
        //else if (!noInheritance)
        //{
        //    @base = ThisType;
        //}

        interfaces = interfaces.Where(itfc => /*noInheritance ||*/ !OwnInterfaces.Contains(itfc)).ToArray();
        if (interfaces.Length == 0)
            return null;

        if (name == null)
            throw new ArgumentNullException(nameof(name));
        if (string.IsNullOrWhiteSpace(name))
            throw NewException.ForArgument(nameof(name));

        #endregion

        var type = module.DefineType(name, attributes, @base, interfaces);

        var field = type.DefineField("@", ThisType, FieldAttributes.Private | FieldAttributes.InitOnly);
        //FieldInfo field = null;
        {
            var ctor = type.DefineConstructor(MethodAttributes.Public | MethodAttributes.NewSlot, CallingConventions.HasThis, new[] { ThisType });
            var ilGen = ctor.GetILGenerator();
            ilGen.Emit(OpCodes.Ldarg_0);
            ilGen.Emit(OpCodes.Ldarg_1);
            //if (!noInheritance)
            //    ilGen.Emit(OpCodes.Call, BaseConstructor);
            //else
            //{
            //field = type.DefineField("@", ThisType, FieldAttributes.Private | FieldAttributes.InitOnly);
            ilGen.Emit(OpCodes.Stfld, field);
            //}
            ilGen.Emit(OpCodes.Ret);
        }

        var methods = interfaces.SelectMany(itfc => itfc.GetMethods()).ToArray();
        var hashset = new HashSet<string>();
        foreach (var grps in methods.GroupBy(mi => mi.Name))
        {
            if (grps.Count() != 1)
                hashset.Add(grps.Key);
        }

        foreach (var mi in methods)
        {
            var isExplicit = hashset.Contains(mi.Name);

            var @params = mi.GetParameters();
            var paramTypes = @params.Select(p => p.ParameterType).ToArray();

            MethodBuilder @new;
            if (!isExplicit)
                @new = type.DefineMethod(mi.Name, ArgsBuilder.InterfaceMethodAttributes | MethodAttributes.Public, mi.ReturnType, paramTypes);
            else
            {
                @new = type.DefineMethod(mi.DeclaringType.FullName + "::" + mi.Name,
                    ArgsBuilder.InterfaceMethodAttributes | MethodAttributes.Private, mi.ReturnType, paramTypes);
            }

            for (var i = 0; i < @params.Length;)
            {
                var p = @params[i++];
                @new.DefineParameter(i, p.Attributes, p.Name);
            }

            var ilGen = @new.GetILGenerator();
            ilGen.Emit(OpCodes.Ldarg_0);
            //if (noInheritance)
            ilGen.Emit(OpCodes.Ldfld, field);
            for (var i = 0; i < @params.Length;)
                ilGen.Emit(OpCodes.Ldarg_S, (byte)++i);
            ilGen.Emit(OpCodes.Call, ThisType.GetMethod(mi.Name, BindingFlags.Public | BindingFlags.Instance, null, paramTypes, null));
            ilGen.Emit(OpCodes.Ret);

            if (isExplicit)
                type.DefineMethodOverride(@new, mi);
        }

        return type;
    }
}
