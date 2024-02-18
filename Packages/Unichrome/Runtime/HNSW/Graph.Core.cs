// <copyright file="Graph.Core.cs" company="Microsoft">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MemoryPack;

namespace Unichrome.HNSW
{
    [MemoryPackable()]
    internal partial class Core<TItem, TDistance> where TDistance : struct, IComparable<TDistance>
    {
        private Func<TItem, TItem, TDistance> Distance;

        private DistanceCache<TDistance> DistanceCache;

        private long DistanceCalculationsCount;

        [MemoryPackInclude] internal List<Node> Nodes { get; private set; }

        internal List<TItem> Items { get; private set; } = new List<TItem>();

        internal Algorithms.Algorithm<TItem, TDistance> Algorithm { get; private set; }

        internal Parameters Parameters { get; private set; }

        internal float DistanceCacheHitRate => (float)(DistanceCache?.HitCount ?? 0) / DistanceCalculationsCount;

        internal Core(Func<TItem, TItem, TDistance> distance, Parameters parameters)
        {
            Distance = distance;
            Parameters = parameters;
            var initialSize = Math.Max(1024, Parameters.InitialItemsSize);

            Nodes = new List<Node>(initialSize);
            Items = new List<TItem>(initialSize);

            switch (Parameters.NeighbourHeuristic)
            {
                case NeighbourSelectionHeuristic.SelectSimple:
                {
                    Algorithm = new Algorithms.Algorithm3<TItem, TDistance>(this);
                    break;
                }
                case NeighbourSelectionHeuristic.SelectHeuristic:
                {
                    Algorithm = new Algorithms.Algorithm4<TItem, TDistance>(this);
                    break;
                }
            }

            if (Parameters.EnableDistanceCacheForConstruction)
            {
                DistanceCache = new DistanceCache<TDistance>();
                DistanceCache.Resize(Parameters.InitialDistanceCacheSize, false);
            }

            DistanceCalculationsCount = 0;
        }

        [MemoryPackConstructor]
        internal Core(List<Node> nodes)
        {
            Nodes = nodes;
        }

        public void InitializeAfterDeserialization(Func<TItem, TItem, TDistance> distance, Parameters parameters)
        {
            Distance = distance;
            Parameters = parameters;
            var initialSize = Math.Max(1024, Parameters.InitialItemsSize);

            Items = new List<TItem>(initialSize);

            switch (Parameters.NeighbourHeuristic)
            {
                case NeighbourSelectionHeuristic.SelectSimple:
                {
                    Algorithm = new Algorithms.Algorithm3<TItem, TDistance>(this);
                    break;
                }
                case NeighbourSelectionHeuristic.SelectHeuristic:
                {
                    Algorithm = new Algorithms.Algorithm4<TItem, TDistance>(this);
                    break;
                }
            }

            if (Parameters.EnableDistanceCacheForConstruction)
            {
                DistanceCache = new DistanceCache<TDistance>();
                DistanceCache.Resize(Parameters.InitialDistanceCacheSize, false);
            }

            DistanceCalculationsCount = 0;
        }

        internal IReadOnlyList<int> AddItems(IReadOnlyList<TItem> items, IProvideRandomValues generator)
        {
            int newCount = items.Count;

            var newIDs = new List<int>();
            Items.AddRange(items);
            DistanceCache?.Resize(newCount, false);

            int id0 = Nodes.Count;

            for (int id = 0; id < newCount; ++id)
            {
                Nodes.Add(Algorithm.NewNode(id0 + id, RandomLayer(generator, Parameters.LevelLambda)));
                newIDs.Add(id0 + id);
            }
            
            return newIDs;
        }

        internal void ResizeDistanceCache(int newSize)
        {
            DistanceCache?.Resize(newSize, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TDistance GetDistance(int fromId, int toId)
        {
            DistanceCalculationsCount++;
            if (DistanceCache is object)
            {
                return DistanceCache.GetValue(fromId, toId, GetDistanceSkipCache);
            }
            else
            {
                return Distance(Items[fromId], Items[toId]);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TDistance GetDistanceSkipCache(int fromId, int toId)
        {
            return Distance(Items[fromId], Items[toId]);
        }

        private static int RandomLayer(IProvideRandomValues generator, double lambda)
        {
            var r = -Math.Log(generator.NextFloat()) * lambda;
            return (int)r;
        }
    }
}