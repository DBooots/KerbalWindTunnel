using System;
using System.Reflection;
using System.Linq;
using System.Linq.Expressions;

namespace KerbalWindTunnel.Extensions.Reflection
{
    public delegate void Action<T1, T2, T3, T4, T5>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);

    #region LICENSE

    /*
     * This class is derived from (style and argument name changes only) from code
     * licensed under the CPOL by @n-podbielski (https://www.codeproject.com/Members/n-podbielski).
     * 
     * CODE PROJECT OPEN LICENSE (CPOL):
     * https://www.codeproject.com/info/cpol10.aspx
     * 
     * Preamble
     * 
     * This License governs Your use of the Work.This License is intended to allow developers to
     * use the Source Code and Executable Files provided as part of the Work in any application
     * in any form.
     *
     * The main points subject to the terms of the License are:
     *   Source Code and Executable Files can be used in commercial applications;
     *   Source Code and Executable Files can be redistributed; and
     *   Source Code can be modified to create derivative works.
     *   No claim of suitability, guarantee, or any warranty whatsoever is provided.The software
     *   is provided "as-is".
     *   The Article(s) accompanying the Work may not be distributed or republished without the
     *   Author's consent
     * 
     * This License is entered between You, the individual or other entity reading or otherwise
     * making use of the Work licensed pursuant to this License and the individual or other
     * entity which offers the Work under the terms of this License ("Author").
     * 
     * License
     * 
     * THE WORK(AS DEFINED BELOW) IS PROVIDED UNDER THE TERMS OF THIS CODE PROJECT OPEN
     * LICENSE("LICENSE"). THE WORK IS PROTECTED BY COPYRIGHT AND/OR OTHER APPLICABLE LAW.ANY USE
     * OF THE WORK OTHER THAN AS AUTHORIZED UNDER THIS LICENSE OR COPYRIGHT LAW IS PROHIBITED.
     * 
     * BY EXERCISING ANY RIGHTS TO THE WORK PROVIDED HEREIN, YOU ACCEPT AND AGREE TO BE BOUND BY THE
     * TERMS OF THIS LICENSE.THE AUTHOR GRANTS YOU THE RIGHTS CONTAINED HEREIN IN CONSIDERATION OF
     * YOUR ACCEPTANCE OF SUCH TERMS AND CONDITIONS. IF YOU DO NOT AGREE TO ACCEPT AND BE BOUND BY
     * THE TERMS OF THIS LICENSE, YOU CANNOT MAKE ANY USE OF THE WORK.
     * 
     * Definitions.
     *   "Articles" means, collectively, all articles written by Author which describes how the
     *   Source Code and Executable Files for the Work may be used by a user.
     *   "Author" means the individual or entity that offers the Work under the terms of this License.
     *   "Derivative Work" means a work based upon the Work or upon the Work and other pre-existing works.
     *   "Executable Files" refer to the executables, binary files, configuration and any required
     *   data files included in the Work.
     *   "Publisher" means the provider of the website, magazine, CD-ROM, DVD or other medium from or
     *   by which the Work is obtained by You.
     *   "Source Code" refers to the collection of source code and configuration files used to create
     *   the Executable Files.
     *   "Standard Version" refers to such a Work if it has not been modified, or has been modified in
     *   accordance with the consent of the Author, such consent being in the full discretion of the Author.
     *   "Work" refers to the collection of files distributed by the Publisher, including the Source
     *   Code, Executable Files, binaries, data files, documentation, whitepapers and the Articles.
     *   "You" is you, an individual or entity wishing to use the Work and exercise your rights under
     *   this License.
     *   
     * Fair Use/Fair Use Rights.Nothing in this License is intended to reduce, limit, or restrict any
     * rights arising from fair use, fair dealing, first sale or other limitations on the exclusive
     * rights of the copyright owner under copyright law or other applicable laws.
     * 
     * License Grant. Subject to the terms and conditions of this License, the Author hereby grants
     * You a worldwide, royalty-free, non-exclusive, perpetual (for the duration of the applicable
     * copyright) license to exercise the rights in the Work as stated below:
     *   You may use the standard version of the Source Code or Executable Files in Your own applications.
     *   You may apply bug fixes, portability fixes and other modifications obtained from the Public
     *   Domain or from the Author.A Work modified in such a way shall still be considered the standard
     *   version and will be subject to this License.
     *   You may otherwise modify Your copy of this Work (excluding the Articles) in any way to create
     *   a Derivative Work, provided that You insert a prominent notice in each changed file stating
     *   how, when and where You changed that file.
     *   You may distribute the standard version of the Executable Files and Source Code or Derivative
     *   Work in aggregate with other(possibly commercial) programs as part of a larger(possibly
     *   commercial) software distribution.
     *   The Articles discussing the Work published in any form by the author may not be distributed
     *   or republished without the Author's consent. The author retains copyright to any such Articles.
     *   You may use the Executable Files and Source Code pursuant to this License but you may not repost
     *   or republish or otherwise distribute or make available the Articles, without the prior written
     *   consent of the Author.
     *   Any subroutines or modules supplied by You and linked into the Source Code or Executable Files
     *   of this Work shall not be considered part of this Work and will not be subject to the terms of
     *   this License.
     *   Patent License. Subject to the terms and conditions of this License, each Author hereby grants
     *   to You a perpetual, worldwide, non-exclusive, no-charge, royalty-free, irrevocable (except as
     *   stated in this section) patent license to make, have made, use, import, and otherwise transfer
     *   the Work.
     * 
     * Restrictions.The license granted in Section 3 above is expressly made subject to and limited by
     * the following restrictions:
     *   You agree not to remove any of the original copyright, patent, trademark, and attribution
     *   notices and associated disclaimers that may appear in the Source Code or Executable Files.
     *   You agree not to advertise or in any way imply that this Work is a product of Your own.
     *   The name of the Author may not be used to endorse or promote products derived from the Work
     *   without the prior written consent of the Author.
     *   You agree not to sell, lease, or rent any part of the Work.This does not restrict you from
     *   including the Work or any part of the Work inside a larger software distribution that itself
     *   is being sold. The Work by itself, though, cannot be sold, leased or rented.
     *   You may distribute the Executable Files and Source Code only under the terms of this License,
     *   and You must include a copy of, or the Uniform Resource Identifier for, this License with
     *   every copy of the Executable Files or Source Code You distribute and ensure that anyone
     *   receiving such Executable Files and Source Code agrees that the terms of this License apply
     *   to such Executable Files and/or Source Code.
     *   You may not offer or impose any terms on the Work that alter or restrict the terms of this
     *   License or the recipients' exercise of the rights granted hereunder.
     *   You may not sublicense the Work.
     *   You must keep intact all notices that refer to this License and to the disclaimer of warranties.
     *   You may not distribute the Executable Files or Source Code with any technological measures
     *   that control access or use of the Work in a manner inconsistent with the terms of this License.
     *   You agree not to use the Work for illegal, immoral or improper purposes, or on pages
     *   containing illegal, immoral or improper material. The Work is subject to applicable export
     *   laws.
     *   You agree to comply with all such laws and regulations that may apply to the Work after Your
     *   receipt of the Work.
     *     
     * Representations, Warranties and Disclaimer.THIS WORK IS PROVIDED "AS IS", "WHERE IS" AND "AS
     * AVAILABLE", WITHOUT ANY EXPRESS OR IMPLIED WARRANTIES OR CONDITIONS OR GUARANTEES. YOU, THE
     * USER, ASSUME ALL RISK IN ITS USE, INCLUDING COPYRIGHT INFRINGEMENT, PATENT INFRINGEMENT,
     * SUITABILITY, ETC.AUTHOR EXPRESSLY DISCLAIMS ALL EXPRESS, IMPLIED OR STATUTORY WARRANTIES OR
     * CONDITIONS, INCLUDING WITHOUT LIMITATION, WARRANTIES OR CONDITIONS OF MERCHANTABILITY,
     * MERCHANTABLE QUALITY OR FITNESS FOR A PARTICULAR PURPOSE, OR ANY WARRANTY OF TITLE OR
     * NON-INFRINGEMENT, OR THAT THE WORK (OR ANY PORTION THEREOF) IS CORRECT, USEFUL, BUG-FREE OR
     * FREE OF VIRUSES.YOU MUST PASS THIS DISCLAIMER ON WHENEVER YOU DISTRIBUTE THE WORK OR DERIVATIVE
     * WORKS.
     * Indemnity.You agree to defend, indemnify and hold harmless the Author and the Publisher from
     * and against any claims, suits, losses, damages, liabilities, costs, and expenses (including
     * reasonable legal or attorneys’ fees) resulting from or relating to any use of the Work by You.
     * Limitation on Liability. EXCEPT TO THE EXTENT REQUIRED BY APPLICABLE LAW, IN NO EVENT WILL THE
     * AUTHOR OR THE PUBLISHER BE LIABLE TO YOU ON ANY LEGAL THEORY FOR ANY SPECIAL, INCIDENTAL,
     * CONSEQUENTIAL, PUNITIVE OR EXEMPLARY DAMAGES ARISING OUT OF THIS LICENSE OR THE USE OF THE WORK
     * OR OTHERWISE, EVEN IF THE AUTHOR OR THE PUBLISHER HAS BEEN ADVISED OF THE POSSIBILITY OF SUCH DAMAGES.
     * Termination.
     *   This License and the rights granted hereunder will terminate automatically upon any breach by
     *   You of any term of this License.Individuals or entities who have received Derivative Works
     *   from You under this License, however, will not have their licenses terminated provided such
     *   individuals or entities remain in full compliance with those licenses.Sections 1, 2, 6, 7, 8,
     *   9, 10 and 11 will survive any termination of this License.
     *   If You bring a copyright, trademark, patent or any other infringement claim against any
     *   contributor over infringements You claim are made by the Work, your License from such
     *   contributor to the Work ends automatically.
     *   Subject to the above terms and conditions, this License is perpetual (for the duration of
     *   the applicable copyright in the Work). Notwithstanding the above, the Author reserves the
     *   right to release the Work under different license terms or to stop distributing the Work
     *   at any time; provided, however that any such election will not serve to withdraw this
     *   License (or any other license that has been, or is required to be, granted under the terms
     *   of this License), and this License will continue in full force and effect unless terminated
     *   as stated above.
     * Publisher. The parties hereby confirm that the Publisher shall not, under any circumstances,
     * be responsible for and shall not have any liability in respect of the subject matter of this
     * License. The Publisher makes no warranty whatsoever in connection with the Work and shall not
     * be liable to You or any party on any legal theory for any damages whatsoever, including
     * without limitation any general, special, incidental or consequential damages arising in
     * connection to this license. The Publisher reserves the right to cease making the Work
     * available to You at any time without notice
     * Miscellaneous
     *   This License shall be governed by the laws of the location of the head office of the Author
     *   or if the Author is an individual, the laws of location of the principal place of residence
     *   of the Author.
     *   If any provision of this License is invalid or unenforceable under applicable law, it shall
     *   not affect the validity or enforceability of the remainder of the terms of this License, and
     *   without further action by the parties to this License, such provision shall be reformed to
     *   the minimum extent necessary to make such provision valid and enforceable.
     *   No term or provision of this License shall be deemed waived and no breach consented to unless
     *   such waiver or consent shall be in writing and signed by the party to be charged with such
     *   waiver or consent.
     *   This License constitutes the entire agreement between the parties with respect to the Work
     *   licensed herein. There are no understandings, agreements or representations with respect to
     *   the Work not specified herein. The Author shall not be bound by any additional provisions that
     *   may appear in any communication from You. This License may not be modified without the mutual
     *   written agreement of the Author and You. */

    #endregion LICENSE

    public static class DelegateFactory
    {
        #region Constructor
        public static Func<object> Constructor(this Type source)
        {
            var constructorInfo = source.GetConstructor(Type.EmptyTypes);// BindingFlags.Public, null, Type.EmptyTypes, null);
            if (constructorInfo == null)
                return null;
            Expression returnExpression = Expression.New(constructorInfo);
            if (source != typeof(object))
                returnExpression = Expression.Convert(returnExpression, typeof(object));
            return (Func<object>)Expression.Lambda(returnExpression).Compile();
        }
        public static TDelegate Constructor<TDelegate>(this Type source) where TDelegate : class
        {
            var ctrArgs = GetFuncDelegateArguments<TDelegate>();
            var constructorInfo = source.GetConstructor(ctrArgs);
            var parameters = ctrArgs.Select(arg => Expression.Parameter(arg, arg.Name)).ToArray();
            Expression returnExpression = Expression.New(constructorInfo, parameters);
            if (!source.IsClass)
                returnExpression = Expression.Convert(returnExpression, typeof(object));
            else if (source != GetFuncDelegateReturnType<TDelegate>())
                returnExpression = Expression.Convert(returnExpression, GetFuncDelegateReturnType<TDelegate>());
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
        #endregion Constructor

        #region Property Get/Set
        // Property Get
        public static Func<object, TProperty> PropertyGet<TProperty>(this Type source, string propertyName)
            => PropertyGet<TProperty>(source, source.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance));
        public static Func<object, TProperty> PropertyGet<TProperty>(this Type source, PropertyInfo propertyInfo)
        {
            if (propertyInfo == null)
                return null;
            var sourceObjectParam = Expression.Parameter(typeof(object), "sourceObject");
            Expression returnExpression = Expression.Call(Expression.Convert(sourceObjectParam, source), propertyInfo.GetGetMethod());
            if (propertyInfo.PropertyType != typeof(TProperty))
            {
                if (!propertyInfo.PropertyType.IsClass && !propertyInfo.PropertyType.IsPrimitive)
                    returnExpression = Expression.Convert(returnExpression, typeof(object));
                else
                    returnExpression = Expression.Convert(returnExpression, typeof(TProperty));
            }
            return (Func<object, TProperty>)Expression.Lambda(returnExpression, sourceObjectParam).Compile();
        }
        public static Func<object, object> PropertyGet(this Type source, string propertyName)
            => PropertyGet(source, source.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance));
        public static Func<object, object> PropertyGet(this Type source, PropertyInfo propertyInfo)
            => PropertyGet<object>(source, propertyInfo);

        //Property Set
        public static Action<object, TProperty> PropertySet<TProperty>(this Type source, string propertyName)
            => PropertySet<TProperty>(source, source.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance));
        public static Action<object, TProperty> PropertySet<TProperty>(this Type source, PropertyInfo propertyInfo)
        {
            if (propertyInfo == null)
                return null;
            var sourceObjectParam = Expression.Parameter(typeof(object), "sourceObject");
            ParameterExpression propertyValueParam;
            Expression valueExpression;
            if (propertyInfo.PropertyType == typeof(TProperty))
            {
                propertyValueParam = Expression.Parameter(propertyInfo.PropertyType, "value");
                valueExpression = propertyValueParam;
            }
            else
            {
                propertyValueParam = Expression.Parameter(typeof(TProperty), "value");
                valueExpression = Expression.Convert(propertyValueParam, propertyInfo.PropertyType);
            }
            return (Action<object, TProperty>)Expression.Lambda(Expression.Call(Expression.Convert(sourceObjectParam, source), propertyInfo.GetSetMethod(), valueExpression), sourceObjectParam, propertyValueParam).Compile();
        }
        public static Action<object, object> PropertySet(this Type source, string propertyName)
            => PropertySet(source, source.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance));
        public static Action<object, object> PropertySet(this Type source, PropertyInfo propertyInfo)
            => PropertySet<object>(source, propertyInfo);
        #endregion Property Get/Set

        #region Indexer Get/Set
        private const string Item = "Item";
        // Indexer Get
        private static Delegate DelegateIndexerGet(Type source, Type returnType, params Type[] indexTypes)
        {
            var propertyInfo = source.GetProperty(Item, returnType, indexTypes);
            var sourceObjectParam = Expression.Parameter(typeof(object), "source");
            var paramsExpression = new ParameterExpression[indexTypes.Length];
            for (var i = 0; i < indexTypes.Length; i++)
                paramsExpression[i] = Expression.Parameter(indexTypes[i], "index");
            return Expression.Lambda(Expression.Call(Expression.Convert(sourceObjectParam, source), propertyInfo.GetGetMethod(), paramsExpression), new[] { sourceObjectParam }.Concat(paramsExpression).ToArray()).Compile();
        }
        public static Func<object, TIndex, TReturn> IndexerGet<TIndex, TReturn>(this Type source)
            => (Func<object, TIndex, TReturn>)DelegateIndexerGet(source, typeof(TReturn), typeof(TIndex));
        public static Func<object, TIndex1, TIndex2, TReturn> IndexerGet<TIndex1, TIndex2, TReturn>(this Type source)
            => (Func<object, TIndex1, TIndex2, TReturn>)DelegateIndexerGet(source, typeof(TReturn), typeof(TIndex1), typeof(TIndex2));
        public static Func<object, TIndex1, TIndex2, TIndex3, TReturn> IndexerGet<TIndex1, TIndex2, TIndex3, TReturn>(this Type source)
            => (Func<object, TIndex1, TIndex2, TIndex3, TReturn>)DelegateIndexerGet(source, typeof(TReturn), typeof(TIndex1), typeof(TIndex2), typeof(TIndex3));

        public static Func<object, object, object> IndexerGet(this Type source, Type returnType, Type indexType)
        {
            var propertyInfo = source.GetProperty(Item, returnType, new[] { indexType });
            var sourceObjectParam = Expression.Parameter(typeof(object), "source");
            var indexObjectParam = Expression.Parameter(typeof(object), "index");
            Expression returnExpression = Expression.Call(Expression.Convert(sourceObjectParam, source), propertyInfo.GetGetMethod(), Expression.Convert(indexObjectParam, indexType));
            if (!propertyInfo.PropertyType.IsClass)
                returnExpression = Expression.Convert(returnExpression, typeof(object));
            return (Func<object, object, object>)Expression.Lambda(returnExpression, sourceObjectParam, indexObjectParam).Compile();
        }
        public static Func<object, object[], object> IndexerGet(this Type source, Type returnType, params Type[] indexTypes)
        {
            var propertyInfo = source.GetProperty(Item, returnType, indexTypes);
            var sourceObjectParam = Expression.Parameter(typeof(object), "source");
            var indexesParam = Expression.Parameter(typeof(object[]), "index");
            var paramsExpression = new Expression[indexTypes.Length];
            for (var i = 0; i < indexTypes.Length; i++)
                paramsExpression[i] = Expression.Convert(Expression.ArrayIndex(indexesParam, Expression.Constant(i)), indexTypes[i]);
            Expression returnExpression = Expression.Call(Expression.Convert(sourceObjectParam, source), propertyInfo.GetGetMethod(), paramsExpression);
            if (!propertyInfo.PropertyType.IsClass)
                returnExpression = Expression.Convert(returnExpression, typeof(object));
            return (Func<object, object[], object>)Expression.Lambda(returnExpression, sourceObjectParam, indexesParam).Compile();
        }

        // Indexer Set
        private static Delegate DelegateIndexerSet(Type source, Type valueType, params Type[] indexTypes)
        {
            var propertyInfo = source.GetProperty(Item, valueType, indexTypes);
            if (propertyInfo == null)
                return null;
            var sourceObjectParam = Expression.Parameter(typeof(object), "source");
            var valueParam = Expression.Parameter(valueType, "value");
            var indexExpressions = new ParameterExpression[indexTypes.Length];
            for (var i = 0; i < indexTypes.Length; i++)
                indexExpressions[i] = Expression.Parameter(indexTypes[i], "index");
            var callArgs = indexExpressions.Concat(new[] { valueParam }).ToArray();
            var paramsExpressions = new[] { sourceObjectParam }.Concat(callArgs).ToArray();
            return Expression.Lambda(Expression.Call(Expression.Convert(sourceObjectParam, source), propertyInfo.GetSetMethod(), callArgs), paramsExpressions).Compile();
        }
        public static Action<object, TIndex, TValue> IndexerSet<TIndex, TValue>(this Type source)
            => (Action<object, TIndex, TValue>)DelegateIndexerSet(source, typeof(TValue), typeof(TIndex));
        public static Action<object, TIndex1, TIndex2, TValue> IndexerSet<TIndex1, TIndex2, TValue>(this Type source)
            => (Action<object, TIndex1, TIndex2, TValue>)DelegateIndexerSet(source, typeof(TValue), typeof(TIndex1), typeof(TIndex2));
        public static Action<object, TIndex1, TIndex2, TIndex3, TValue> IndexerSet<TIndex1, TIndex2, TIndex3, TValue>(this Type source)
            => (Action<object, TIndex1, TIndex2, TIndex3, TValue>)DelegateIndexerSet(source, typeof(TValue), typeof(TIndex1), typeof(TIndex2), typeof(TIndex3));

        public static Action<object, object, object> IndexerSet(this Type source, Type valueType, Type indexType)
        {
            var propertyInfo = source.GetProperty(Item, valueType, new[] { indexType });
            var sourceObjectParam = Expression.Parameter(typeof(object), "source");
            var indexObjectParam = Expression.Parameter(typeof(object), "index");
            var valueParam = Expression.Parameter(typeof(object), "value");
            Expression returnExpression = Expression.Call(Expression.Convert(sourceObjectParam, source), propertyInfo.GetSetMethod(), Expression.Convert(indexObjectParam, indexType));
            return (Action<object, object, object>)Expression.Lambda(returnExpression, sourceObjectParam, indexObjectParam, valueParam).Compile();
        }
        public static Action<object, object[], object> IndexerSet(this Type source, Type valueType, params Type[] indexTypes)
        {
            var propertyInfo = source.GetProperty(Item, valueType, indexTypes);
            if (propertyInfo == null)
                return null;
            var sourceObjectParam = Expression.Parameter(typeof(object), "source");
            var indexesParam = Expression.Parameter(typeof(object[]), "index");
            var valueParam = Expression.Parameter(typeof(object), "value");
            var paramsExpression = new Expression[indexTypes.Length + 1];
            for (var i = 0; i < indexTypes.Length; i++)
                paramsExpression[i] = Expression.Convert(Expression.ArrayIndex(indexesParam, Expression.Constant(i)), indexTypes[i]);
            paramsExpression[indexTypes.Length] = Expression.Convert(valueParam, valueType);
            Expression returnExpression = Expression.Call(Expression.Convert(sourceObjectParam, source), propertyInfo.GetSetMethod(), paramsExpression);
            return (Action<object, object[], object>)Expression.Lambda(returnExpression, sourceObjectParam, indexesParam, valueParam).Compile();
        }
        #endregion Indexer Get/Set

        #region Fields
        // Static
        // Get
        public static Func<TField> StaticFieldGet<TField>(this Type source, string fieldName)
            => StaticFieldGet<TField>(source, source.GetField(fieldName, BindingFlags.Public | BindingFlags.Static));
        public static Func<TField> StaticFieldGet<TField>(this Type source, FieldInfo fieldInfo)
        {
            if (fieldInfo == null) return null;
            return (Func<TField>)Expression.Lambda(Expression.Field(null, fieldInfo)).Compile();
        }
        public static Func<object> StaticFieldGet(this Type source, string fieldName)
            => StaticFieldGet(source, source.GetField(fieldName, BindingFlags.Public | BindingFlags.Static));
        public static Func<object> StaticFieldGet(this Type source, FieldInfo fieldInfo)
        {
            if (fieldInfo == null) return null;
            Expression returnExpression = Expression.Field(null, fieldInfo);
            if (!fieldInfo.FieldType.IsClass)
                returnExpression = Expression.Convert(returnExpression, typeof(object));
            return (Func<object>)Expression.Lambda(returnExpression).Compile();
        }

        // Set
        public static Action<TField> StaticFieldSet<TField>(this Type source, string fieldName)
            => StaticFieldSet<TField>(source, source.GetField(fieldName, BindingFlags.Public | BindingFlags.Static));
        public static Action<TField> StaticFieldSet<TField>(this Type source, FieldInfo fieldInfo)
        {
            System.Reflection.Emit.DynamicMethod m = new System.Reflection.Emit.DynamicMethod(
                "setter_" + fieldInfo.Name, typeof(void), new Type[] { typeof(TField) }, source);
            System.Reflection.Emit.ILGenerator cg = m.GetILGenerator();

            // arg0.<field> = arg1
            cg.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
            cg.Emit(System.Reflection.Emit.OpCodes.Stsfld, fieldInfo);
            cg.Emit(System.Reflection.Emit.OpCodes.Ret);

            return (Action<TField>)m.CreateDelegate(typeof(Action<TField>));
        }
        public static Action<object> StaticFieldSet(this Type source, string fieldName)
            => StaticFieldSet<object>(source, source.GetField(fieldName, BindingFlags.Public | BindingFlags.Static));
        public static Action<object> StaticFieldSet(this Type source, FieldInfo fieldInfo)
            => StaticFieldSet<object>(source, fieldInfo);

        // Instance
        // Get
        public static Func<object, TField> FieldGet<TField>(this Type source, string fieldName)
            => FieldGet<TField>(source, source.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance));
        public static Func<object, TField> FieldGet<TField>(this Type source, FieldInfo fieldInfo)
        {
            if (fieldInfo == null) return null;
            if (!typeof(TField).IsAssignableFrom(fieldInfo.FieldType))
                throw new ArgumentException();

            var sourceParam = Expression.Parameter(typeof(object), "object");
            Expression returnExpression = Expression.Field(Expression.Convert(sourceParam, source), fieldInfo);
            if (fieldInfo.FieldType != typeof(TField))
            {
                if (fieldInfo.FieldType != typeof(void) && !fieldInfo.FieldType.IsClass && !fieldInfo.FieldType.IsPrimitive)
                    returnExpression = Expression.Convert(returnExpression, typeof(object));
                else
                    returnExpression = Expression.Convert(returnExpression, typeof(TField));
            }
            return (Func<object, TField>)Expression.Lambda(returnExpression, sourceParam).Compile();
        }
        public static Func<object, object> FieldGet(this Type source, string fieldName)
            => FieldGet(source, source.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance));
        public static Func<object, object> FieldGet(this Type source, FieldInfo fieldInfo)
        {
            if (fieldInfo == null) return null;

            var sourceParam = Expression.Parameter(typeof(object), "object");
            Expression returnExpression = Expression.Field(Expression.Convert(sourceParam, source), fieldInfo);
            if (!fieldInfo.FieldType.IsClass)
                returnExpression = Expression.Convert(returnExpression, typeof(object));
            var lambda = Expression.Lambda(returnExpression, sourceParam);
            return (Func<object, object>)lambda.Compile();
        }

        // Set
        public static Action<object, TField> FieldSet<TField>(this Type source, string fieldName)
            => FieldSet<TField>(source, source.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance));
        public static Action<object, TField> FieldSet<TField>(this Type source, FieldInfo fieldInfo)
        {
            System.Reflection.Emit.DynamicMethod m = new System.Reflection.Emit.DynamicMethod(
                "setter_" + fieldInfo.Name, typeof(void), new Type[] { typeof(object), typeof(TField) }, source);
            System.Reflection.Emit.ILGenerator cg = m.GetILGenerator();

            // arg0.<field> = arg1
            cg.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
            if (source.IsValueType && !source.IsEnum)
                cg.Emit(System.Reflection.Emit.OpCodes.Unbox, source);
            cg.Emit(System.Reflection.Emit.OpCodes.Ldarg_1);
            cg.Emit(System.Reflection.Emit.OpCodes.Stfld, fieldInfo);
            cg.Emit(System.Reflection.Emit.OpCodes.Ret);

            return (Action<object, TField>)m.CreateDelegate(typeof(Action<object, TField>));
        }
        public static Action<object, object> FieldSet(this Type source, string fieldName)
            => FieldSet<object>(source, source.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance));
        public static Action<object, object> FieldSet(this Type source, FieldInfo fieldInfo)
            => FieldSet<object>(source, fieldInfo);
        #endregion Fields

        #region Methods
        // Static
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
        
        // Instance
        public static TDelegate InstanceMethod<TDelegate>(this Type source, string name, params Type[] paramTypes) where TDelegate : class
            => InstanceMethod<TDelegate>(source, source.GetMethod(name, BindingFlags.Instance | BindingFlags.Public, null, paramTypes.Skip(1).ToArray(), null), paramTypes);
        public static TDelegate InstanceMethod<TDelegate>(this Type source, MethodInfo methodInfo, params Type[] paramTypes) where TDelegate : class
        {
            if (methodInfo == null)
                return null;
            var argsArray = Expression.Parameter(typeof(object[]), "objects");
            var sourceParameter = Expression.Parameter(typeof(object), "source");
            var paramsExpression = new Expression[paramTypes.Length - 1];
            for (int i = 0; i < paramTypes.Length - 1; i++)
                paramsExpression[i] = Expression.Convert(Expression.ArrayIndex(argsArray, Expression.Constant(i + 1)), paramTypes[i + 1]);
            Expression returnExpression = Expression.Call(Expression.Convert(sourceParameter, source), methodInfo, paramsExpression);
            if (methodInfo.ReturnType != typeof(void) && !methodInfo.ReturnType.IsClass)
                returnExpression = Expression.Convert(returnExpression, typeof(object));
            return Expression.Lambda(returnExpression, sourceParameter, argsArray).Compile() as TDelegate;
        }

        public static TDelegate InstanceMethod<TDelegate>(this Type source, string name) where TDelegate : class
            => InstanceMethod<TDelegate>(source, source.GetMethod(name, BindingFlags.Instance | BindingFlags.Public, null, GetFuncDelegateArguments<TDelegate>().Skip(1).ToArray(), null));
        public static TDelegate InstanceMethod<TDelegate>(this Type source, MethodInfo methodInfo) where TDelegate : class
        {
            var delegateParams = GetFuncDelegateArguments<TDelegate>();
            if (delegateParams.Length > 4 || !typeof(TDelegate).IsGenericType)
                return MethodEmit<TDelegate>(methodInfo);
            Delegate deleg;
            if (delegateParams[0] == source)
                deleg = Delegate.CreateDelegate(typeof(TDelegate), methodInfo);
            else
            {
                delegateParams = delegateParams.Skip(1).ToArray();
                var sourceParameter = Expression.Parameter(typeof(object), "source");
                var expressions = delegateParams.Select(arg => Expression.Parameter(arg, arg.Name)).ToArray();
                Expression returnExpression = Expression.Call(Expression.Convert(sourceParameter, source), methodInfo, expressions.Cast<Expression>());
                if (methodInfo.ReturnType != GetFuncDelegateReturnType<TDelegate>())
                {
                    if (methodInfo.ReturnType != typeof(void) && !methodInfo.ReturnType.IsClass && !methodInfo.ReturnType.IsPrimitive)
                        returnExpression = Expression.Convert(returnExpression, typeof(object));
                    else if (methodInfo.ReturnType != typeof(void))
                        returnExpression = Expression.Convert(returnExpression, GetFuncDelegateReturnType<TDelegate>());
                }
                var lambdaParams = new[] { sourceParameter }.Concat(expressions).ToArray();
                deleg = Expression.Lambda(returnExpression, lambdaParams).Compile();
            }
            return deleg as TDelegate;
        }
        
        public static TDelegate MethodEmit<TDelegate>(MethodInfo methodInfo) where TDelegate : class
        {
            Type[] parameterTypes;
            parameterTypes = GetFuncDelegateArguments<TDelegate>();
            System.Reflection.Emit.DynamicMethod m = new System.Reflection.Emit.DynamicMethod(
                "call_" + methodInfo.Name, GetFuncDelegateReturnType<TDelegate>(), parameterTypes, methodInfo.DeclaringType, true);
            System.Reflection.Emit.ILGenerator cg = m.GetILGenerator();

            for (int i = 0; i < parameterTypes.Length; i++)
            {
                switch (i)
                {
                    case 0:
                        cg.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                        break;
                    case 1:
                        cg.Emit(System.Reflection.Emit.OpCodes.Ldarg_1);
                        break;
                    case 2:
                        cg.Emit(System.Reflection.Emit.OpCodes.Ldarg_2);
                        break;
                    case 3:
                        cg.Emit(System.Reflection.Emit.OpCodes.Ldarg_3);
                        break;
                    default:
                        cg.Emit(System.Reflection.Emit.OpCodes.Ldarg, i);
                        break;
                }
                if (methodInfo.IsStatic || i > 0)
                {
                    Type parameterType = methodInfo.GetParameters()[i - (methodInfo.IsStatic ? 0 : 1)].ParameterType;
                    if (parameterTypes[i] == typeof(object) && parameterType != typeof(object))
                        cg.Emit(System.Reflection.Emit.OpCodes.Unbox_Any, parameterType);
                }
            }
            cg.Emit(System.Reflection.Emit.OpCodes.Callvirt, methodInfo);
            if (methodInfo.ReturnType != typeof(void) && (methodInfo.ReturnType.IsValueType && !methodInfo.ReturnType.IsPrimitive))
                cg.Emit(System.Reflection.Emit.OpCodes.Box, methodInfo.ReturnType);
            cg.Emit(System.Reflection.Emit.OpCodes.Ret);

            return m.CreateDelegate(typeof(TDelegate)) as TDelegate;
        }

        public static Func<object[], object> InstanceMethod(this Type source, string name, params Type[] paramTypes)
            => InstanceMethod<Func<object[], object>>(source, name, paramTypes);
        public static Func<object[], object> InstanceMethod(this Type source, MethodInfo methodInfo, params Type[] paramTypes)
            => InstanceMethod<Func<object[], object>>(source, methodInfo, paramTypes);

        public static Action<object[]> InstanceMethodVoid(this Type source, string name, params Type[] paramTypes)
            => InstanceMethod<Action<object[]>>(source, name, paramTypes);
        public static Action<object[]> InstanceMethodVoid(this Type source, MethodInfo methodInfo, params Type[] paramTypes)
            => InstanceMethod<Action<object[]>>(source, methodInfo, paramTypes);

        #endregion Methods

        internal static Type[] GetFuncDelegateArguments<TDelegate>() where TDelegate : class
        {
            if (typeof(TDelegate) == typeof(Action))
                return Type.EmptyTypes;
            if (!typeof(TDelegate).IsGenericType)
                return typeof(TDelegate).GetMethod("Invoke").GetParameters().Select(p => p.ParameterType).ToArray();
                //throw new ArgumentException();
            if (typeof(TDelegate).GetMethod("Invoke").ReturnType == typeof(void))
                return typeof(TDelegate).GetGenericArguments();
            return typeof(TDelegate).GetGenericArguments().Reverse().Skip(1).Reverse().ToArray();
        }
        private static Type GetFuncDelegateReturnType<TDelegate>() where TDelegate : class
        {
            if (!typeof(TDelegate).IsGenericType)
                return typeof(TDelegate).GetMethod("Invoke").ReturnType;
                //throw new ArgumentException();
            if (typeof(TDelegate).GetMethod("Invoke").ReturnType == typeof(void))
                return typeof(void);
            return typeof(TDelegate).GetGenericArguments().Last();
        }
    }
}
