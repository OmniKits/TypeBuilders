using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace TypeBuilders
{
    using static ArgsBuilder;
    using static GenericParameterAttributes;

    public static class ArgsBuilder
    {
        internal static readonly Type TypeObject = typeof(object);
        internal static readonly Type TypeValueType = typeof(ValueType);
        internal const MethodAttributes InterfaceMethodAttributes =
            MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual | MethodAttributes.Final;

        public static TypeBuilder For<T>(this ModuleBuilder module, Type constraint, string name, TypeAttributes attributes,
            Func<MethodInfo, string> explicitInterfaceMethodNameTranslator = null)
         => ArgsBuilder<T>.Default.MakeConstrainedType(module, constraint, name, attributes, explicitInterfaceMethodNameTranslator);
    }

    public class ArgsBuilder<T>
    {
        protected static readonly Type ThisType = typeof(T);

        static readonly Type[] OwnInterfaces;
        internal static readonly ArgsBuilder<T> Default = new ArgsBuilder<T>();

        static ArgsBuilder()
        {
            if (ThisType.IsInterface)
                throw new NotSupportedException();

            if (ThisType.ContainsGenericParameters)
                throw new InvalidOperationException();

            if (ThisType.IsSealed && ThisType.IsAbstract)
                throw new InvalidOperationException();

            OwnInterfaces = ThisType.GetInterfaces();
        }

        private static readonly Func<MethodInfo, string> ExplicitInterfaceMethodNameTranslator
          = (method) => method.DeclaringType.FullName + "::" + method.Name;
        public virtual TypeBuilder MakeConstrainedType(ModuleBuilder module, Type constraint, string name, TypeAttributes attributes,
            Func<MethodInfo, string> explicitInterfaceMethodNameTranslator = null)
        {
            #region ensure arguments

            if (module == null)
                throw new ArgumentNullException(nameof(module));

            #region check argument constraint

            if (constraint == null)
                throw new ArgumentNullException(nameof(constraint));

            if (!constraint.IsGenericParameter)
                throw NewException.ForInvalidArgument(nameof(constraint));

            var gpAttrs = constraint.GenericParameterAttributes;
            if ((gpAttrs & (DefaultConstructorConstraint | NotNullableValueTypeConstraint)) == DefaultConstructorConstraint)
                throw NewException.ForInvalidArgument(nameof(constraint));

            var interfaces = constraint.GetInterfaces();
            if (interfaces.Length == 0)
                throw NewException.ForInvalidArgument(nameof(constraint));

            Type @base = constraint.BaseType;
            if (@base != null && @base != TypeObject && @base != TypeValueType)
                throw NewException.ForInvalidArgument(nameof(constraint));

            #endregion

            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (string.IsNullOrWhiteSpace(name))
                throw NewException.ForArgument(nameof(name));

            #endregion

            var type = module.DefineType(name, attributes, @base, interfaces);

            var input = type.DefineField("@", ThisType, FieldAttributes.Private | FieldAttributes.InitOnly);
            var ctor = type.DefineConstructor(MethodAttributes.Public, CallingConventions.HasThis, new[] { ThisType });
            var ilGen = ctor.GetILGenerator();
            ilGen.Emit(OpCodes.Ldarg_0);
            ilGen.Emit(OpCodes.Ldarg_1);
            ilGen.Emit(OpCodes.Stfld, input);
            ilGen.Emit(OpCodes.Ret);

            ImplementInterfaceMethods(type, input, explicitInterfaceMethodNameTranslator);

            return type;
        }

        public virtual void ImplementInterfaceMethods(TypeBuilder type, FieldInfo input,
            Func<MethodInfo, string> explicitInterfaceMethodNameTranslator = null)
        {
            var methods = type.GetInterfaces().SelectMany(itfc => itfc.GetMethods()).ToArray();
            var hashset = new HashSet<string>();
            foreach (var grps in methods.GroupBy(mi => mi.Name))
            {
                if (grps.Count() != 1)
                    hashset.Add(grps.Key);
            }

            foreach (var mi in methods)
            {
                var isExplicit = explicitInterfaceMethodNameTranslator != null || hashset.Contains(mi.Name);

                var @params = mi.GetParameters();
                var paramTypes = @params.Select(p => p.ParameterType).ToArray();

                MethodBuilder @new;
                if (!isExplicit)
                    @new = type.DefineMethod(mi.Name, InterfaceMethodAttributes | MethodAttributes.Public, mi.ReturnType, paramTypes);
                else
                {
                    @new = type.DefineMethod((explicitInterfaceMethodNameTranslator ?? ExplicitInterfaceMethodNameTranslator).Invoke(mi),
                        InterfaceMethodAttributes | MethodAttributes.Private, mi.ReturnType, paramTypes);
                }

                for (var i = 0; i < @params.Length;)
                {
                    var p = @params[i++];
                    @new.DefineParameter(i, p.Attributes, p.Name);
                }

                ImplementInterfaceMethod(mi, @new, input);

                if (isExplicit)
                    type.DefineMethodOverride(@new, mi);
            }
        }

        public virtual void ImplementInterfaceMethod(MethodInfo declaration, MethodBuilder implement, FieldInfo input)
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
