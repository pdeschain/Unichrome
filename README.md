# Unichrome

**Unichrome** is an embeddable vector storage for Unity.

# Features

- **Embeddable**: Unichrome is designed to be embedded in your Unity project.
- **In-Memory Storage**: Unichrome supports in-memory storage.
- **Local Persistence**: Unichrome supports local persistence using ultra-fast MemoryPack.
- Embeddings:
  - **Sentis Integration**: Unichrome is designed to work with Unity Sentis for generation of embeddings.
  - **Local: HuggingFace Sharp-Transformers**: Unichrome is designed to work with HuggingFace Sharp-Transformers for generation of embeddings (using SentenceTransformer).
  - **API: OpenAI**: Unichrome supports OpenAI API for generation of embeddings. It would require an API key for OpenAI and additional settings for Azure OpenAI API.


# Dependency

- [Unity Sentis][sentis-link]
- [HuggingFace Transformers][huggingface-transformers-link]
- [MemoryPack][memorypack-link]

[sentis-link]: https://docs.unity3d.com/Packages/com.unity.sentis@1.2/manual/index.html
[memorypack-link]: https://github.com/Cysharp/MemoryPack
[huggingface-transformers-link]: https://github.com/huggingface/sharp-transformers

# How to Install

This project requires several git-based dependencies in the `package.json` which Unity does not support.
As such you will need to add them manually to your project's manifest file.

### Install via Manifest

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