using System;
using System.Collections.Generic;
using Aurora.Collections;
using MemoryPack;

namespace Aurora.MemoryPack
{
    /// <summary>
    /// Provides a wrapper for the information contained in <see cref="MemoryPackUnionAttribute"/>.
    /// </summary>
    public readonly struct UnionInfo : IComparable<UnionInfo>, IEquatable<UnionInfo>
    {
        /// <summary>
        /// The type decorated with the <see cref="MemoryPackUnionAttribute"/> attribute.
        /// </summary>
        public readonly Type UnionType;

        /// <summary>
        /// <see cref="MemoryPackUnionAttribute.Tag"/>.
        /// </summary>
        public readonly ushort UnionTargetTag;

        /// <summary>
        /// <see cref="MemoryPackUnionAttribute.Type"/>.
        /// </summary>
        public readonly Type UnionTargetType;

        /// <summary>
        /// Initializes a new instance of the <see cref="UnionInfo"/> struct.
        /// </summary>
        /// <param name="unionType">The type decorated with the <see cref="MemoryPackUnionAttribute"/> attribute.</param>
        /// <param name="unionTargetTag"><see cref="MemoryPackUnionAttribute.Tag"/>.</param>
        /// <param name="targetType"><see cref="MemoryPackUnionAttribute.Type"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="unionType"/> or <see cref="UnionTargetType"/> is <see langword="null"/>.</exception>
        public UnionInfo(Type unionType, ushort unionTargetTag, Type targetType)
        {
            UnionType       = unionType ?? throw new ArgumentNullException(nameof(unionType));
            UnionTargetTag  = unionTargetTag;
            UnionTargetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
        }

        /// <inheritdoc />
        public int CompareTo(UnionInfo other)
        {
            var c = HashCodeComparer<Type>.Instance.Compare(UnionType, other.UnionType);
            if (c != 0)
            {
                return c;
            }
            c = Comparer<ushort>.Default.Compare(UnionTargetTag, other.UnionTargetTag);
            if (c != 0)
            {
                return c;
            }
            return HashCodeComparer<Type>.Instance.Compare(UnionTargetType, other.UnionTargetType);
        }

        /// <inheritdoc />
        public bool Equals(UnionInfo other)
        {
            return EqualityComparer<Type>.Default.Equals(UnionType, other.UnionType) &&
                   EqualityComparer<ushort>.Default.Equals(UnionTargetTag, other.UnionTargetTag) &&
                   EqualityComparer<Type>.Default.Equals(UnionTargetType, other.UnionTargetType);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{UnionType.Name} @ [MemoryPackUnion({UnionTargetTag}, typeof({UnionTargetType.Name}))]";
        }
    }
}
