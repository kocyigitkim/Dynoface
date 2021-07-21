using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;


namespace Dynoface
{
    public class DynamicInterfaceBuilder
    {
        public delegate dynamic MiddlewareFunction(dynamic instance, Type returnType, dynamic target, MethodInfo methodInfo, dynamic[] args);

        private static readonly Dictionary<Type, CachedInterface> CachedInterfaces = new Dictionary<Type, CachedInterface>();
        public static T Build<T>(object targetObject, MiddlewareFunction middlewareFunction)
        {
            var iType = typeof(T);
            if (CachedInterfaces.TryGetValue(iType, out var cachedType))
            {
                return (T)Activator.CreateInstance(cachedType.DeclarationType, new object[] { targetObject, cachedType.MethodInfo });
            }
            AssemblyBuilder asm = AssemblyBuilder.DefineDynamicAssembly(new System.Reflection.AssemblyName(Guid.NewGuid().ToString().Replace("-", "").Replace("{", "").Replace("}", "")), AssemblyBuilderAccess.Run);
            var cModule = asm.DefineDynamicModule(asm.FullName + "Module");
            var cType = cModule.DefineType(iType.Name.Substring(1), TypeAttributes.Public | TypeAttributes.Class);
            cType.AddInterfaceImplementation(iType);

            var fldTarget = cType.DefineField("m_target", typeof(object), FieldAttributes.Public);
            var fldMethods = cType.DefineField("m_methods", typeof(MethodInfo[]), FieldAttributes.Public);
            var methodList = new List<MethodInfo>();
            foreach (var iMethod in iType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (iMethod.Name.StartsWith("get_") || iMethod.Name.StartsWith("set_")) continue;

                var cMethod = cType.DefineMethod(iMethod.Name, MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual, iMethod.ReturnType, iMethod.GetParameters().Select(p => p.ParameterType).ToArray());
                List<ParameterBuilder> parameters = new List<ParameterBuilder>();
                foreach (var p in iMethod.GetParameters())
                {
                    var cP = cMethod.DefineParameter(p.Position, p.Attributes, p.Name);
                    parameters.Add(cP);
                }
                var cIl = cMethod.GetILGenerator();

                cIl.DeclareLocal(typeof(object[]));

                cIl.Emit(OpCodes.Ldc_I4, parameters.Count);
                cIl.Emit(OpCodes.Newarr, typeof(object));
                cIl.Emit(OpCodes.Stloc_0);

                for (var i = 0; i < parameters.Count; i++)
                {
                    cIl.Emit(OpCodes.Ldloc_0); //arr
                    cIl.Emit(OpCodes.Ldc_I4_S, i); //[i]
                    cIl.Emit(OpCodes.Ldarg_S, (uint)(i + 1)); //args[i]
                    cIl.Emit(OpCodes.Stelem, typeof(object)); // arr[i] = args[i]
                }

                //call arguments
                cIl.Emit(OpCodes.Ldarg_0);

                cIl.Emit(OpCodes.Ldtoken, iMethod.ReturnType); //returnType

                cIl.Emit(OpCodes.Ldarg_0); // args[0] - this object
                cIl.Emit(OpCodes.Ldfld, fldTarget); // this.targetObject

                cIl.Emit(OpCodes.Ldarg_0); //args[0] - this object
                cIl.Emit(OpCodes.Ldfld, fldMethods); //this.fldMethod
                cIl.Emit(OpCodes.Ldc_I4, methodList.Count);
                cIl.Emit(OpCodes.Ldelem, typeof(MethodInfo));

                cIl.Emit(OpCodes.Ldloc_0); //arr
                cIl.Emit(OpCodes.Call, middlewareFunction.GetMethodInfo()); //middlewareFunction (returnType, targetObject, arr)
                cIl.Emit(OpCodes.Ret);
                cType.DefineMethodOverride(cMethod, iMethod);
                methodList.Add(iMethod);
            }

            var initProperties = new List<Tuple<FieldBuilder, string>>();
            foreach (var iProperty in iType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var cBackendField = cType.DefineField(iProperty.Name + "_bckfld", iProperty.PropertyType, FieldAttributes.Private);
                var cProperty = cType.DefineProperty(iProperty.Name, PropertyAttributes.None, iProperty.PropertyType, null);
                var getterMethod = cType.DefineMethod("get_" + iProperty.Name, MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual, iProperty.PropertyType, null);
                var getIL = getterMethod.GetILGenerator();
                getIL.Emit(OpCodes.Ldarg_0);
                getIL.Emit(OpCodes.Ldfld, cBackendField);
                getIL.Emit(OpCodes.Ret);
                cProperty.SetGetMethod(getterMethod);
                if (iProperty.GetMethod != null) cType.DefineMethodOverride(getterMethod, iProperty.GetMethod);

                var setterMethod = cType.DefineMethod("set_" + iProperty.Name, MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual, null, new Type[] { iProperty.PropertyType });
                var setIL = setterMethod.GetILGenerator();
                setIL.Emit(OpCodes.Ldarg_0);
                setIL.Emit(OpCodes.Ldarg_1);
                setIL.Emit(OpCodes.Stfld, cBackendField);
                setIL.Emit(OpCodes.Nop);
                setIL.Emit(OpCodes.Ret);
                initProperties.Add(new Tuple<FieldBuilder, string>(cBackendField, iProperty.Name));
                cProperty.SetSetMethod(setterMethod);
                if (iProperty.SetMethod != null) cType.DefineMethodOverride(setterMethod, iProperty.SetMethod);
            }


            var ctor = cType.DefineConstructor(MethodAttributes.Public, CallingConventions.HasThis, new Type[] { typeof(object), typeof(MethodInfo[]) });
            ctor.DefineParameter(0, ParameterAttributes.In, "m_" + fldTarget.Name);
            var ctorIl = ctor.GetILGenerator();
            ctorIl.Emit(OpCodes.Ldarg_0); //this
            ctorIl.Emit(OpCodes.Ldarg_1); //targetObject
            ctorIl.Emit(OpCodes.Stfld, fldTarget); //this.targetObject = targetObject

            ctorIl.Emit(OpCodes.Ldarg_0); //this
            ctorIl.Emit(OpCodes.Ldarg_2); //targetObject
            ctorIl.Emit(OpCodes.Stfld, fldMethods); //this.targetObject = targetObject

            foreach (var property in initProperties)
            {
                ctorIl.Emit(OpCodes.Ldarg_0);
                ctorIl.Emit(OpCodes.Newobj, property.Item1.FieldType.GetConstructor(new Type[0]));
                ctorIl.Emit(OpCodes.Stfld, property.Item1);
            }

            ctorIl.Emit(OpCodes.Nop);
            ctorIl.Emit(OpCodes.Ret);
            var methods = methodList.ToArray();
            CachedInterface instance;
            var buildType = cType.CreateType();
            CachedInterfaces[iType] = instance = new CachedInterface()
            {
                DeclarationType = buildType,
                MethodInfo = methods
            };
            return (T)Activator.CreateInstance(buildType, new object[] { targetObject, instance.MethodInfo });
        }
    }
}
