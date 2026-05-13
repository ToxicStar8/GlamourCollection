using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Main.Services;

public static class GarlandBulkSourceFetchRunner
{
    public static async Task RunAsync(
        GarlandSourceCacheService sourceCache,
        IReadOnlyList<uint> itemIds,
        TimeSpan requestInterval,
        Action onItemCompleted,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < itemIds.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await sourceCache.FetchAndCacheAsync(itemIds[index], cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            onItemCompleted();

            if (index < itemIds.Count - 1)
                await Task.Delay(requestInterval, cancellationToken).ConfigureAwait(false);
        }
    }
}
