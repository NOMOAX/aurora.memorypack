using System;
using System.Collections.Generic;
using Aurora.Collections;
using MemoryPack;

namespace Aurora.MemoryPack
{
    /// <summary>
    /// 提供对 <see cref="MemoryPackUnionAttribute"/> 包含信息的封装。
    /// </summary>
    public readonly struct UnionInfo : IComparable<UnionInfo>, IEquatable<UnionInfo>
    {
        /// <summary>
        /// 被 <see cref="MemoryPackUnionAttribute"/> 修饰的类型。
        /// </summary>
        public readonly Type UnionType;

        /// <summary>
        /// <see cref="MemoryPackUnionAttribute.Tag"/>。
        /// </summary>
        public readonly ushort UnionTargetTag;

        /// <summary>
        /// <see cref="MemoryPackUnionAttribute.Type"/>。
        /// </summary>
        public readonly Type UnionTargetType;

        /// <summary>
        /// 初始化 <see cref="UnionInfo"/> 结构的新实例。
        /// </summary>
        /// <param name="unionType">添加了 <see cref="MemoryPackUnionAttribute"/> 特性的类型。</param>
        /// <param name="unionTargetTag"><see cref="MemoryPackUnionAttribute.Tag"/>。</param>
        /// <param name="targetType"><see cref="MemoryPackUnionAttribute.Type"/>。</param>
        /// <exception cref="ArgumentNullException"><paramref name="unionType"/> 或 <see cref="UnionTargetType"/> 为 <see langword="null"/>。</exception>
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
