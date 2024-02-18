# Unichrome 🚀

**Unichrome** is an embeddable vector storage for Unity.

# Features 🌟

- **Embeddable**: Seamlessly integrates with your Unity project 🛠️.
- **In-Memory Storage**: Offers swift in-memory data handling ⚡.
- **Local Persistence**: Boasts local data saving with the speedy MemoryPack 📦.
- **Embeddings**:
  - **Sentis Integration**: Perfectly pairs with Unity Sentis for embedding creation 🤝.
  - **Local: HuggingFace Sharp-Transformers**: Utilizes HuggingFace Sharp-Transformers for local embeddings (via SentenceTransformer) 🧠.
  - **(coming soon) API: OpenAI**: Connects with OpenAI API for embedding generation (needs API key & Azure settings) 🔑.

# Dependencies 📚

Props to:
 - [HNSW by Microsoft & Curiosity AI](https://github.com/curiosity-ai/hnsw-sharp) - top-notch work! 👏
 - [Sentis ML runtime by Unity Technologies](https://docs.unity3d.com/Packages/com.unity.sentis@1.2/manual/index.html) for local model inference 🧪
 - [MemoryPack](https://github.com/Cysharp/MemoryPack) for our primary storage backend 🗃️
 - [HuggingFace Sharp-Transformers](https://github.com/huggingface/sharp-transformers) for their SentenceTransformers embeddings 💬

# Installation Guide 🛠️

Unity's `package.json` doesn't play nice with git dependencies. You'll need to manually tweak your manifest file.

### Manifest Installation

```json
{
  "dependencies": {
    "com.magicforge.unichrome": "",
    "com.huggingface.transformers": "https://github.com/huggingface/sharp-transformers.git"
  }
}
```

MemoryPack will need to be added to your project too - the preferred way is to use a `.unitypackage` provided on the github of the project [here](https://github.com/Cysharp/MemoryPack?tab=readme-ov-file#unity). This will save you the need to manually manage the `System.Runtime.CompilerServices.Unsafe.dll` file in your project.


# How to Use

### Unichrome Hello World

```c#
using System.Collections.Generic;
using System.Threading.Tasks;
using Unichrome.Embeddings.SentenceTransformers;

namespace Unichrome.Sample
{
    public class UnichromeHelloWorld
    {
        public async ValueTask Example()
        {
            //set up SentenceTransformers embeddings
            var embeddings = new SentenceTransformerEmbeddings();

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
```