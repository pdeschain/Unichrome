// <copyright file="Graph.cs" company="Microsoft">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using MemoryPack;

namespace Unichrome.HNSW
{
    using static global::Unichrome.HNSW.EventSources;

    /// <summary>
    /// The implementation of a hierarchical small world graph.
    /// </summary>
    /// <typeparam name="TItem">The type of items to connect into small world.</typeparam>
    /// <typeparam name="TDistance">The type of distance between items (expects any numeric type: float, double, decimal, int, ...).</typeparam>
    
    [MemoryPackable()]
    internal partial class Graph<TItem, TDistance> where TDistance : struct, IComparable<TDistance>
    {
        [MemoryPackIgnore]
        public Func<TItem, TItem, TDistance> Distance;

        [MemoryPackInclude]
        internal Core<TItem, TDistance> GraphCore;

        [MemoryPackInclude]
        private Node? EntryPoint;

        [MemoryPackInclude]
        internal Parameters Parameters { get; }

        private long _version;

        /// <summary>
        /// Initializes a new instance of the <see cref="Graph{TItem, TDistance}"/> class.
        /// </summary>
        /// <param name="distance">The distance function.</param>
        /// <param name="parameters">The parameters of the world.</param>
        internal Graph(Func<TItem, TItem, TDistance> distance, Parameters parameters)
        {
            Distance = distance;
            Parameters = parameters;
        }
        
        [MemoryPackConstructor]
        internal Graph(Parameters parameters, Core<TItem, TDistance> graphcore, Node? entryPoint)
        {
            Parameters = parameters;
            GraphCore = graphcore;
            EntryPoint = entryPoint;
        }

        public void InitializeAfterDeserialization(Func<TItem, TItem, TDistance> distance)
        {
            Distance = distance;
            GraphCore.InitializeAfterDeserialization(Distance, Parameters);
        }

        /// <summary>
        /// Creates graph from the given items.
        /// Contains implementation of INSERT(hnsw, q, M, Mmax, efConstruction, mL) algorithm.
        /// Article: Section 4. Algorithm 1.
        /// </summary>
        /// <param name="items">The items to insert.</param>
        /// <param name="generator">The random number generator to distribute nodes across layers.</param>
        /// <param name="progressReporter">Interface to report progress </param>
        internal IReadOnlyList<int> AddItems(IReadOnlyList<TItem> items, IProvideRandomValues generator, IProgressReporter progressReporter)
        {
            if (items is null || !items.Any()) { return Array.Empty<int>(); }

            GraphCore ??= new Core<TItem, TDistance>(Distance, Parameters);

            int startIndex = GraphCore.Items.Count;

            var newIDs = GraphCore.AddItems(items, generator);

            var entryPoint = EntryPoint ?? GraphCore.Nodes[0];

            var searcher = new Searcher(GraphCore);
            Func<int, int, TDistance> nodeDistance = GraphCore.GetDistance;
            var neighboursIdsBuffer = new List<int>(GraphCore.Algorithm.GetM(0) + 1);

            for (int nodeId = startIndex; nodeId < GraphCore.Nodes.Count; ++nodeId)
            {
                var versionNow = Interlocked.Increment(ref _version);

                using (new ScopeLatencyTracker(GraphBuildEventSource.Instance?.GraphInsertNodeLatencyReporter))
                {
                    /*
                     * W ← ∅ // list for the currently found nearest elements
                     * ep ← get enter point for hnsw
                     * L ← level of ep // top layer for hnsw
                     * l ← ⌊-ln(unif(0..1))∙mL⌋ // new element’s level
                     * for lc ← L … l+1
                     *   W ← SEARCH-LAYER(q, ep, ef=1, lc)
                     *   ep ← get the nearest element from W to q
                     * for lc ← min(L, l) … 0
                     *   W ← SEARCH-LAYER(q, ep, efConstruction, lc)
                     *   neighbors ← SELECT-NEIGHBORS(q, W, M, lc) // alg. 3 or alg. 4
                     *     for each e ∈ neighbors // shrink connections if needed
                     *       eConn ← neighbourhood(e) at layer lc
                     *       if │eConn│ > Mmax // shrink connections of e if lc = 0 then Mmax = Mmax0
                     *         eNewConn ← SELECT-NEIGHBORS(e, eConn, Mmax, lc) // alg. 3 or alg. 4
                     *         set neighbourhood(e) at layer lc to eNewConn
                     *   ep ← W
                     * if l > L
                     *   set enter point for hnsw to q
                     */

                    // zoom in and find the best peer on the same level as newNode
                    var bestPeer = entryPoint;
                    var currentNode = GraphCore.Nodes[nodeId];
                    var currentNodeTravelingCosts = new TravelingCosts<int, TDistance>(nodeDistance, nodeId);
                    for (int layer = bestPeer.MaxLayer; layer > currentNode.MaxLayer; --layer)
                    {
                        searcher.RunKnnAtLayer(bestPeer.Id, currentNodeTravelingCosts, neighboursIdsBuffer, layer, 1, ref _version, versionNow);
                        bestPeer = GraphCore.Nodes[neighboursIdsBuffer[0]];
                        neighboursIdsBuffer.Clear();
                    }

                    // connecting new node to the small world
                    for (int layer = Math.Min(currentNode.MaxLayer, entryPoint.MaxLayer); layer >= 0; --layer)
                    {
                        searcher.RunKnnAtLayer(bestPeer.Id, currentNodeTravelingCosts, neighboursIdsBuffer, layer, Parameters.ConstructionPruning, ref _version, versionNow);
                        var bestNeighboursIds = GraphCore.Algorithm.SelectBestForConnecting(neighboursIdsBuffer, currentNodeTravelingCosts, layer);

                        for (int i = 0; i < bestNeighboursIds.Count; ++i)
                        {
                            int newNeighbourId = bestNeighboursIds[i];
                            versionNow = Interlocked.Increment(ref _version);
                            GraphCore.Algorithm.Connect(currentNode, GraphCore.Nodes[newNeighbourId], layer);
                            
                            versionNow = Interlocked.Increment(ref _version);
                            GraphCore.Algorithm.Connect(GraphCore.Nodes[newNeighbourId], currentNode, layer);

                            // if distance from newNode to newNeighbour is better than to bestPeer => update bestPeer
                            if (DistanceUtils.LowerThan(currentNodeTravelingCosts.From(newNeighbourId), currentNodeTravelingCosts.From(bestPeer.Id)))
                            {
                                bestPeer = GraphCore.Nodes[newNeighbourId];
                            }
                        }

                        neighboursIdsBuffer.Clear();
                    }

                    // zoom out to the highest level
                    if (currentNode.MaxLayer > entryPoint.MaxLayer)
                    {
                        entryPoint = currentNode;
                    }

                    // report distance cache hit rate
                    GraphBuildEventSource.Instance?.CoreGetDistanceCacheHitRateReporter?.Invoke(GraphCore.DistanceCacheHitRate);
                }
                progressReporter?.Progress(nodeId - startIndex, GraphCore.Nodes.Count - startIndex);
            }

            // construction is done
            EntryPoint = entryPoint;

            return newIDs;
        }

        /// <summary>
        /// Get k nearest items for a given one.
        /// Contains implementation of K-NN-SEARCH(hnsw, q, K, ef) algorithm.
        /// Article: Section 4. Algorithm 5.
        /// </summary>
        /// <param name="destination">The given node to get the nearest neighbourhood for.</param>
        /// <param name="k">The size of the neighbourhood.</param>
        /// <returns>The list of the nearest neighbours.</returns>
        internal IList<SmallWorld<TItem, TDistance>.KNNSearchResult> KNearest(TItem destination, int k)
        {
            if (EntryPoint is null) return null;

            int retries = 1_024;
            while (true)
            {
                var versionNow = Interlocked.Read(ref _version);

                try
                {
                    using (new ScopeLatencyTracker(GraphSearchEventSource.Instance?.GraphKNearestLatencyReporter))
                    {
                        // TODO: hack we know that destination id is -1.
                        TDistance RuntimeDistance(int x, int y)
                        {
                            int nodeId = x >= 0 ? x : y;
                            return Distance(destination, GraphCore.Items[nodeId]);
                        }

                        var bestPeer = EntryPoint.Value;
                        var searcher = new Searcher(GraphCore);
                        var destiantionTravelingCosts = new TravelingCosts<int, TDistance>(RuntimeDistance, -1);
                        var resultIds = new List<int>(k + 1);

                        int visitedNodesCount = 0;
                        for (int layer = EntryPoint.Value.MaxLayer; layer > 0; --layer)
                        {
                            visitedNodesCount += searcher.RunKnnAtLayer(bestPeer.Id, destiantionTravelingCosts, resultIds, layer, 1, ref _version, versionNow);
                            bestPeer = GraphCore.Nodes[resultIds[0]];
                            resultIds.Clear();
                        }

                        visitedNodesCount += searcher.RunKnnAtLayer(bestPeer.Id, destiantionTravelingCosts, resultIds, 0, k, ref _version, versionNow);
                        GraphSearchEventSource.Instance?.GraphKNearestVisitedNodesReporter?.Invoke(visitedNodesCount);

                        return resultIds.Select(id => new SmallWorld<TItem, TDistance>.KNNSearchResult(id, GraphCore.Items[id], RuntimeDistance(id, -1))).ToList();
                    }
                }
                catch (GraphChangedException)
                {
                    if(retries > 0)
                    {
                        retries--; 
                        continue;
                    }
                    throw;
                }
                catch(Exception)
                {
                    if (retries > 0)
                    {
                        retries--;
                        continue;
                    }
                    throw;
                }
            }
        }
        
        /// <summary>
        /// Prints edges of the graph.
        /// </summary>
        /// <returns>String representation of the graph's edges.</returns>
        internal string Print()
        {
            var buffer = new StringBuilder();
            for (int layer = EntryPoint.Value.MaxLayer; layer >= 0; --layer)
            {
                buffer.AppendLine($"[LEVEL {layer}]");
                BFS(GraphCore, EntryPoint.Value, layer, (node) =>
                {
                    var neighbours = string.Join(", ", node[layer]);
                    buffer.AppendLine($"({node.Id}) -> {{{neighbours}}}");
                });

                buffer.AppendLine();
            }

            return buffer.ToString();
        }
    }
}
