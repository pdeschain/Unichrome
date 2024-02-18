// <copyright file="SmallWorld.cs" company="Microsoft">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MemoryPack;

namespace Unichrome.HNSW
{
    /// <summary>
    /// The Hierarchical Navigable Small World Graphs. https://arxiv.org/abs/1603.09320
    /// </summary>
    /// <typeparam name="TItem">The type of items to connect into small world.</typeparam>
    /// <typeparam name="TDistance">The type of distance between items (expect any numeric type: float, double, decimal, int, ...).</typeparam>
    public partial class SmallWorld<TItem, TDistance> where TDistance : struct, IComparable<TDistance>
    {
        private readonly Func<TItem, TItem, TDistance> Distance;

        private Graph<TItem, TDistance> Graph;
        private IProvideRandomValues Generator;

        private ReaderWriterLockSlim _rwLock;

        /// <summary>
        /// Gets the list of items currently held by the SmallWorld graph. 
        /// The list is not protected by any locks, and should only be used when it is known the graph won't change
        /// </summary>
        public IReadOnlyList<TItem> UnsafeItems => Graph?.GraphCore?.Items;

        /// <summary>
        /// Gets a copy of the list of items currently held by the SmallWorld graph. 
        /// This call is protected by a read-lock and is safe to be called from multiple threads.
        /// </summary>
        public IReadOnlyList<TItem> Items
        {
            get
            {
                if (_rwLock is object)
                {
                    _rwLock.EnterReadLock();
                    try
                    {
                        return Graph.GraphCore.Items.ToList();
                    }
                    finally
                    {
                        _rwLock.ExitReadLock();
                    }
                }
                else
                {
                    return Graph?.GraphCore?.Items;
                }
            }
        }


        /// <summary>
        /// Initializes a new instance of the <see cref="SmallWorld{TItem, TDistance}"/> class.
        /// </summary>
        /// <param name="distance">The distance function to use in the small world.</param>
        /// <param name="generator">The random number generator for building graph.</param>
        /// <param name="parameters">Parameters of the algorithm.</param>
        public SmallWorld(Func<TItem, TItem, TDistance> distance, IProvideRandomValues generator, Parameters parameters, bool threadSafe = true)
        {
            Distance = distance;
            Graph = new Graph<TItem, TDistance>(Distance, parameters);
            Generator = generator;
            _rwLock = threadSafe ? new ReaderWriterLockSlim() : null;
        }

        /// <summary>
        /// Builds hnsw graph from the items.
        /// </summary>
        /// <param name="items">The items to connect into the graph.</param>

        public IReadOnlyList<int> AddItems(IReadOnlyList<TItem> items, IProgressReporter progressReporter = null)
        {
            _rwLock?.EnterWriteLock();
            try
            {
               return Graph.AddItems(items, Generator, progressReporter);
            }
            finally
            {
                _rwLock?.ExitWriteLock();
            }
        }

        /// <summary>
        /// Run knn search for a given item.
        /// </summary>
        /// <param name="item">The item to search nearest neighbours.</param>
        /// <param name="k">The number of nearest neighbours.</param>
        /// <returns>The list of found nearest neighbours.</returns>
        public IList<KNNSearchResult> KNNSearch(TItem item, int k)
        {
            _rwLock?.EnterReadLock();
            try
            {
                return Graph.KNearest(item, k);
            }
            finally
            {
                _rwLock?.ExitReadLock();
            }
        }

        /// <summary>
        /// Get the item with the index
        /// </summary>
        /// <param name="index">The index of the item</param>
        public TItem GetItem(int index)
        {
            _rwLock?.EnterReadLock();
            try
            {
                return Items[index];
            }
            finally
            {
                _rwLock?.ExitReadLock();
            }
        }

        /// <summary>
        /// Serializes the graph WITHOUT linked items.
        /// </summary>
        /// <returns>Bytes representing the graph.</returns>
        public byte[] SerializeGraph()
        {
            if (Graph == null)
            {
                throw new InvalidOperationException("The graph does not exist");
            }
            _rwLock?.EnterReadLock();
            try
            {
                return MemoryPackSerializer.Serialize(Graph);
            }
            finally
            {
                _rwLock?.ExitReadLock();
            }
        }

        /// <summary>
        /// Deserializes the graph from byte array.
        /// </summary>
        /// <param name="items">The items to assign to the graph's verticies.</param>
        /// <param name="bytes">The serialized parameters and edges.</param>
        public static SmallWorld<TItem, TDistance> DeserializeGraph(byte[] bytes, IReadOnlyList<TItem> items, Func<TItem, TItem, TDistance> distance, IProvideRandomValues generator, bool threadSafe = true)
        {
            var graph = MemoryPackSerializer.Deserialize<Graph<TItem, TDistance>>(bytes);
            //Overwrite previous InitialDistanceCacheSize parameter, so we don't waste time/memory allocating a distance cache for an already existing graph
            graph.Parameters.InitialDistanceCacheSize = 0;
            graph.InitializeAfterDeserialization(distance);
            
            //items are not serialized, so we need to add them back
            graph.GraphCore.Items.AddRange(items);
            
            var world = new SmallWorld<TItem, TDistance>(distance, generator, graph.Parameters, threadSafe: threadSafe);
            world.Graph = graph;
            return world;
        }

        /// <summary>
        /// Prints edges of the graph. Mostly for debug and test purposes.
        /// </summary>
        /// <returns>String representation of the graph's edges.</returns>
        public string Print()
        {
            return Graph.Print();
        }

        /// <summary>
        /// Frees the memory used by the Distance Cache
        /// </summary>
        public void ResizeDistanceCache(int newSize)
        {
            Graph.GraphCore.ResizeDistanceCache(newSize);
        }

        

        public class KNNSearchResult
        {
            internal KNNSearchResult(int id, TItem item, TDistance distance)
            {
                Id = id;
                Item = item;
                Distance = distance;
            }

            public int Id { get; }

            public TItem Item { get; }

            public TDistance Distance { get; }

            public override string ToString()
            {
                return $"I:{Id} Dist:{Distance:n2} [{Item}]";
            }
        }
    }
}
