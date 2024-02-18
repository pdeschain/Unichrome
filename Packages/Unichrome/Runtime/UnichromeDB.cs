using System;
using System.Collections.Generic;
using System.IO;
using Unichrome.Embeddings; 
using Unichrome.Storage.MemoryPack;

namespace Unichrome
{
    /// <summary>
    /// Represents a Unichrome database, providing functionalities for managing
    /// Unichrome collections and performing operations related to the embeddings.
    /// </summary>
    public sealed class UnichromeDB
    {
        /// <summary>
        /// Holds all the collections in the Unichrome database.
        /// </summary>
        private readonly Dictionary<string, UnichromeCollection> collections = new Dictionary<string, UnichromeCollection>();
       
        /// <summary>
        /// An instance of the IEmbeddings interface representing the vector embeddings.
        /// </summary>
        private readonly IEmbeddings embeddings;

        /// <summary>
        /// The path where the Unichrome database is stored. If the database is in memory, Path is null.
        /// </summary>
        public readonly string Path;
        
        /// <summary>
        /// Gets the embeddings instance associated with this Unichrome database.
        /// </summary>
        public IEmbeddings Embeddings => embeddings;
        
        /// <summary>
        /// Checks if the Unichrome database is stored in memory.
        /// </summary>
        public bool IsInMemory => Path == null;

        /// <summary>
        /// Initializes a new instance of the UnichromeDB class, specifying the embeddings to use and an optional path.
        /// </summary>
        /// <param name="embeddings">The embeddings instance to use.</param>
        /// <param name="path">Optional path for database storage.</param>
        public UnichromeDB(IEmbeddings embeddings, string path = null)
        {
            this.Path = path;
            this.embeddings = embeddings; ;
            // we want to lazy load collections, so we don't load them here.
        }

        /// <summary>
        /// Gets a Unichrome collection from the database by name.
        /// </summary>
        /// <param name="name">The name of the collection.</param>
        /// <returns>The requested Unichrome collection.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the collection does not exist.</exception>
        public UnichromeCollection GetCollection(string name)
        {
            if (collections.TryGetValue(name, out var collection))
            {
                return collection;
            }
            
            if (!IsInMemory)
            {
                // try load from file  
                var filePath = System.IO.Path.Combine(Path, $"{name}.db");
          
                if (File.Exists(filePath))
                {
                    collection = UnichromeCollectionFactory.Create(this, name);
                    collections[name] = collection;
                    return collection;
                }
            }

            throw new KeyNotFoundException($"Collection '{name}' does not exist.");
        }
        
        /// <summary>
        /// Gets a Unichrome collection from the database by name, or creates a new one if it does not exist.
        /// </summary>
        /// <param name="name">The name of the collection.</param>
        /// <returns>The requested Unichrome collection, or a new collection if it does not exist.</returns>
        public UnichromeCollection GetOrCreateCollection(string name)
        {
            if (collections.TryGetValue(name, out var collection))
            {
                return collection;
            }
            
            if (!IsInMemory)
            {
                // try load from file  
                var filePath = System.IO.Path.Combine(Path, $"{name}.db");
          
                if (File.Exists(filePath))
                {
                    collection = UnichromeCollectionFactory.Create(this, name);
                    collections[name] = collection;
                    return collection;
                }
            }

            collection = UnichromeCollectionFactory.Create(this, name, new MemoryPackBackend());
            collections[name] = collection;
            return collection;
        }

        /// <summary>
        /// Creates a new Unichrome collection in the database.
        /// </summary>
        /// <param name="name">The name of the new collection.</param>
        /// <returns>The newly created Unichrome collection.</returns>
        /// <exception cref="ArgumentException">Thrown if a collection with the specified name already exists.</exception>
        public UnichromeCollection CreateCollection(string name)
        {
            if (CollectionExists(name))
            {
                throw new ArgumentException($"Collection '{name}' already exists.");
            }
            
            var collection = UnichromeCollectionFactory.Create(this, name, new MemoryPackBackend());
            collections[name] = collection;
            return collection;
        }

        /// <summary>
        /// Deletes a Unichrome collection from the database by name.
        /// </summary>
        /// <param name="name">The name of the collection.</param>
        /// <exception cref="KeyNotFoundException">Thrown if the collection does not exist.</exception>
        public void DeleteCollection(string name)
        {
            if (CollectionExists(name))
            {
                var collection = GetCollection(name);
                collection.DeletePersistedStorage();
                collections.Remove(name);
            }
            else
            {
                throw new KeyNotFoundException($"Collection '{name}' does not exist.");
            }
        }

        /// <summary>
        /// Checks if a Unichrome collection exists in the database.
        /// </summary>
        /// <param name="name">The name of the collection.</param>
        /// <returns>True if the collection exists, false otherwise.</returns>
        public bool CollectionExists(string name)
        {
            if (collections.ContainsKey(name))
            {
                return true;
            }
            
            if (!IsInMemory)
            {
                var filePath = System.IO.Path.Combine(Path, $"{name}.db");
                return File.Exists(filePath);
            }

            return false;
        }

        /// <summary>
        /// Gets the size of a Unichrome collection in the database by name.
        /// </summary>
        /// <param name="name">The name of the collection.</param>
        /// <returns>The size of the requested Unichrome collection.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the collection does not exist.</exception>
        public int GetCollectionSize(string name)
        {
            if (CollectionExists(name))
            {
                var collection = GetCollection(name);
                return collection.Count;
            }
            
            throw new KeyNotFoundException($"Collection '{name}' does not exist.");
        }
        
        /// <summary>
        /// Persists all changes in the database to storage if the database is not in memory.
        /// </summary>
        public void Persist()
        {
            if (!IsInMemory)
            {
                foreach (var collection in collections.Values)
                {
                    collection.Persist();
                }
            }
        }
    }
}