using System.Collections.Generic;
using System.IO;
using System.Linq;
using MemoryPack;
using Unichrome.Utils;

namespace Unichrome.Storage.MemoryPack
{
    [MemoryPackable]
    public partial class MemoryPackBackend : IStorageBackend
    {
        [MemoryPackInclude]
        private readonly Dictionary<int, UnichromeDocument> documents;

        [MemoryPackInclude]
        public int NextId { get; private set; } = 0;
        
        [MemoryPackIgnore]
        public int Count => documents.Count;

        public IReadOnlyList<UnichromeDocument> GetDocuments()
        {
            return documents.Values.ToArray();
        }

        public void DeserializeAndPopulate(string dbFilePath)
        {
            var bytes = File.ReadAllBytes(dbFilePath);
            var temp = MemoryPackSerializer.Deserialize<MemoryPackBackend>(bytes);
            
            this.documents.Clear();
            foreach (var (key, value) in temp.documents)
            {
                this.documents.Add(key, value);
            }
            this.NextId = temp.NextId;
        }

        internal MemoryPackBackend()
        {
            NextId = 0;
            documents = new Dictionary<int, UnichromeDocument>();
        }
        
        [MemoryPackConstructor]
        internal MemoryPackBackend(Dictionary<int, UnichromeDocument> documents, int nextId)
        {
            this.documents = documents;
            NextId = nextId;
        }
        
        public bool Contains(int id)
        {
            return documents.ContainsKey(id);
        }

        public bool TryGetDocument(int id, out UnichromeDocument document)
        {
            return documents.TryGetValue(id, out document);
        }
        
        public UnichromeDocument GetDocument(int id)
        {
            return documents[id];
        }

        /// <summary>
        /// CreationTime and LastUpdateTime will be set to UnichromeTime.now
        /// </summary>
        /// <param name="document"></param>
        public void AddDocument(UnichromeDocument document)
        {
            var d = document;
            d.Id = NextId;
            var t = UnichromeTime.now;
            d.CreationDateTime = t;
            d.ModificationDateTime = t;
            NextId++;
            
            documents.Add(d.Id, d);
        }
        
        /// <summary>
        /// LastUpdateTime will be set to UnichromeTime.now
        /// </summary>
        /// <param name="document"></param>
        public void UpdateDocument(UnichromeDocument document)
        {
            var d = document;
            d.ModificationDateTime = UnichromeTime.now;
            documents[document.Id] = d;
        }
        
        public bool DeleteDocument(int id)
        {
            if (documents.ContainsKey(id))
            {
                documents.Remove(id);
                return true;
            }

            return false;
        }
        
        public void Clear()
        {
            documents.Clear();
            NextId = 0;
        }
        
        public void Persist(string path)
        {
            var bytes = MemoryPackSerializer.Serialize(this);
            File.WriteAllBytes(path, bytes);
        }
    }
}