using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace QuestceSpire.Tracking;

/// <summary>
/// Shared HTTP client with retry and per-host rate limiting for all data pipelines.
/// </summary>
public static class PipelineHttp
{
	private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(15) };
	private static readonly ConcurrentDictionary<string, SemaphoreSlim> HostLocks = new();
	private static readonly ConcurrentDictionary<string, DateTime> LastRequestByHost = new();

	/// <summary>
	/// GET with per-host rate limiting. Ensures at least <paramref name="minInterval"/> between
	/// requests to the same host.
	/// </summary>
	public static async Task<string> GetAsync(string url, TimeSpan? minInterval = null)
	{
		var uri = new Uri(url);
		var interval = minInterval ?? TimeSpan.FromSeconds(1);

		// Get or create per-host lock
		var hostLock = HostLocks.GetOrAdd(uri.Host, _ => new SemaphoreSlim(1, 1));

		await hostLock.WaitAsync();
		try
		{
			if (LastRequestByHost.TryGetValue(uri.Host, out var last))
			{
				var elapsed = DateTime.UtcNow - last;
				if (elapsed < interval)
					await Task.Delay(interval - elapsed);
			}
			LastRequestByHost[uri.Host] = DateTime.UtcNow;
		}
		finally
		{
			hostLock.Release();
		}

		return await Client.GetStringAsync(url);
	}

	/// <summary>
	/// POST with JSON body.
	/// </summary>
	public static async Task<HttpResponseMessage> PostAsync(string url, HttpContent content)
	{
		return await Client.PostAsync(url, content);
	}

	/// <summary>
	/// Execute an async operation with exponential backoff retry.
	/// </summary>
	public static async Task<T> RetryAsync<T>(Func<Task<T>> action, int maxRetries = 3, int baseDelayMs = 2000)
	{
		Exception lastEx = null;
		for (int i = 0; i <= maxRetries; i++)
		{
			try
			{
				return await action();
			}
			catch (Exception ex)
			{
				lastEx = ex;
				if (i < maxRetries)
				{
					int delay = baseDelayMs * (1 << i);
					Plugin.Log($"PipelineHttp: retry {i + 1}/{maxRetries} after {delay}ms — {ex.Message}");
					await Task.Delay(delay);
				}
			}
		}
		throw lastEx!;
	}

	/// <summary>
	/// Execute an async action with exponential backoff retry (no return value).
	/// </summary>
	public static async Task RetryAsync(Func<Task> action, int maxRetries = 3, int baseDelayMs = 2000)
	{
		await RetryAsync(async () => { await action(); return 0; }, maxRetries, baseDelayMs);
	}
}
