using System;
using System.Collections.Generic;
using System.Linq;
using Aurora.Diagnostics;
using MemoryPack;

namespace Aurora.MemoryPack
{
    /// <summary>
    /// Wraps common functionality.
    /// </summary>
    public static class MemoryPackUtility
    {
        private static readonly Dictionary<Type, Dictionary<Type, ushort>> UnionTypeToUnionTargetTypes = new();

        private static readonly Func<MemoryPackUnionAttribute, Type> GetUnionTargetType = memoryPackUnionAttribute =>
            memoryPackUnionAttribute.Type;

        private static readonly Func<MemoryPackUnionAttribute, ushort> GetUnionTargetTag = memoryPackUnionAttribute =>
            memoryPackUnionAttribute.Tag;

        /// <summary>
        /// Clones.
        /// </summary>
        /// <param name="memoryPackable">An instance of a type decorated with the <see cref="MemoryPackableAttribute"/> attribute. This parameter is passed by reference.</param>
        /// <typeparam name="T">The type of <paramref name="memoryPackable"/>.</typeparam>
        /// <returns>A deep copy of <paramref name="memoryPackable"/>.</returns>
        /// <remarks><typeparamref name="T"/> can only be a type decorated with the <see cref="MemoryPackableAttribute"/> attribute, or a collection of the aforementioned types (arrays, lists, dictionaries, etc.; see the MemoryPack documentation for the supported cases).</remarks>
        public static T Clone<T>(in T memoryPackable)
        {
            return MemoryPackSerializer.Deserialize<T>(MemoryPackSerializer.Serialize(in memoryPackable));
        }

        /// <summary>
        /// Updates <paramref name="value"/> to the latest version.
        /// </summary>
        /// <param name="value">The instance to update to the latest version.</param>
        /// <typeparam name="T">The type of <paramref name="value"/>.</typeparam>
        /// <returns><see langword="true"/> if <paramref name="value"/> was updated to the latest version; otherwise, <see langword="false"/>.</returns>
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
                        Log.I($"Instance ({oldType.Name}, UnionTag = {oldUnionTag}) has been upgraded");
                    }
                    else
                    {
                        Log.I(
                            $"Instance ({oldType.Name}, UnionTag = {oldUnionTag}) has been upgraded to another instance of the same type"
                        );
                    }
                }
                else
                {
                    var unionTag = GetUnionTag(type, typeof(T));
                    Log.I(
                        $"Instance ({oldType.Name}, UnionTag = {oldUnionTag}) has been upgraded to another instance ({type.Name}, UnionTag = {unionTag})"
                    );
                }
            }
            return converted;
        }

        /// <summary>
        /// Updates <paramref name="values"/> to the latest version.
        /// </summary>
        /// <param name="values">The array to update to the latest version.</param>
        /// <typeparam name="T">The type of the elements of <paramref name="values"/>.</typeparam>
        /// <returns><see langword="true"/> if <paramref name="values"/> was updated to the latest version; otherwise, <see langword="false"/>.</returns>
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
                Log.I("At least one element in the array has been upgraded");
            }
            return anyElementConvertedToLatestVersion;
        }

        /// <summary>
        /// Updates <paramref name="values"/> to the latest version.
        /// </summary>
        /// <param name="values">The list to update to the latest version.</param>
        /// <typeparam name="T">The type of the elements of <paramref name="values"/>.</typeparam>
        /// <returns><see langword="true"/> if <paramref name="values"/> was updated to the latest version; otherwise, <see langword="false"/>.</returns>
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
                Log.I("At least one element in the list has been upgraded");
            }
            return anyElementConvertedToLatestVersion;
        }

        /// <summary>
        /// Gets the tag of the <see cref="MemoryPackUnionAttribute"/> attribute defined for the concrete type on its union type.
        /// </summary>
        /// <param name="targetType">The target type of the union type (the concrete type).</param>
        /// <param name="unionType">The union type. It is the interface type or abstract base class type of <paramref name="targetType"/>, decorated with the <see cref="MemoryPackUnionAttribute"/> attribute.</param>
        /// <returns>The union tag of <paramref name="targetType"/>.</returns>
        public static ushort GetUnionTag(Type targetType, Type unionType)
        {
            if (!UnionTypeToUnionTargetTypes.TryGetValue(unionType, out var unionTargetTypes))
            {
                var memoryPackUnionAttributes =
                    (MemoryPackUnionAttribute[])unionType.GetCustomAttributes(typeof(MemoryPackUnionAttribute), false);
                if (memoryPackUnionAttributes.Length == 0)
                {
                    throw new ArgumentException(
                        $"Type {unionType} is not decorated with the {nameof(MemoryPackUnionAttribute)} attribute"
                    );
                }
                unionTargetTypes = memoryPackUnionAttributes.ToDictionary(GetUnionTargetType, GetUnionTargetTag);
                UnionTypeToUnionTargetTypes.Add(unionType, unionTargetTypes);
            }
            return unionTargetTypes[targetType];
        }
    }
}
