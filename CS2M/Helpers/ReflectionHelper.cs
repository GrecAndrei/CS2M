using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace CS2M.Helpers
{
    public static class ReflectionHelper
    {
        public static BindingFlags AllAccessFlags =
            BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.Static;

        private static readonly ConcurrentDictionary<(Type, string, Type[]), MethodInfo> _methodCache = new();

        public static int GetEnumValue(Type enumType, string value)
        {
            int i = 0;
            foreach (string name in Enum.GetNames(enumType))
            {
                if (name.Equals(value))
                {
                    return (int) Enum.GetValues(enumType).GetValue(i);
                }

                i++;
            }

            return 0;
        }

        public static T Call<T>(Type type, string name, params object[] param)
        {
            return (T) Call(type, name, param);
        }

        public static object Call(Type type, string name, params object[] param)
        {
            return Call(type, name, param.Select(p => p.GetType()).ToArray(), param);
        }

        public static object Call(Type type, string name, Type[] types, params object[] param)
        {
            var key = (type, name, types);
            MethodInfo methodInfo = _methodCache.GetOrAdd(key,
                k => k.Item1.GetMethod(k.Item2, AllAccessFlags, null, k.Item3, null));
            if (methodInfo == null)
            {
                Log.Error($"ReflectionHelper::Call failed on {type}::{name}");
                return null;
            }

            return methodInfo.Invoke(null, param);
        }

        public static T Call<T>(object obj, string name, params object[] param)
        {
            return (T) Call(obj, name, param);
        }

        /// <summary>
        ///     Call a method through reflection.
        ///     This method infers parameter types, if
        ///     you need to pass null, use the Call method
        ///     with explicit parameter types.
        /// </summary>
        /// <param name="obj">The object to call the method on.</param>
        /// <param name="name">The name of the method.</param>
        /// <param name="param">The non-null parameters.</param>
        /// <returns>The return value.</returns>
        public static object Call(object obj, string name, params object[] param)
        {
            return Call(obj, name, param.Select(p => p.GetType()).ToArray(), param);
        }

        public static object Call(object obj, string name, Type[] types, params object[] param)
        {
            var type = obj.GetType();
            var key = (type, name, types);
            MethodInfo methodInfo = _methodCache.GetOrAdd(key,
                k => k.Item1.GetMethod(k.Item2, AllAccessFlags, null, k.Item3, null));
            if (methodInfo == null)
            {
                Log.Error($"ReflectionHelper::Call failed on {type}.{name}");
                return null;
            }
            return methodInfo.Invoke(obj, param);
        }

        public static void SetAttr(object obj, string attribute, object value)
        {
            FieldInfo field = obj.GetType().GetField(attribute, AllAccessFlags);
            if (field == null)
            {
                Log.Error($"ReflectionHelper::SetAttr failed on {obj.GetType()}.{attribute}");
                return;
            }
            field.SetValue(obj, value);
        }

        public static T GetAttr<T>(object obj, string attribute)
        {
            return (T) GetAttr(obj, attribute);
        }

        public static object GetAttr(object obj, string attribute)
        {
            return obj.GetType().GetField(attribute, AllAccessFlags)?.GetValue(obj);
        }

        public static T GetAttr<T>(Type type, string attribute)
        {
            return (T) GetAttr(type, attribute);
        }

        public static object GetAttr(Type type, string attribute)
        {
            return type.GetField(attribute, AllAccessFlags)?.GetValue(null);
        }

        public static T GetProp<T>(object obj, string property)
        {
            return (T) GetProp(obj, property);
        }

        public static object GetProp(object obj, string property)
        {
            return obj.GetType().GetProperty(property, AllAccessFlags)?.GetValue(obj, null);
        }

        public static MethodBase GetIteratorTargetMethod(Type container, string itName, out Type iteratorType)
        {
            iteratorType = container.GetNestedType(itName, AllAccessFlags);
            return iteratorType.GetMethod("MoveNext", AllAccessFlags);
        }
    }
}
