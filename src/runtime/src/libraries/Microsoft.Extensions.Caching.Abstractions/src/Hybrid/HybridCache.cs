// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;

namespace Microsoft.Extensions.Caching.Hybrid;

/// <summary>
/// Provides multi-tier caching services building on <see cref="IDistributedCache"/> backends.
/// </summary>
public abstract class HybridCache
{
    /// <summary>
    /// Asynchronously gets the value associated with the key if it exists, or generates a new entry using the provided key and a value from the given factory if the key is not found.
    /// </summary>
    /// <typeparam name="TState">The type of additional state required by <paramref name="factory"/>.</typeparam>
    /// <typeparam name="T">The type of the data being considered.</typeparam>
    /// <param name="key">The key of the entry to look for or create.</param>
    /// <param name="factory">Provides the underlying data service if the data is not available in the cache.</param>
    /// <param name="state">The state required for <paramref name="factory"/>.</param>
    /// <param name="options">Additional options for this cache entry.</param>
    /// <param name="tags">The tags to associate with this cache item.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The data, either from cache or the underlying data service.</returns>
    public abstract ValueTask<T> GetOrCreateAsync<TState, T>(string key, TState state, Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null, IEnumerable<string>? tags = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously gets the value associated with the key if it exists, or generates a new entry using the provided key and a value from the given factory if the key is not found.
    /// </summary>
    /// <typeparam name="T">The type of the data being considered.</typeparam>
    /// <param name="key">The key of the entry to look for or create.</param>
    /// <param name="factory">Provides the underlying data service if the data is not available in the cache.</param>
    /// <param name="options">Additional options for this cache entry.</param>
    /// <param name="tags">The tags to associate with this cache item.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The data, either from cache or the underlying data service.</returns>
    public ValueTask<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null, IEnumerable<string>? tags = null, CancellationToken cancellationToken = default)
        => GetOrCreateAsync(key, factory, WrappedCallbackCache<T>.Instance, options, tags, cancellationToken);

#if NET10_0_OR_GREATER
    /// <summary>
    /// Asynchronously gets the value associated with the key if it exists, or generates a new entry using the provided key and a value from the given factory if the key is not found.
    /// </summary>
    /// <typeparam name="T">The type of the data being considered.</typeparam>
    /// <param name="key">The key of the entry to look for or create.</param>
    /// <param name="factory">Provides the underlying data service if the data is not available in the cache.</param>
    /// <param name="options">Additional options for this cache entry.</param>
    /// <param name="tags">The tags to associate with this cache item.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The data, either from cache or the underlying data service.</returns>
    public ValueTask<T> GetOrCreateAsync<T>(
        ReadOnlySpan<char> key,
        Func<CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
        => GetOrCreateAsync(key, factory, WrappedCallbackCache<T>.Instance, options, tags, cancellationToken);

    /// <summary>
    /// Asynchronously gets the value associated with the key if it exists, or generates a new entry using the provided key and a value from the given factory if the key is not found.
    /// </summary>
    /// <typeparam name="TState">The type of additional state required by <paramref name="factory"/>.</typeparam>
    /// <typeparam name="T">The type of the data being considered.</typeparam>
    /// <param name="key">The key of the entry to look for or create.</param>
    /// <param name="factory">Provides the underlying data service if the data is not available in the cache.</param>
    /// <param name="state">The state required for <paramref name="factory"/>.</param>
    /// <param name="options">Additional options for this cache entry.</param>
    /// <param name="tags">The tags to associate with this cache item.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The data, either from cache or the underlying data service.</returns>
    /// <remarks>Implementors may use the key span to attempt a local-cache synchronous 'get' without requiring the key as a <see cref="string"/>.</remarks>
    public virtual ValueTask<T> GetOrCreateAsync<TState, T>(
        ReadOnlySpan<char> key,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
        => GetOrCreateAsync(key.ToString(), state, factory, options, tags, cancellationToken);

    /// <summary>
    /// Asynchronously gets the value associated with the key if it exists, or generates a new entry using the provided key and a value from the given factory if the key is not found.
    /// </summary>
    /// <typeparam name="T">The type of the data being considered.</typeparam>
    /// <param name="key">The key of the entry to look for or create.</param>
    /// <param name="factory">Provides the underlying data service if the data is not available in the cache.</param>
    /// <param name="options">Additional options for this cache entry.</param>
    /// <param name="tags">The tags to associate with this cache item.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The data, either from cache or the underlying data service.</returns>
    public ValueTask<T> GetOrCreateAsync<T>(
        ref DefaultInterpolatedStringHandler key,
        Func<CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        // It is *not* an error that this Clear occurs before the "await"; by definition, the implementation is *required* to copy
        // the value locally before an await, precisely because the ref-struct cannot bridge an await. Thus: we are fine to clean
        // the buffer even in the non-synchronous completion scenario.
        ValueTask<T> result = GetOrCreateAsync(key.Text, factory, WrappedCallbackCache<T>.Instance, options, tags, cancellationToken);
        key.Clear();
        return result;
    }

    /// <summary>
    /// Asynchronously gets the value associated with the key if it exists, or generates a new entry using the provided key and a value from the given factory if the key is not found.
    /// </summary>
    /// <typeparam name="TState">The type of additional state required by <paramref name="factory"/>.</typeparam>
    /// <typeparam name="T">The type of the data being considered.</typeparam>
    /// <param name="key">The key of the entry to look for or create.</param>
    /// <param name="factory">Provides the underlying data service if the data is not available in the cache.</param>
    /// <param name="state">The state required for <paramref name="factory"/>.</param>
    /// <param name="options">Additional options for this cache entry.</param>
    /// <param name="tags">The tags to associate with this cache item.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The data, either from cache or the underlying data service.</returns>
    public ValueTask<T> GetOrCreateAsync<TState, T>(
        ref DefaultInterpolatedStringHandler key,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        // It is *not* an error that this Clear occurs before the "await"; by definition, the implementation is *required* to copy
        // the value locally before an await, precisely because the ref-struct cannot bridge an await. Thus: we are fine to clean
        // the buffer even in the non-synchronous completion scenario.
        ValueTask<T> result = GetOrCreateAsync(key.Text, state, factory, options, tags, cancellationToken);
        key.Clear();
        return result;
    }
#endif

    private static class WrappedCallbackCache<T> // per-T memoized helper that allows GetOrCreateAsync<T> and GetOrCreateAsync<TState, T> to share an implementation
    {
        // for the simple usage scenario (no TState), pack the original callback as the "state", and use a wrapper function that just unrolls and invokes from the state
        public static readonly Func<Func<CancellationToken, ValueTask<T>>, CancellationToken, ValueTask<T>> Instance = static (callback, ct) => callback(ct);
    }

    /// <summary>
    /// Asynchronously sets or overwrites the value associated with the key.
    /// </summary>
    /// <typeparam name="T">The type of the data being considered.</typeparam>
    /// <param name="key">The key of the entry to create.</param>
    /// <param name="value">The value to assign for this cache entry.</param>
    /// <param name="options">Additional options for this cache entry.</param>
    /// <param name="tags">The tags to associate with this cache entry.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    public abstract ValueTask SetAsync<T>(string key, T value, HybridCacheEntryOptions? options = null, IEnumerable<string>? tags = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously removes the value associated with the key if it exists.
    /// </summary>
    public abstract ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously removes the value associated with the key if it exists.
    /// </summary>
    /// <remarks>Implementors should treat <c>null</c> as empty</remarks>
    public virtual ValueTask RemoveAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        return keys switch
        {
            // for consistency with GetOrCreate/Set: interpret null as "none"
            null or ICollection<string> { Count: 0 } => default,
            ICollection<string> { Count: 1 } => RemoveAsync(keys.First(), cancellationToken),
            _ => ForEachAsync(this, keys, cancellationToken),
        };

        // default implementation is to call RemoveAsync for each key in turn
        static async ValueTask ForEachAsync(HybridCache @this, IEnumerable<string> keys, CancellationToken cancellationToken)
        {
            foreach (var key in keys)
            {
                await @this.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Asynchronously removes all values associated with the specified tags.
    /// </summary>
    /// <remarks>Implementors should treat <c>null</c> as empty</remarks>
    public virtual ValueTask RemoveByTagAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
    {
        return tags switch
        {
            // for consistency with GetOrCreate/Set: interpret null as "none"
            null or ICollection<string> { Count: 0 } => default,
            ICollection<string> { Count: 1 } => RemoveByTagAsync(tags.Single(), cancellationToken),
            _ => ForEachAsync(this, tags, cancellationToken),
        };

        // default implementation is to call RemoveByTagAsync for each key in turn
        static async ValueTask ForEachAsync(HybridCache @this, IEnumerable<string> keys, CancellationToken cancellationToken)
        {
            foreach (var key in keys)
            {
                await @this.RemoveByTagAsync(key, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Asynchronously removes all values associated with the specified tag.
    /// </summary>
    public abstract ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default);
}
