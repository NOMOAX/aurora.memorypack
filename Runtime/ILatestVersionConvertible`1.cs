using MemoryPack;

namespace Aurora.MemoryPack
{
    /// <summary>
    /// 定义判断当前实例是否是最新版本的可读属性以及转换到最新版本的方法。
    /// </summary>
    /// <typeparam name="T">添加了 <see cref="MemoryPackUnionAttribute"/> 特性的接口类型或抽象类类型。</typeparam>
    public interface ILatestVersionConvertible<T> where T : ILatestVersionConvertible<T>
    {
        /// <summary>
        /// 更新当前实例到最新版本。
        /// </summary>
        /// <param name="value">当前实例。</param>
        /// <returns>如果 <paramref name="value"/> 更新到了最新版本，则为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
        bool ConvertToLatestVersion(ref T value);
    }
}
