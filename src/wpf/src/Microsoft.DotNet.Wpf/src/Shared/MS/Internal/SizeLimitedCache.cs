﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace MS.Internal
{
    /// <summary>
    ///     Implementation of a cache with a limited size (a limited number of items
    ///     can be stored in the cache). When adding to a full cache, the element
    ///     that was last accessed is removed. Also, the cache supports permanent items
    ///     which are not subject to removal or change.
    /// </summary>
    /// <remarks>
    ///     The cache is stored in a hash table. The hash table maps
    ///     keys to nodes in a linked list. Each node contains the required
    ///     info (key, resource, permanence flag). The linked list is what
    ///     maintains the order in which items should be removed. The beginning
    ///     (_begin.Next) is the first to be removed and the end (_end.Previous)
    ///     is the last to be removed. Every time a node is accessed or
    ///     changed, it gets moved to the end of the list. Also, permanent items,
    ///     though in the hash table, are NOT in the linked list.
    /// </remarks>
    internal sealed class SizeLimitedCache<K, V>
    {
        //*****************************************************
        // Constructors
        // ****************************************************

        /// <summary>
        ///     Constructs a ResourceCache instance
        /// </summary>
        /// <param name="maximumItems">
        ///     The maximum number of nonpermanent resources the cache can store.
        /// </param>
        public SizeLimitedCache(int maximumItems)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);

            _maximumItems = maximumItems;
            _permanentCount = 0;

            // set up an empty list.
            // the _begin and _end nodes are empty nodes marking the begin and end of the list.
            _begin = new Node(default(K), default(V), false);
            _end = new Node(default(K), default(V), false);

            _begin.Next = _end;
            _end.Previous = _begin;

            _nodeLookup = new Dictionary<K, Node>();
        }

        //*****************************************************
        // Public Properties
        // ****************************************************

        /// <summary>
        ///     Returns the maximum number of nonpermanent resources the cache can store.
        /// </summary>
        public int MaximumItems
        {
            get
            {
                return _maximumItems;
            }
        }

        //*****************************************************
        // Public Methods
        // ****************************************************

        /// <summary>
        ///     Add an item to the cache. If the cache is full, the last item 
        ///     to have been accessed is removed. Permanent objects are not 
        ///     included in the count to determine if the cache is full.
        /// </summary>
        /// <param name="key">
        ///     The key of the object to add.
        /// </param>
        /// <param name="resource">
        ///     The object to be stored in the cache.
        /// </param>
        /// <param name="isPermanent">
        ///     bool indicating if the object to be cached will always be left
        ///     in the cache upon adding to a full cache.
        /// </param>
        public void Add(K key, V resource, bool isPermanent)
        {
            ArgumentNullException.ThrowIfNull(key, nameof(key));
            ArgumentNullException.ThrowIfNull(resource, nameof(resource));

            // Lookup first
            if (!_nodeLookup.TryGetValue(key, out Node node))
            {
                node = new Node(key, resource, isPermanent);
                if (!isPermanent)
                {
                    if (IsFull())
                    {
                        RemoveOldest();
                    }
                    InsertAtEnd(node);
                }
                else
                {
                    _permanentCount++;
                }

                _nodeLookup[key] = node;
            }
            else
            {
                if (!node.IsPermanent)
                {
                    RemoveFromList(node);
                }

                if (!node.IsPermanent && isPermanent)
                {
                    _permanentCount++;
                }
                else if (node.IsPermanent && !isPermanent)
                {
                    _permanentCount--;
                    if (IsFull())
                    {
                        RemoveOldest();
                    }
                }

                node.IsPermanent = isPermanent;
                node.Resource = resource;
                if (!isPermanent)
                {
                    InsertAtEnd(node);
                }
            }
        }

        /// <summary>
        ///     Remove an item from the cache.
        /// </summary>
        /// <param name="key">
        ///     The key of the object to remove.
        /// </param>
        public void Remove(K key)
        {
            ArgumentNullException.ThrowIfNull(key, nameof(key));

            if (!_nodeLookup.Remove(key, out Node node))
                return;

            if (!node.IsPermanent)
                RemoveFromList(node);
            else
                _permanentCount--;
        }

        /// <summary>
        ///     Retrieve an item from the cache.
        /// </summary>
        /// <param name="key">
        ///     The key of the object to get.
        /// </param>
        /// <returns>
        ///     The object stored in the cache based on the key. If the key is not
        ///     contained in the class, V.default is returned (Use the Contains method
        ///     if V is a value type)
        /// </returns>
        public V Get(K key)
        {
            ArgumentNullException.ThrowIfNull(key, nameof(key));

            if (!_nodeLookup.TryGetValue(key, out Node node))
                return default;

            if (!node.IsPermanent)
            {
                RemoveFromList(node);
                InsertAtEnd(node);
            }

            return node.Resource;
        }

        //*****************************************************
        // Private Methods
        // ****************************************************

        /// <summary>
        ///     Remove the oldest nonpermanent item in the cache.
        /// </summary>
        private void RemoveOldest()
        {
            Node node = _begin.Next;

            _nodeLookup.Remove(node.Key);

            RemoveFromList(node);
        }

        /// <summary>
        ///     Inserts a node at the end of the linked list
        /// </summary>
        /// <param name="node">
        ///     The node to insert
        /// </param>
        private void InsertAtEnd(Node node)
        {
            node.Next = _end;
            node.Previous = _end.Previous;

            node.Previous.Next = node;
            _end.Previous = node;
        }

        /// <summary>
        ///     Removes an item from the linked list
        /// </summary>
        /// <param name="node">
        ///     The node to remove
        /// </param>
        private static void RemoveFromList(Node node)
        {
            node.Previous.Next = node.Next;
            node.Next.Previous = node.Previous;
        }

        /// <summary>
        ///     Check if the cache is full. Do not include permanent items
        ///     in the count.
        /// </summary>
        /// <returns>
        ///     true if the cache is full. false if not.
        /// </returns>
        private bool IsFull()
        {
            return (_nodeLookup.Count - _permanentCount) >= _maximumItems;
        }

        /// <summary>
        ///     Doubly linked list node class. Has 3 values: key, resource, permanence flag
        /// </summary>
        private sealed class Node
        {
            public Node(K key, V resource, bool isPermanent)
            {
                Key = key;
                Resource = resource;
                IsPermanent = isPermanent;
            }

            public K Key { get; }

            public V Resource { get; set; }

            public bool IsPermanent { get; set; }

            public Node Next { get; set; }

            public Node Previous { get; set; }

        }

        //*****************************************************
        // Private Fields
        // ****************************************************

        // need to keep a separate counter for permanent items
        private int _permanentCount;

        // the maximum nonpermanent items allowed
        private readonly int _maximumItems;

        // the _begin and _end nodes are empty nodes marking the begin and
        // end of the list.
        private readonly Node _begin;
        private readonly Node _end;

        // the hashtable mapping keys to nodes
        private readonly Dictionary<K, Node> _nodeLookup;
    }
}
