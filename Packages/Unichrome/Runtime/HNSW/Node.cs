// <copyright file="Node.cs" company="Microsoft">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// </copyright>

using System.Collections.Generic;
using MemoryPack;

namespace Unichrome.HNSW
{
    /// <summary>
    /// The implementation of the node in hnsw graph.
    /// </summary>
    [MemoryPackable()]
    public partial struct Node
    {
       
        public List<List<int>> Connections;

        public int Id;

        /// <summary>
        /// Gets the max layer where the node is presented.
        /// </summary>
        [MemoryPackIgnore]
        public int MaxLayer
        {
            get
            {
                return Connections.Count - 1;
            }
        }

        /// <summary>
        /// Gets connections ids of the node at the given layer
        /// </summary>
        /// <param name="layer">The layer to get connections at.</param>
        /// <returns>The connections of the node at the given layer.</returns>
        [MemoryPackIgnore]
        public List<int> this[int layer]
        {
            get
            {
                return Connections[layer];
            }
            set
            {
                Connections[layer] = value;
            }
        }
    }
}
