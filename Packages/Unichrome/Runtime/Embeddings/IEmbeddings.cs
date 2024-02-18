using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Sentis;

namespace Unichrome.Embeddings
{
    public interface IEmbeddings : IDisposable
    {
        ValueTask<TensorFloat> Encode(IList<string> input);
        
        ValueTask<TensorFloat> Encode(string input);
    }
}