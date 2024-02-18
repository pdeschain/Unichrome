using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unichrome.HNSW;
using Unichrome.Storage;
using Unity.Sentis;
using DefaultRandomGenerator = Unichrome.HNSW.DefaultRandomGenerator;
using Parameters = Unichrome.HNSW.Parameters;

namespace Unichrome
{
    /// <summary>
    /// Represents a collection within the Unichrome database system, supporting document storage, search, and visualization.
    /// </summary>
    public class UnichromeCollection
    {
        /// <summary>
        /// Represents a range of dates.
        /// </summary>
        public struct DateTimeRange
        {
            public DateTime Start;
            public DateTime End;

            public DateTimeRange(DateTime start, DateTime end)
            {
                Start = start;
                End = end;
            }

            public bool Contains(DateTime date)
            {
                return date >= Start && date <= End;
            }
        }

        /// <summary>
        /// Gets the associated database instance.
        /// </summary>
        public UnichromeDB DB { get; private set; }

        /// <summary>
        /// Gets the associated storage backend.
        /// </summary>
        public IStorageBackend Storage { get; private set; }

        /// <summary>
        /// The name of the collection.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// Gets the count of active documents in the collection.
        /// </summary>
        private SmallWorld<UnichromeDocument, float> world;

        /// <summary>
        /// Gets the count of documents in the collection.
        /// </summary>
        public int Count => Storage.Count;

        internal UnichromeCollection(UnichromeDB db, string name, IStorageBackend storageBackend)
        {
            this.DB = db;
            this.Name = name;
            
            //asserting that the storage backend is not null in a non-unity-dependent way
            if (storageBackend == null)
            {
                throw new ArgumentNullException(nameof(storageBackend));
            }
            
            Storage = storageBackend;
            
            if (DB.IsInMemory)
            {
                var parameters = new Parameters();
                world = new SmallWorld<UnichromeDocument, float>(CosineDistanceNonOptimized,
                    DefaultRandomGenerator.Instance,
                    parameters);
            }
            else
            {
                var dbFilePath = Path.Combine(DB.Path, $"{name}.db");
                var hnswFilePath = Path.Combine(DB.Path, $"{name}.hnsw");

                if (File.Exists(dbFilePath))
                {
                    Storage.DeserializeAndPopulate(dbFilePath);

                    if (File.Exists(hnswFilePath))
                    {
                        world = SmallWorld<UnichromeDocument, float>.DeserializeGraph(
                            File.ReadAllBytes(hnswFilePath),
                            Storage.GetDocuments(),
                            CosineDistanceSIMD,
                            DefaultRandomGenerator.Instance
                        );
                    }
                }
                else
                {
                    world = new SmallWorld<UnichromeDocument, float>(CosineDistanceSIMD,
                        DefaultRandomGenerator.Instance,
                        new Parameters());
                    world.AddItems(Storage.GetDocuments());

                    Persist();
                }
            }
        }

        /// <summary>
        /// Persists the current state of the collection and its associated graph.
        /// </summary>
        public void Persist()
        {
            if (DB.IsInMemory)
            {
                return;
            }

            var dbFilePath = Path.Combine(DB.Path, $"{Name}.db");
            var hnswFilePath = Path.Combine(DB.Path, $"{Name}.hnsw");

            //serialize the graph and the storage
            var worldBytes = world.SerializeGraph();
            File.WriteAllBytes(hnswFilePath, worldBytes);

            Storage.Persist(dbFilePath);
        }

        /// <summary>
        /// Adds a document with its associated vector representation and optional metadata to the collection.
        /// </summary>
        /// <returns>The ID of the added document.</returns>
        public int AddDocument(string text, TensorFloat vector, Dictionary<string, string> metadata = null)
        {
            var document = UnichromeDocumentFactory.Create(Storage, text, metadata, vector);
            Storage.AddDocument(document);
            world.AddItems(new[] { document });
            return document.Id;
        }

        /// <summary>
        /// Adds multiple documents to the collection.
        /// </summary>
        /// <returns>The IDs of the added documents.</returns>
        public async ValueTask<IList<int>> AddDocumentsAsync(IList<string> texts, IList<Dictionary<string, string>> metadatas = null)
        {
            var documents = new List<UnichromeDocument>(texts.Count);
            var ids = new List<int>(texts.Count);
            for (var index = 0; index < texts.Count; index++)
            {
                var text = texts[index];
                var metadata = metadatas?[index];
                var vector = await DB.Embeddings.Encode(text);
                var doc = UnichromeDocumentFactory.Create(Storage, text, metadata, vector);
                documents.Add(doc);
                Storage.AddDocument(doc);
                ids.Add(doc.Id);
            }

            world.AddItems(documents);

            return ids;
        }

        /// <summary>
        /// Add a document to the collection, encoding its text with the default embedding model.
        /// </summary>
        /// <returns>The ID of the added document.</returns>
        public async ValueTask<int> AddDocumentAsync(string text, Dictionary<string, string> metadata = null)
        {
            var vector = await DB.Embeddings.Encode(text);
            return AddDocument(text, vector, metadata);
        }

        /// <summary>
        /// Attempts to retrieve a document from the collection by its ID.
        /// </summary>
        /// <returns>True if the document was found; otherwise, false.</returns>
        public bool TryGetDocument(int id, out UnichromeDocument document)
        {
            return Storage.TryGetDocument(id, out document);
        }

        /// <summary>
        /// Retrieves a document from the collection by its ID.
        /// Throws an exception if the document is not found.
        /// </summary>
        public UnichromeDocument GetDocument(int id)
        {
            return Storage.GetDocument(id);
        }

        /// <summary>
        /// Deletes a document from the collection by its ID.
        /// </summary>
        public void DeleteDocument(int id)
        {
            Storage.DeleteDocument(id);
            
            world = new SmallWorld<UnichromeDocument, float>(CosineDistanceNonOptimized,
                DefaultRandomGenerator.Instance,
                new Parameters());

            world.AddItems(Storage.GetDocuments());
        }
        
        /// <summary>
        /// Updates a document in the collection by its ID.
        /// </summary>
        public async ValueTask UpdateDocumentAsync(int id, string text, Dictionary<string, string> metadata = null)
        {
            var existingDocument = Storage.GetDocument(id);
            
            if(existingDocument.Text != text)
            {
                var vector = await DB.Embeddings.Encode(text);
                existingDocument.Text = text;
                existingDocument.Vector = vector.ToReadOnlyArray();
            }
            
            Storage.UpdateDocument(existingDocument);
            
            world = new SmallWorld<UnichromeDocument, float>(CosineDistanceNonOptimized,
                DefaultRandomGenerator.Instance,
                new Parameters());
            
            world.AddItems(Storage.GetDocuments());
        }

        /// <summary>
        /// Searches for the K nearest neighbors of a given text in the collection, with optional metadata and date filtering.
        /// </summary>
        /// <param name="searchString">The search query text.</param>
        /// <param name="k">The number of nearest neighbors to retrieve (default is 10).</param>
        /// <param name="metadataFilters">Optional list of filter conditions for metadata.</param>
        /// <param name="createdDateFilter">Optional filter condition for the creation date.</param>
        /// <param name="modifiedDateMetadataFilter">Optional filter condition for the modification date.</param>
        /// <returns>A list of matching document-distance pairs that satisfy the filters, sorted by distance.</returns>
        /// <remarks>
        /// Metadata filtering operations allowed:
        /// - "==": Checks if the field value is equal to the specified value. Works for all field types.
        /// - "!=": Checks if the field value is not equal to the specified value. Works for all field types.
        /// - "<": Checks if the field value is less than the specified value. Works for numeric and date fields.
        /// - "<=": Checks if the field value is less than or equal to the specified value. Works for numeric and date fields.
        /// - ">": Checks if the field value is greater than the specified value. Works for numeric and date fields.
        /// - ">=": Checks if the field value is greater than or equal to the specified value. Works for numeric and date fields.
        /// - "contains": Checks if the field value contains the specified value as a substring. Works for string fields.
        /// </remarks>
        public async ValueTask<List<(UnichromeDocument, float)>> SearchAsync(
            string searchString,
            int k = 10,
            (string, string, string)[] metadataFilters = null,
            DateTimeRange? createdDateFilter = null,
            DateTimeRange? modifiedDateMetadataFilter = null)
        {
            var vector = await DB.Embeddings.Encode(searchString);
            return Search(vector, k, metadataFilters, createdDateFilter, modifiedDateMetadataFilter);
        }

        /// <summary>
        /// 
        public List<(UnichromeDocument, float)> Search(
            TensorFloat searchVector,
            int k = 10,
            (string, string, string)[] metadataFilters = null,
            DateTimeRange? createdDateFilter = null,
            DateTimeRange? modifiedDateMetadataFilter = null)
        {
            var searchDoc = new UnichromeDocument()
            {
                Vector = searchVector.ToReadOnlyArray()
            };

            var neighbors = world.KNNSearch(searchDoc, k);

            var results = new List<(UnichromeDocument, float)>();

            foreach (var neighbor in neighbors)
            {
                var index = neighbor.Id;
                var distance = neighbor.Distance;
                var document = GetDocument(index);

                // Apply creation date filter if provided
                if (createdDateFilter != null && !createdDateFilter.Value.Contains(document.CreationDateTime))
                {
                    continue; // Skip this document if it doesn't match the creation date filter
                }

                // Apply modification date filter if provided
                if (modifiedDateMetadataFilter != null &&
                    !modifiedDateMetadataFilter.Value.Contains(document.ModificationDateTime))
                {
                    continue; // Skip this document if it doesn't match the modification date filter
                }

                // Apply metadata filters if provided
                if (metadataFilters != null)
                {
                    if (!MetadataMatchesFilters(document, metadataFilters))
                    {
                        continue; // Skip this document if it doesn't match the metadata filters
                    }
                }

                results.Add((document, distance));
            }

            results.Sort((x, y) => x.Item2.CompareTo(y.Item2));

            return results;
        }

        private bool MetadataMatchesFilters(UnichromeDocument document,  (string, string, string)[]  metadataFilters)
        {
            foreach (var filter in metadataFilters)
            {
                if (!document.Metadata.TryGetValue(filter.Item1, out var metadataValue))
                {
                    return false; // Skip this document if the field doesn't exist
                }

                if (!ApplyFilterCondition(metadataValue, filter))
                {
                    return false; // Skip this document if it doesn't match the filter condition
                }
            }

            return true;
        }

        private bool ApplyFilterCondition(string fieldValue,  (string, string, string) metadataFilter)
        {
            double.TryParse(fieldValue, out double numericValue);

            switch (metadataFilter.Item2)
            {
                case "==":
                    return fieldValue.Equals(metadataFilter.Item3);
                case "!=":
                    return !fieldValue.Equals(metadataFilter.Item3);
                case "<":
                    return numericValue < double.Parse(metadataFilter.Item3);
                case "<=":
                    return numericValue <= double.Parse(metadataFilter.Item3);
                case ">":
                    return numericValue > double.Parse(metadataFilter.Item3);
                case ">=":
                    return numericValue >= double.Parse(metadataFilter.Item3);
                case "contains":
                    return fieldValue.Contains(metadataFilter.Item3);
                default:
                    throw new InvalidOperationException($"Unsupported operation: {metadataFilter.Item2}");
            }
        }


        /// <summary>
        /// Deletes the persisted storage files associated with the collection.
        /// </summary>
        internal void DeletePersistedStorage()
        {
            var dbPath = Path.Combine(DB.Path, $"{Name}.db");
            var hnswPath = Path.Combine(DB.Path, $"{Name}.hnsw");

            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }

            if (File.Exists(hnswPath))
            {
                File.Delete(hnswPath);
            }
        }


        #region Distance Function Wrappers

        private static float CosineDistanceNonOptimized(UnichromeDocument a, UnichromeDocument b)
        {
            var u = a.Vector;
            var v = b.Vector;

            return CosineDistance.NonOptimized(u, v);
        }

        private static float CosineDistanceForUnits(UnichromeDocument a, UnichromeDocument b)
        {
            var u = a.Vector;
            var v = b.Vector;

            return CosineDistance.ForUnits(u, v);
        }

        private static float CosineDistanceSIMD(UnichromeDocument a, UnichromeDocument b)
        {
            var u = a.Vector;
            var v = b.Vector;

            return CosineDistance.SIMD(u, v);
        }

        private static float CosineDistanceSIMDForUnits(UnichromeDocument a, UnichromeDocument b)
        {
            var u = a.Vector;
            var v = b.Vector;

            return CosineDistance.SIMDForUnits(u, v);
        }

        #endregion
    }
}