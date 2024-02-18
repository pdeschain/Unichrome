using System.Collections.Generic;
using Unichrome.Storage;
using Unichrome.Utils;
using Unity.Sentis;

namespace Unichrome
{
    /// <summary>
    /// Provides a static method to create new instances of the UnichromeDocument struct.
    /// </summary>
    internal static class UnichromeDocumentFactory
    {
        /// <summary>
        /// Creates a new UnichromeDocument with specified text, metadata and vector, using a provided storage backend for generating the document's id. 
        /// The Creation and LastUpdate timestamps will be set to the current time.
        /// </summary>
        /// <param name="storageBackend">The IStorageBackend instance used to generate the next unique id for the document.</param>
        /// <param name="text">The text content of the document.</param>
        /// <param name="metadata">The metadata for the document. If null, an empty dictionary will be used.</param>
        /// <param name="vector">The vector representation of the document.</param>
        /// <returns>A new instance of UnichromeDocument.</returns>
        public static UnichromeDocument Create(IStorageBackend storageBackend, string text, Dictionary<string, string> metadata, TensorFloat vector)
        {
            var t = UnichromeTime.now;
            metadata = metadata ?? new Dictionary<string, string>();
            return new UnichromeDocument(storageBackend.NextId, text, metadata, vector.ToReadOnlyArray(), t, t);
        }
    }
}