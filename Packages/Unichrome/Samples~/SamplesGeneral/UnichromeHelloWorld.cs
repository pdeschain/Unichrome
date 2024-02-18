using System.Collections.Generic;
using System.Threading.Tasks;
using Unichrome.Embeddings.HuggingFace;

namespace Unichrome.Sample
{
    public class UnichromeHelloWorld
    {
        public async ValueTask Example()
        {
            //set up SentenceTransformers embeddings
            var embeddings = new HuggingFaceSentenceTransformerEmbeddings();

            //set up UnichromeDB in-memory, for easy prototyping. Can add persistence easily by specifying a path and calling Persist()!
            var db = new UnichromeDB(embeddings);
            
            string collectionName = "HelloWorld";
            
            // Create collection. GetCollection, GetOrCreateCollection, DeleteCollection also available!
            var collection = db.GetOrCreateCollection(collectionName);
            
            // Supports adding, updating, getting and deleting documents.
            var id = await collection.AddDocumentAsync(
                text: "This is document1",
                metadata:new Dictionary<string, string>
                {
                    {"source", "notion"},
                });
            
            //Metadata can be used to filter search results.
            // 
            
            // Query/search 2 most similar results. 
            var results = await collection.SearchAsync("This is a query document", 2);
            
            // Query/search 2 most similar results with a filter on the metadata.
            var metadataSearchResults = await collection.SearchAsync("This is a query document", 2,
                new []{("source", "==", "notion")});
            
            //...when you're done, (optionally) persist the db and dispose the embeddings
            
            db.Persist();
            embeddings?.Dispose();
        }
    }
}