// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Models.APIs.Roblox;
using Froststrap.Services;
using Froststrap.Utility;
using System.Collections.Concurrent;

namespace Froststrap.Integrations.Playtime;

internal sealed class PlaytimeGameMetadataFetcher : IDisposable
{
    private const int BatchSize = 50;
    private const int MaxConcurrentBatches = 2;
    private const int DelayBetweenBatchesMs = 1000;
    private const long StaleThresholdSeconds = 7 * 24 * 60 * 60; // 7 days

    private readonly PlaytimeDb _db;
    private readonly ConcurrentQueue<long> _fetchQueue = new();
    private readonly HashSet<long> _inProgress = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _processorTask;
    private readonly SemaphoreSlim _batchSemaphore = new(MaxConcurrentBatches, MaxConcurrentBatches);
    private bool _disposed;

    public PlaytimeGameMetadataFetcher(PlaytimeDb db)
    {
        _db = db;
    }

    public void Start()
    {
        if (_processorTask != null)
            return;

        _processorTask = ProcessQueueAsync(_cts.Token);
    }

    public async Task StopAsync()
    {
        _cts.Cancel();
        if (_processorTask != null)
        {
            try { await _processorTask; } catch (OperationCanceledException) { }
            _processorTask = null;
        }
    }

    public void Enqueue(long universeId)
    {
        if (_inProgress.Add(universeId))
        {
            _fetchQueue.Enqueue(universeId);
        }
    }

    public void EnqueueRange(IEnumerable<long> universeIds)
    {
        foreach (var id in universeIds)
        {
            if (_inProgress.Add(id))
            {
                _fetchQueue.Enqueue(id);
            }
        }
    }

    private async Task ProcessQueueAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var batch = new List<long>();
            
            // Collect up to BatchSize items
            while (batch.Count < BatchSize && _fetchQueue.TryDequeue(out var id))
            {
                batch.Add(id);
            }

            if (batch.Count == 0)
            {
                await Task.Delay(5000, ct); // Wait for new items
                continue;
            }

            try
            {
                await FetchBatchAsync(batch, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Froststrap.App.Logger.Error(ex, "Game metadata fetch batch failed");
            }
            finally
            {
                foreach (var id in batch)
                    _inProgress.Remove(id);
            }

            // Rate-limit friendly delay between batches
            await Task.Delay(DelayBetweenBatchesMs, ct);
        }
    }

    private async Task FetchBatchAsync(List<long> universeIds, CancellationToken ct)
    {
        if (universeIds.Count == 0)
            return;

        var idsString = string.Join(",", universeIds);
        
        try
        {
            // Fetch game details
            Uri gameDetailsUrl = UrlBuilder.BuildApiUrl("games", $"v1/games?universeIds={idsString}");
            var gameDetailResponse = await Http.GetJson<ApiArrayResponse<GameDetailResponse>>(gameDetailsUrl);

            // Fetch thumbnails
            Uri thumbnailsUrl = UrlBuilder.BuildApiUrl("thumbnails", $"v1/games/icons?universeIds={idsString}&returnPolicy=PlaceHolder&size=128x128&format=Png&isCircular=false");
            var thumbnailResponse = await Http.GetJson<ApiArrayResponse<ThumbnailResponse>>(thumbnailsUrl);

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var games = gameDetailResponse?.Data ?? [];
            var thumbnails = thumbnailResponse?.Data ?? [];

            foreach (var game in games)
            {
                if (ct.IsCancellationRequested) break;

                var thumb = thumbnails.FirstOrDefault(t => t.TargetId == game.Id)
                            ?? new ThumbnailResponse { TargetId = game.Id, ImageUrl = "" };

                await _db.UpsertGameAsync(game.Id, game.Name, thumb.ImageUrl, ct);
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            // Re-queue items on rate limit
            foreach (var id in universeIds)
                _fetchQueue.Enqueue(id);
            foreach (var id in universeIds)
                _inProgress.Remove(id);
            
            Froststrap.App.Logger.Warn("Game metadata fetch rate limited, re-queuing batch");
            await Task.Delay(30000, ct); // Longer backoff on 429
        }
        catch (Exception ex)
        {
            Froststrap.App.Logger.Error(ex, "Failed to fetch game metadata batch");
            // Re-queue on transient failures (optional - could skip instead)
        }
    }

    public async Task EnsureGamesCachedAsync(IEnumerable<long> universeIds, CancellationToken ct = default)
    {
        var now = NowUnix();
        var toFetch = new List<long>();
        
        foreach (var id in universeIds)
        {
            if (ct.IsCancellationRequested) break;
            
            var game = await _db.GetGameAsync(id, ct);
            if (game == null || game.FetchedAt == 0 || (now - game.FetchedAt) > StaleThresholdSeconds)
            {
                toFetch.Add(id);
            }
        }

        if (toFetch.Count > 0)
        {
            EnqueueRange(toFetch);
        }
    }

    private static long NowUnix() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts.Cancel();
        try { _processorTask?.Wait(2000); } catch { }
        _cts.Dispose();
        _batchSemaphore.Dispose();
    }
}