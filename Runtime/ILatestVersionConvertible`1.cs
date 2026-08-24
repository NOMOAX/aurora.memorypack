using MemoryPack;

namespace Aurora.MemoryPack
{
    /// <summary>
    /// Defines a method for converting the current instance to the latest version.
    /// </summary>
    /// <typeparam name="T">The interface type or abstract class type decorated with the <see cref="MemoryPackUnionAttribute"/> attribute.</typeparam>
    public interface ILatestVersionConvertible<T> where T : ILatestVersionConvertible<T>
    {
        /// <summary>
        /// Updates the current instance to the latest version.
        /// </summary>
        /// <param name="value">The current instance.</param>
        /// <returns><see langword="true"/> if <paramref name="value"/> was updated to the latest version; otherwise, <see langword="false"/>.</returns>
        bool ConvertToLatestVersion(ref T value);
    }
}
