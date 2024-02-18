using System;
using MemoryPack;

namespace Unichrome.HNSW
{
    [MemoryPackable()]
    public partial class Parameters
    {
        public Parameters()
        {
            M = 10;
            LevelLambda = 1 / Math.Log(M);
            NeighbourHeuristic = NeighbourSelectionHeuristic.SelectSimple;
            ConstructionPruning = 200;
            ExpandBestSelection = false;
            KeepPrunedConnections = false;
            EnableDistanceCacheForConstruction = true;
            InitialDistanceCacheSize = 1024 * 1024;
            InitialItemsSize = 1024;
        }

        /// <summary>
        /// Gets or sets the parameter which defines the maximum number of neighbors in the zero and above-zero layers.
        /// The maximum number of neighbors for the zero layer is 2 * M.
        /// The maximum number of neighbors for higher layers is M.
        /// </summary>
        public int M { get; set; }

        /// <summary>
        /// Gets or sets the max level decay parameter. https://en.wikipedia.org/wiki/Exponential_distribution See 'mL' parameter in the HNSW article.
        /// </summary>
        public double LevelLambda { get; set; }

        /// <summary>
        /// Gets or sets parameter which specifies the type of heuristic to use for best neighbours selection.
        /// </summary>
        public NeighbourSelectionHeuristic NeighbourHeuristic { get; set; }

        /// <summary>
        /// Gets or sets the number of candidates to consider as neighbours for a given node at the graph construction phase. See 'efConstruction' parameter in the article.
        /// </summary>
        public int ConstructionPruning { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to expand candidates if <see cref="NeighbourSelectionHeuristic.SelectHeuristic"/> is used. See 'extendCandidates' parameter in the article.
        /// </summary>
        public bool ExpandBestSelection { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to keep pruned candidates if <see cref="NeighbourSelectionHeuristic.SelectHeuristic"/> is used. See 'keepPrunedConnections' parameter in the article.
        /// </summary>
        public bool KeepPrunedConnections { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to cache calculated distances at graph construction time.
        /// </summary>
        public bool EnableDistanceCacheForConstruction { get; set; }

        /// <summary>
        /// Gets or sets a the initial distance cache size. 
        /// Note: This value is reset to 0 on deserialization to avoid allocating the distance cache for pre-built graphs.
        /// </summary>
        public int InitialDistanceCacheSize { get; set; }

        /// <summary>
        /// Gets or sets a the initial size of the Items list
        /// </summary>
        public int InitialItemsSize { get; set; }
    }
}