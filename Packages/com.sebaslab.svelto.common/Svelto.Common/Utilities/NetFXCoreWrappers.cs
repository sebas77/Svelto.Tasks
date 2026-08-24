using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Svelto.DataStructures;

public static class NetFXCoreWrappers
{
    static readonly Type _compilerType = typeof(CompilerGeneratedAttribute);

    public static Type GetDeclaringType(this MethodInfo methodInfo)
    {
#if NETFX_CORE
        return methodInfo.DeclaringType;
#else
        return methodInfo.ReflectedType;
#endif
    }

    public static MethodInfo GetMethodInfoEx(this Delegate delegateEx)
    {
#if NETFX_CORE
            var method = delegateEx.GetMethodInfo();
#else
        var method = delegateEx.Method;
#endif
        return method;
    }

    public static Type[] GetInterfacesEx(this Type type)
    {
#if NETFX_CORE
            return type.GetInterfaces();
#else
        return type.GetInterfaces();
#endif
    }

    public static bool IsInterfaceEx(this Type type)
    {
#if NETFX_CORE
            return type.GetTypeInfo().IsInterface;
#else
        return type.IsInterface;
#endif
    }

    public static bool IsValueTypeEx(this Type type)
    {
#if NETFX_CORE
            return type.GetTypeInfo().IsValueType;
#else
        return type.IsValueType;
#endif
    }

    public static Type GetDeclaringType(this MemberInfo memberInfo)
    {
#if NETFX_CORE
            return memberInfo.DeclaringType;
#else
        return memberInfo.ReflectedType;
#endif
    }

    public static Type GetBaseType(this Type type)
    {
#if NETFX_CORE
            return type.GetTypeInfo().BaseType;
#else
        return type.BaseType;
#endif
    }

    public static IEnumerable<Attribute> GetCustomAttributes(this Type type, bool inherit)
    {
#if !NETFX_CORE
        return Attribute.GetCustomAttributes(type, inherit);
#else
            return type.GetTypeInfo().GetCustomAttributes(inherit);
#endif
    }

    public static bool ContainsCustomAttribute(this MemberInfo memberInfo, Type customAttribute, bool inherit = false)
    {
#if !NETFX_CORE
        return Attribute.IsDefined(memberInfo, customAttribute, inherit);
#else
            return memberInfo.GetCustomAttribute(customAttribute, inherit) != null;
#endif
    }
    
    public static bool ContainsCustomAttribute(this FieldInfo memberInfo, Type customAttribute, bool inherit = false)
    {
#if !NETFX_CORE
        return Attribute.IsDefined(memberInfo, customAttribute, inherit);
#else
            return memberInfo.GetCustomAttribute(customAttribute, inherit) != null;
#endif
    }

    public static bool IsGenericTypeEx(this Type type)
    {
#if !NETFX_CORE
        return type.IsGenericType;
#else
            return type.IsConstructedGenericType;
#endif
    }

    public static Type[] GetGenericArgumentsEx(this Type type)
    {
#if !NETFX_CORE
        return type.GetGenericArguments();
#else
        var typeinfo = type.GetTypeInfo();
        return typeinfo.IsGenericTypeDefinition 
    ? typeinfo.GenericTypeParameters 
    : typeinfo.GenericTypeArguments;
#endif
    }

    public static MemberInfo[] FindWritablePropertiesWithCustomAttribute(this Type contract,
                                                                         Type      customAttributeType)
    {
        var propertyList = new FasterList<MemberInfo>(8);

        do
        {
            var propertyInfos = contract.GetProperties(BindingFlags.Public |
                                                       BindingFlags.NonPublic |
                                                       BindingFlags.DeclaredOnly |
                                                       BindingFlags.Instance);

            for (var i = 0; i < propertyInfos.Length; i++)
            {
                var propertyInfo = propertyInfos[i];

                if (propertyInfo.CanWrite &&
                    propertyInfo.ContainsCustomAttribute(customAttributeType, false))
                    propertyList.Add(propertyInfo);
            }

            contract = contract.GetBaseType();
        } while (contract != null);

        if (propertyList.count > 0)
            return propertyList.ToArray();

        return null;
    }

    public static bool IsCompilerGenerated(this Type t)
    {
#if NETFX_CORE
            var attr = t.GetTypeInfo().GetCustomAttribute(_compilerType);

            return attr != null;
#else
        var attr = Attribute.IsDefined(t, typeof(CompilerGeneratedAttribute));

        return attr;
#endif
    }

    public static bool IsCompilerGenerated(this MemberInfo memberInfo)
    {
#if NETFX_CORE
            var attr = memberInfo.DeclaringType.GetTypeInfo().GetCustomAttribute(_compilerType);
            
            return attr != null;
#else
        var attr = Attribute.IsDefined(memberInfo, _compilerType);

        return attr;
#endif
    }
}
