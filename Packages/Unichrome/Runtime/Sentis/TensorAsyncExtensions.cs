using System.Threading.Tasks;
using Unity.Sentis;

namespace Unichrome.Sentis
{
    public static class TensorAsyncExtensions
    {
        public static async ValueTask<T> BurstReadDataAsync<T>(this T output)
            where T:Tensor
        {
            var burstData = BurstTensorData.Pin(output);
            
            while (!burstData.IsAsyncReadbackRequestDone())
            {
                await Task.Yield();
            }
            return output;
        }
        
        public static async ValueTask<T> ComputeReadDataAsync<T>(this T output)
            where T:Tensor
        {
            output.AsyncReadbackRequest();
            while (!output.IsAsyncReadbackRequestDone())
            {
                await Task.Yield();
            }
            
            return output;
        }
    }
}