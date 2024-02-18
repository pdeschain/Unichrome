using Unichrome.Storage;

namespace Unichrome
{
    /// <summary>
    /// Provides a factory method for creating instances of UnichromeCollection.
    /// </summary>
    internal static class UnichromeCollectionFactory
    {
        /// <summary>
        /// Creates and returns a new instance of UnichromeCollection.
        /// </summary>
        /// <param name="db">The UnichromeDB instance associated with the collection.</param>
        /// <param name="name">The name of the collection.</param>
        /// <param name="storageBackend">Optional. The storage backend to be used by the collection. If not provided, a default storage backend may be used.</param>
        /// <returns>A new instance of UnichromeCollection.</returns>
        public static UnichromeCollection Create(UnichromeDB db, string name, IStorageBackend storageBackend = null)
        {
            return new UnichromeCollection(db, name, storageBackend);
        }
    }
}