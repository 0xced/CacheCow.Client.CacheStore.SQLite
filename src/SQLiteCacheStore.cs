using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CacheCow.Common;
using SQLite;

[assembly: CLSCompliant(false)]

namespace CacheCow.Client.CacheStore.SQLite;

/// <summary>
/// An implementation of CacheCow's <see cref="ICacheStore"/> interface that stores
/// HTTP responses inside a SQLite database using the <c>SQLite-net</c> library.
/// </summary>
public sealed class SQLiteCacheStore : ICacheStore
{
    private readonly SQLiteAsyncConnection _db;
    private readonly IHttpMessageSerializerAsync _serializer;
    private readonly SemaphoreSlim _semaphore;
    private bool _tableIsCreated;

    /// <summary>
    /// Initializes a new instance of the <see cref="SQLiteCacheStore"/> class.
    /// </summary>
    /// <param name="db">The <see cref="SQLiteAsyncConnection"/> used to access the SQLite database.</param>
    /// <param name="serializer">An <see cref="IHttpMessageSerializer"/> used to serialize and deserialize <see cref="HttpResponseMessage"/> objects.</param>
    /// <exception cref="ArgumentNullException">Either <paramref name="db"/> or <paramref name="serializer"/> is <see langword="null"/>.</exception>
    public SQLiteCacheStore(SQLiteAsyncConnection db, IHttpMessageSerializerAsync serializer)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _semaphore = new SemaphoreSlim(1, 1);
        _tableIsCreated = false;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _semaphore.Dispose();
    }

    private async Task EnsureTableCreatedAsync()
    {
        if (_tableIsCreated)
        {
            return;
        }

        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_tableIsCreated)
            {
                // The method is named `CreateTableAsync` but is documented as is:
                // > Executes a "create table if not exists" on the database.
                await _db.CreateTableAsync<HttpResponse>().ConfigureAwait(false);
                _tableIsCreated = true;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<HttpResponseMessage?> GetValueAsync(CacheKey key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        await EnsureTableCreatedAsync().ConfigureAwait(false);

        var httpResponse = await _db.FindAsync<HttpResponse>(key.HashBase64).ConfigureAwait(false);
        if (httpResponse is not null)
        {
            using var memoryStream = new MemoryStream(httpResponse.Data);
            return await _serializer.DeserializeToResponseAsync(memoryStream).ConfigureAwait(false);
        }

        return null;
    }

    /// <inheritdoc />
    public async Task AddOrUpdateAsync(CacheKey key, HttpResponseMessage response)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (response == null) throw new ArgumentNullException(nameof(response));

        await EnsureTableCreatedAsync().ConfigureAwait(false);

        using var stream = new MemoryStream();
        await _serializer.SerializeAsync(response, stream).ConfigureAwait(false);

        await _db.InsertOrReplaceAsync(new HttpResponse(key, stream.ToArray())).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> TryRemoveAsync(CacheKey key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        await EnsureTableCreatedAsync().ConfigureAwait(false);

        var deleted = await _db.DeleteAsync<HttpResponse>(key.HashBase64).ConfigureAwait(false);

        return deleted > 0;
    }

    /// <inheritdoc />
    public async Task ClearAsync()
    {
        await EnsureTableCreatedAsync().ConfigureAwait(false);

        await _db.DeleteAllAsync<HttpResponse>().ConfigureAwait(false);
    }
}