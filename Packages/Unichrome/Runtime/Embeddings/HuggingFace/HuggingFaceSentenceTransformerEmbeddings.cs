using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Sentis;
using SentenceSimilarityUtils;
using Unichrome.Embeddings;
using Unichrome.Sentis;

public class HuggingFaceSentenceTransformerEmbeddings : IEmbeddings
{
    private ModelAsset _modelAsset;
    private Model _runtimeModel;
    private IWorker _worker;
    private ITensorAllocator _allocator;
    private Ops _ops;
    
    /// <summary>
    /// Load the model on awake
    /// </summary>
    public HuggingFaceSentenceTransformerEmbeddings()
    {
        _modelAsset = Resources.Load<ModelAsset>("Model/model");
        
        // Load the ONNX model
        _runtimeModel = ModelLoader.Load(_modelAsset);

        // Create an engine and set the backend as GPU //GPUCompute
        _worker = WorkerFactory.CreateWorker(BackendType.CPU, _runtimeModel);

        // Create an allocator.
        _allocator = new TensorCachingAllocator();

        // Create an operator
        _ops = WorkerFactory.CreateOps(BackendType.GPUCompute, _allocator);
    }

    public void Dispose()
    {
        if (_modelAsset != null)
        {
            Resources.UnloadAsset(_modelAsset);
        }
            
        _worker?.Dispose();
        _allocator?.Dispose();
        _ops?.Dispose();
    }
    
    /// <summary>
    /// Encode the input
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public async ValueTask<TensorFloat> Encode(string input)
    {
        return await Encode(new[] {input});
    }

    /// <summary>
    /// Encode the input
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public async ValueTask<TensorFloat> Encode(IList<string> input)
    {
        // Step 1: Tokenize the sentences
        Dictionary<string, Tensor> inputSentencesTokensTensor = Utils.TokenizeInput(input);

        // Step 2: Compute embedding and get the output
        _worker.Execute(inputSentencesTokensTensor);
        
        // Step 3: Get the output from the neural network
        TensorFloat outputTensor = _worker.PeekOutput("last_hidden_state") as TensorFloat;

        outputTensor = await outputTensor.ComputeReadDataAsync();

        // Step 4: Perform pooling
        TensorFloat MeanPooledTensor = Utils.MeanPooling(inputSentencesTokensTensor["attention_mask"], outputTensor, _ops);

        // Step 5: Normalize the results
        TensorFloat NormedTensor = Utils.L2Norm(MeanPooledTensor, _ops);

        return NormedTensor;
    }


    /// <summary>
    /// We calculate the similarity scores between the input sequence (what the user typed) and the comparison
    /// sequences (the robot action list)
    /// This similarity score is simply the cosine similarity. It is calculated as the cosine of the angle between two vectors. 
    /// It is particularly useful when your texts are not the same length
    /// </summary>
    /// <param name="InputSequence"></param>
    /// <param name="ComparisonSequences"></param>
    /// <returns></returns>
    private TensorFloat SentenceSimilarityScores(TensorFloat InputSequence, TensorFloat ComparisonSequences)
    {
        TensorFloat SentenceSimilarityScores_ = _ops.MatMul2D(InputSequence, ComparisonSequences, false, true);
        return SentenceSimilarityScores_;
    }
    
    /// <summary>
    /// Get the most similar action and its index given the player input
    /// </summary>
    /// <param name="inputSentence"></param>
    /// <param name="comparisonSentences"></param>
    /// <returns></returns>
    public async ValueTask<Tuple<int, float>> RankSimilarityScores(string inputSentence, string[] comparisonSentences)
    {
        // Step 1: Transform string and string[] to lists
        List<string> InputSentences = new List<string>();
        List<string> ComparisonSentences = new List<string>();

        InputSentences.Add(inputSentence);
        ComparisonSentences = comparisonSentences.ToList();

        // Step 2: Encode the input sentences and comparison sentences
        TensorFloat NormEmbedSentences = await Encode(InputSentences);
        TensorFloat NormEmbedComparisonSentences = await Encode(ComparisonSentences);

        // Calculate the similarity score of the player input with each action
        TensorFloat scores = SentenceSimilarityScores(NormEmbedSentences, NormEmbedComparisonSentences);
        scores.MakeReadable(); // Be able to read this tensor

        // Helper to return only best score and index
        TensorInt scoreIndex = _ops.ArgMax(scores, 1, true);
        scoreIndex.MakeReadable();

        int scoreIndexInt = scoreIndex[0];
        scores.MakeReadable();
        float score = scores[scoreIndexInt];

        // Return the similarity score and the action index
        return Tuple.Create(scoreIndexInt, score);
    }
}