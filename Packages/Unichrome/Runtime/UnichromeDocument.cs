using System;
using System.Collections.Generic;
using MemoryPack;
using Unity.Sentis;

namespace Unichrome
{
    /// <summary>
    /// Represents a Unichrome document, providing properties for identifying and describing the document.
    /// </summary>
    [MemoryPackable]
    public partial struct UnichromeDocument
    {
        /// <summary>
        /// The unique identifier for this Unichrome document.
        /// </summary>
        public int Id;

        /// <summary>
        /// The text content of this Unichrome document.
        /// </summary>
        public string Text;

        /// <summary>
        /// A dictionary holding metadata for this Unichrome document. The metadata is a collection of key-value pairs where both the key and value are strings.
        /// </summary>
        public Dictionary<string, string> Metadata;

        /// <summary>
        /// A vector representation of this Unichrome document. The vector is an array of floats.
        /// </summary>
        public float[] Vector;


        private TensorFloat _vectorTensor;
        [MemoryPackIgnore]
        public TensorFloat VectorTensor => _vectorTensor ??= new TensorFloat(new TensorShape(Vector.Length), Vector);

        /// <summary>
        /// The date and time when this Unichrome document was created.
        /// </summary>
        public DateTime CreationDateTime;

        /// <summary>
        /// The date and time when this Unichrome document was last updated.
        /// </summary>
        public DateTime ModificationDateTime;

        /// <summary>
        /// Initializes a new instance of the UnichromeDocument struct, specifying all property values.
        /// </summary>
        /// <param name="id">The unique identifier for the document.</param>
        /// <param name="text">The text content of the document.</param>
        /// <param name="metadata">The metadata for the document.</param>
        /// <param name="vector">The vector representation of the document.</param>
        /// <param name="creationDateTime">The date and time when the document was created.</param>
        /// <param name="modificationDateTime">The date and time when the document was last updated.</param>
        public UnichromeDocument(int id, string text, Dictionary<string, string> metadata, float[] vector,
            DateTime creationDateTime, DateTime modificationDateTime)
        {
            Id = id;
            Text = text;
            Metadata = metadata;
            Vector = vector;
            CreationDateTime = creationDateTime;
            ModificationDateTime = modificationDateTime;
            
            _vectorTensor = null;
        }
    }
}