using System.Collections.Generic;

namespace Unichrome.Storage
{
    public interface IStorageBackend
    {
        int NextId { get; }
        int Count { get; }
        bool TryGetDocument(int id, out UnichromeDocument document);
        UnichromeDocument GetDocument(int id);
        
        void AddDocument(UnichromeDocument document);
        void UpdateDocument(UnichromeDocument document);
        bool DeleteDocument(int id);
        void Clear();
        bool Contains(int id);
        void Persist(string path);
        IReadOnlyList<UnichromeDocument> GetDocuments();
        void DeserializeAndPopulate(string path);
    } 
}