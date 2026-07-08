using System;
using System.Collections.Generic;
using System.Linq;
using Aurora.Diagnostics;
using MemoryPack;

namespace Aurora.MemoryPack
{
    /// <summary>
    /// 封装常用功能。
    /// </summary>
    public static class MemoryPackUtility
    {
        private static readonly Dictionary<Type, Dictionary<Type, ushort>> UnionTypeToUnionTargetTypes = new();

        private static readonly Func<MemoryPackUnionAttribute, Type> GetUnionTargetType = memoryPackUnionAttribute =>
            memoryPackUnionAttribute.Type;

        private static readonly Func<MemoryPackUnionAttribute, ushort> GetUnionTargetTag = memoryPackUnionAttribute =>
            memoryPackUnionAttribute.Tag;

        /// <summary>
        /// 克隆。
        /// </summary>
        /// <param name="memoryPackable">添加了 <see cref="MemoryPackableAttribute"/> 特性的类型的实例。</param>
        /// <typeparam name="T"><paramref name="memoryPackable"/> 的类型。</typeparam>
        /// <returns><paramref name="memoryPackable"/> 的深拷贝。</returns>
        /// <exception cref="ArgumentException"><typeparamref name="T"/> 没有添加 <see cref="MemoryPackableAttribute"/> 特性。</exception>
        public static T Clone<T>(T memoryPackable)
        {
            if (Attribute.GetCustomAttribute(typeof(T), typeof(MemoryPackableAttribute)) is null)
            {
                throw new ArgumentException($"类型 {typeof(T)} 没有添加 {nameof(MemoryPackableAttribute)} 特性");
            }
            return MemoryPackSerializer.Deserialize<T>(MemoryPackSerializer.Serialize(memoryPackable));
        }

        /// <summary>
        /// 更新 <paramref name="value"/> 到最新版本。
        /// </summary>
        /// <param name="value">要更新到最新版本的实例。</param>
        /// <typeparam name="T"><paramref name="value"/> 的类型。</typeparam>
        /// <returns>如果 <paramref name="value"/> 更新到了最新版本，则为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
        public static bool ConvertToLatestVersion<T>(ref T value) where T : ILatestVersionConvertible<T>
        {
            var oldValue  = value;
            var oldType   = value.GetType();
            var converted = value.ConvertToLatestVersion(ref value);
            if (converted)
            {
                var oldUnionTag = GetUnionTag(oldType, typeof(T));
                var type        = value.GetType();
                if (oldType == type)
                {
                    if (oldValue.Equals(value))
                    {
                        Log.I($"实例 ({oldType.Name}, UnionTag = {oldUnionTag}) 已升级");
                    }
                    else
                    {
                        Log.I($"实例 ({oldType.Name}, UnionTag = {oldUnionTag}) 已升级为相同类型的另一实例");
                    }
                }
                else
                {
                    var unionTag = GetUnionTag(type, typeof(T));
                    Log.I(
                        $"实例 ({oldType.Name}, UnionTag = {oldUnionTag}) 已升级为另一实例 ({type.Name}, UnionTag = {unionTag})"
                    );
                }
            }
            return converted;
        }

        /// <summary>
        /// 更新 <paramref name="values"/> 到最新版本。
        /// </summary>
        /// <param name="values">要更新到最新版本的数组。</param>
        /// <typeparam name="T"><paramref name="values"/> 的元素的类型。</typeparam>
        /// <returns>如果 <paramref name="values"/> 更新到了最新版本，则为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
        public static bool ConvertToLatestVersion<T>(T[] values) where T : ILatestVersionConvertible<T>
        {
            if (values is null)
            {
                return false;
            }
            var anyElementConvertedToLatestVersion = false;
            for (var i = 0; i < values.Length; i++)
            {
                ref var value = ref values[i];
                if (value is not null && ConvertToLatestVersion(ref value))
                {
                    anyElementConvertedToLatestVersion = true;
                }
            }
            if (anyElementConvertedToLatestVersion)
            {
                Log.I("数组中的至少一个元素已升级");
            }
            return anyElementConvertedToLatestVersion;
        }

        /// <summary>
        /// 更新 <paramref name="values"/> 到最新版本。
        /// </summary>
        /// <param name="values">要更新到最新版本的列表。</param>
        /// <typeparam name="T"><paramref name="values"/> 的元素的类型。</typeparam>
        /// <returns>如果 <paramref name="values"/> 更新到了最新版本，则为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
        public static bool ConvertToLatestVersion<T>(IList<T> values) where T : ILatestVersionConvertible<T>
        {
            if (values is null)
            {
                return false;
            }
            var anyElementConvertedToLatestVersion = false;
            for (var i = 0; i < values.Count; i++)
            {
                var value = values[i];
                if (value is not null && ConvertToLatestVersion(ref value))
                {
                    values[i]                          = value;
                    anyElementConvertedToLatestVersion = true;
                }
            }
            if (anyElementConvertedToLatestVersion)
            {
                Log.I("列表中的至少一个元素已升级");
            }
            return anyElementConvertedToLatestVersion;
        }

        /// <summary>
        /// 获取具体类型在其联合类型上定义的对应的 <see cref="MemoryPackUnionAttribute"/> 特性的标签。
        /// </summary>
        /// <param name="targetType">联合类型的目标类型（具体类型）。</param>
        /// <param name="unionType">联合类型。它是 <paramref name="targetType"/> 的接口类型或者抽象基类类型，带有 <see cref="MemoryPackUnionAttribute"/> 特性。</param>
        /// <returns></returns>
        public static ushort GetUnionTag(Type targetType, Type unionType)
        {
            if (!UnionTypeToUnionTargetTypes.TryGetValue(unionType, out var unionTargetTypes))
            {
                var memoryPackUnionAttributes =
                    (MemoryPackUnionAttribute[]) unionType.GetCustomAttributes(typeof(MemoryPackUnionAttribute), false);
                if (memoryPackUnionAttributes.Length == 0)
                {
                    throw new ArgumentException($"类型 {unionType} 没有添加 {nameof(MemoryPackUnionAttribute)} 特性");
                }
                unionTargetTypes = memoryPackUnionAttributes.ToDictionary(GetUnionTargetType, GetUnionTargetTag);
                UnionTypeToUnionTargetTypes.Add(unionType, unionTargetTypes);
            }
            return unionTargetTypes[targetType];
        }
    }
}
