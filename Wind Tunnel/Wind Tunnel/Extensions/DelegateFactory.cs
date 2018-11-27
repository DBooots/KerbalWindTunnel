using System;
using System.Reflection;
using System.Linq;
using System.Linq.Expressions;

namespace KerbalWindTunnel.Extensions
{
    public static class DelegateFactory
    {
        public static Func<object> Constructor(this Type source)
        {
            var constructorInfo = source.GetConstructor(BindingFlags.Public, null, Type.EmptyTypes, null);
            if (constructorInfo == null)
                return null;
            return (Func<object>)Expression.Lambda(Expression.New(constructorInfo)).Compile();
        }
        public static TDelegate Constructor<TDelegate>(this Type source) where TDelegate : class
        {
            var ctrArgs = GetFuncDelegateArguments<TDelegate>();
            var constructorInfo = source.GetConstructor(BindingFlags.Public, null, ctrArgs, null);
            var parameters = ctrArgs.Select(arg => Expression.Parameter(arg, arg.Name)).ToArray();
            Expression returnExpression = Expression.New(constructorInfo, parameters);
            if (!source.IsClass)
                returnExpression = Expression.Convert(returnExpression, typeof(object));
            return Expression.Lambda(returnExpression, parameters).Compile() as TDelegate;
        }
        public static Func<object[], object> Constructor(this Type source, Type[] ctrArgTypes)
        {
            var constructorInfo = source.GetConstructor(BindingFlags.Public, null, ctrArgTypes, null);
            if (constructorInfo == null)
                return null;
            var argsArray = Expression.Parameter(typeof(object[]), "objects");
            var paramsExpression = new Expression[ctrArgTypes.Length];
            for (int i = 0; i < ctrArgTypes.Length; i++)
                paramsExpression[i] = Expression.Convert(Expression.ArrayIndex(argsArray, Expression.Constant(i)), ctrArgTypes[i]);
            Expression returnExpression = Expression.New(constructorInfo, paramsExpression);
            if (!source.IsClass)
                returnExpression = Expression.Convert(returnExpression, typeof(object));
            return (Func<object[], object>)Expression.Lambda(returnExpression, argsArray).Compile();
        }

        private static Type[] GetFuncDelegateArguments<TDelegate>() where TDelegate : class
        {
            if (!typeof(TDelegate).IsGenericType)
                throw new ArgumentException();
            return typeof(TDelegate).GetGenericArguments().Reverse().Skip(1).Reverse().ToArray();
        }
        private static Type GetFuncDelegateReturnType<TDelegate>() where TDelegate : class
        {
            if (!typeof(TDelegate).IsGenericType)
                throw new ArgumentException();
            return typeof(TDelegate).GetGenericArguments().Last();
        }

        public static Func<object, object> FieldGet(this Type source, string fieldName)
        {
            return FieldGet(source, source.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance));
        }
        public static Func<object, object> FieldGet(this Type source, FieldInfo fieldInfo)
        {
            if (fieldInfo == null)
                return null;

            var sourceParam = Expression.Parameter(typeof(object), "object");
            Expression returnExpression = Expression.Field(Expression.Convert(sourceParam, source), fieldInfo);
            if (!fieldInfo.FieldType.IsClass)
                returnExpression = Expression.Convert(returnExpression, typeof(object));
            var lambda = Expression.Lambda(returnExpression, sourceParam);
            return (Func<object, object>)lambda.Compile();
        }

        public static TDelegate StaticMethod<TDelegate>(this Type source, string name) where TDelegate : class
            => StaticMethod<TDelegate>(source, source.GetMethod(name, BindingFlags.Static | BindingFlags.Public, null, GetFuncDelegateArguments<TDelegate>(), null));
        public static TDelegate StaticMethod<TDelegate>(this Type source, MethodInfo methodInfo) where TDelegate : class
            => Delegate.CreateDelegate(typeof(TDelegate), methodInfo) as TDelegate;

        public static TDelegate StaticMethod<TDelegate>(this Type source, string name, params Type[] paramTypes) where TDelegate : class
            => StaticMethod<TDelegate>(source, source.GetMethod(name, BindingFlags.Static | BindingFlags.Public, null, paramTypes, null), paramTypes);
        public static TDelegate StaticMethod<TDelegate>(this Type source, MethodInfo methodInfo, params Type[] paramTypes) where TDelegate : class
        {
            var argsArray = Expression.Parameter(typeof(object[]), "objects");
            var paramsExpression = new Expression[paramTypes.Length];
            for (int i = 0; i < paramTypes.Length; i++)
                paramsExpression[i] = Expression.Convert(Expression.ArrayIndex(argsArray, Expression.Constant(i)), paramTypes[i]);
            Expression returnExpression = Expression.Call(methodInfo, paramsExpression);
            if (!source.IsClass)
                returnExpression = Expression.Convert(returnExpression, typeof(object));
            return Expression.Lambda(returnExpression, argsArray).Compile() as TDelegate;
        }

        public static Func<object[], object> StaticMethod(this Type source, string name, params Type[] paramTypes)
            => StaticMethod<Func<object[], object>>(source, name, paramTypes);
        public static Func<object[], object> StaticMethod(this Type source, MethodInfo methodInfo, params Type[] paramTypes)
            => StaticMethod<Func<object[], object>>(source, methodInfo, paramTypes);

        public static Action<object[]> StaticMethodVoid(this Type source, string name, params Type[] paramTypes)
            => StaticMethod<Action<object[]>>(source, name, paramTypes);
        public static Action<object[]> StaticMethodVoid(this Type source, MethodInfo methodInfo, params Type[] paramTypes)
            => StaticMethod<Action<object[]>>(source, methodInfo, paramTypes);

        public static TDelegate InstanceMethod<TDelegate>(this Type source, string name, params Type[] paramTypes) where TDelegate : class
            => InstanceMethod<TDelegate>(source, source.GetMethod(name, BindingFlags.Instance | BindingFlags.Public, null, paramTypes, null), paramTypes);
        public static TDelegate InstanceMethod<TDelegate>(this Type source, MethodInfo methodInfo, params Type[] paramTypes) where TDelegate : class
        {
            if (methodInfo == null)
                return null;
            var argsArray = Expression.Parameter(typeof(object[]), "objects");
            var sourceParameter = Expression.Parameter(typeof(object), "source");
            var paramsExpression = new Expression[paramTypes.Length];
            for (int i = 0; i < paramTypes.Length; i++)
                paramsExpression[i] = Expression.Convert(Expression.ArrayIndex(argsArray, Expression.Constant(i)), paramTypes[i]);
            Expression returnExpression = Expression.Call(Expression.Convert(sourceParameter, source), methodInfo, paramsExpression);
            if (methodInfo.ReturnType != typeof(void) && !methodInfo.ReturnType.IsClass)
                returnExpression = Expression.Convert(returnExpression, typeof(object));
            return Expression.Lambda(returnExpression, sourceParameter, argsArray).Compile() as TDelegate;
        }

        public static TDelegate InstanceMethod<TDelegate>(this Type source, string name) where TDelegate : class
            => InstanceMethod<TDelegate>(source, source.GetMethod(name, BindingFlags.Instance | BindingFlags.Public, null, GetFuncDelegateArguments<TDelegate>(), null));
        public static TDelegate InstanceMethod<TDelegate>(this Type source, MethodInfo methodInfo) where TDelegate : class
        {
            var delegateParams = GetFuncDelegateArguments<TDelegate>();
            if (methodInfo == null)
                return null;
            Delegate deleg;
            if (delegateParams[0] == source)
                deleg = Delegate.CreateDelegate(typeof(TDelegate), methodInfo);
            else
            {
                var sourceParameter = Expression.Parameter(typeof(object), "source");
                var expressions = delegateParams.Select(arg => Expression.Parameter(arg, arg.Name)).ToArray();
                Expression returnExpression = Expression.Call(Expression.Convert(sourceParameter, source), methodInfo, expressions.Cast<Expression>());
                if (methodInfo.ReturnType != typeof(void) && !methodInfo.ReturnType.IsClass)
                    returnExpression = Expression.Convert(returnExpression, typeof(object));
                var lambdaParams = new[] { sourceParameter }.Concat(expressions).ToArray();
                deleg = Expression.Lambda(returnExpression, lambdaParams).Compile();
            }
            return deleg as TDelegate;
        }
        
        public static Func<object[], object> InstanceMethod(this Type source, string name, params Type[] paramTypes)
            => InstanceMethod<Func<object[], object>>(source, name, paramTypes);
        public static Func<object[], object> InstanceMethod(this Type source, MethodInfo methodInfo, params Type[] paramTypes)
            => InstanceMethod<Func<object[], object>>(source, methodInfo, paramTypes);

        public static Action<object[]> InstanceMethodVoid(this Type source, string name, params Type[] paramTypes)
            => InstanceMethod<Action<object[]>>(source, name, paramTypes);
        public static Action<object[]> InstanceMethodVoid(this Type source, MethodInfo methodInfo, params Type[] paramTypes)
            => InstanceMethod<Action<object[]>>(source, methodInfo, paramTypes);
    }
}
