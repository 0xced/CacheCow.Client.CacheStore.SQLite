using System;
using CacheCow.Common;

namespace CacheCow.Client.CacheStore.SQLite;

internal class HttpResponse
{
    public HttpResponse()
    {
    }

    public HttpResponse(CacheKey key, byte[] data)
    {
        Id = key.HashBase64;
        CacheKey = key.ToString();
        ModificationDate = DateTimeOffset.Now;
        Data = data;
    }

    [global::SQLite.PrimaryKeyAttribute]
    public string Id { get; set; } = null!;

    [global::SQLite.NotNull]
    public string CacheKey { get; set; } = null!;

    [global::SQLite.NotNull]
    public DateTimeOffset ModificationDate { get; set; }

    [global::SQLite.NotNull]
    public byte[] Data { get; set; } = null!;
}