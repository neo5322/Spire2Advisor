using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using QuestceSpire.Core;

namespace QuestceSpire.Tracking;

/// <summary>
/// Orchestrates all data pipelines at startup and periodically.
/// Runs pipelines in dependency order, isolating failures per-pipeline.
/// Supports both IDataPipeline registrations and legacy lambda pipelines.
/// </summary>
public class PipelineOrchestrator
{
	private readonly OverlaySettings _settings;
	private readonly List<IDataPipeline> _registeredPipelines = new();
	private readonly Dictionary<string, PipelineRunInfo> _runHistory = new();

	public PipelineOrchestrator(OverlaySettings settings)
	{
		_settings = settings;
	}

	/// <summary>
	/// Register a pipeline for orchestration.
	/// </summary>
	public void Register(IDataPipeline pipeline)
	{
		if (pipeline != null && !_registeredPipelines.Any(p => p.Name == pipeline.Name))
			_registeredPipelines.Add(pipeline);
	}

	/// <summary>
	/// Get status of all pipelines (registered + legacy).
	/// </summary>
	public IReadOnlyList<PipelineRunInfo> GetRunHistory()
	{
		return _runHistory.Values.ToList().AsReadOnly();
	}

	/// <summary>
	/// Run all pipelines in dependency order. Safe to call from background thread.
	/// </summary>
	public async Task RunAll()
	{
		var sw = Stopwatch.StartNew();
		Plugin.Log("PipelineOrchestrator: starting all pipelines...");

		// Phase 0: Data file updates + cloud sync (existing)
		await RunPipeline("DataUpdater", async () =>
		{
			if (_settings.AutoUpdateData)
			{
				bool updated = await Plugin.DataUpdater.CheckAndUpdate();
				if (updated)
					Plugin.ReloadAllData();
			}
		});

		await RunPipeline("CloudSync.Download", async () =>
		{
			if (_settings.CloudSyncEnabled)
			{
				await Plugin.CloudSync.DownloadCommunityStats();
				new GameDataImporter(Plugin.RunDatabase).ImportAll();
			}
		});

		// Phase 1: External data collection (parallel)
		var phase1 = new List<Task>();
		if (Plugin.SpireCodexSync != null)
			phase1.Add(RunPipeline("SpireCodexSync", () => Plugin.SpireCodexSync.SyncAll()));
		if (Plugin.PatchNotesTracker != null)
			phase1.Add(RunPipeline("PatchNotesTracker", () => Plugin.PatchNotesTracker.FetchAndParse()));
		if (Plugin.SteamLeaderboardSync != null)
			phase1.Add(RunPipeline("SteamLeaderboardSync", () => Plugin.SteamLeaderboardSync.Sync()));
		if (Plugin.RuntimeCardExtractor != null)
			phase1.Add(RunPipeline("RuntimeCardExtractor", () => { Plugin.RuntimeCardExtractor.Extract(); return Task.CompletedTask; }));

		// Add registered Phase 1 pipelines
		foreach (var p in _registeredPipelines.Where(p => p.Phase == 1))
			phase1.Add(RunRegisteredPipeline(p));
		if (phase1.Count > 0)
			await Task.WhenAll(phase1);

		// Phase 2: Local data analysis (parallel)
		var phase2 = new List<Task>();
		if (Plugin.CoPickSynergyComputer != null)
			phase2.Add(RunPipeline("CoPickSynergy", () => { Plugin.CoPickSynergyComputer.Compute(); return Task.CompletedTask; }));
		if (Plugin.FloorTierComputer != null)
			phase2.Add(RunPipeline("FloorTier", () => { Plugin.FloorTierComputer.Compute(); return Task.CompletedTask; }));
		if (Plugin.UpgradeValueComputer != null)
			phase2.Add(RunPipeline("UpgradeValue", () => { Plugin.UpgradeValueComputer.Compute(); return Task.CompletedTask; }));
		if (Plugin.MetaArchetypeComputer != null)
			phase2.Add(RunPipeline("MetaArchetype", () => { Plugin.MetaArchetypeComputer.Compute(); return Task.CompletedTask; }));
		if (Plugin.RelicCardCrossRef != null)
			phase2.Add(RunPipeline("RelicCardCrossRef", () => { Plugin.RelicCardCrossRef.Compute(); return Task.CompletedTask; }));

		// Add registered Phase 2 pipelines
		foreach (var p in _registeredPipelines.Where(p => p.Phase == 2))
			phase2.Add(RunRegisteredPipeline(p));
		if (phase2.Count > 0)
			await Task.WhenAll(phase2);

		// Phase 3: Derived data (depends on Phase 2)
		if (Plugin.AutoTierGenerator != null)
			await RunPipeline("AutoTierGenerator", () => { Plugin.AutoTierGenerator.Generate(); return Task.CompletedTask; });

		// Add registered Phase 3+ pipelines sequentially
		foreach (var p in _registeredPipelines.Where(p => p.Phase >= 3).OrderBy(p => p.Phase))
			await RunRegisteredPipeline(p);

		sw.Stop();
		Plugin.Log($"PipelineOrchestrator: all pipelines completed in {sw.ElapsedMilliseconds}ms.");
	}

	/// <summary>
	/// Upload pending data to cloud. Called after run ends.
	/// </summary>
	public async Task RunUpload()
	{
		await RunPipeline("CloudSync.Upload", async () =>
		{
			if (_settings.CloudSyncEnabled)
				await Plugin.CloudSync.UploadPendingRuns();
		});
	}

	private async Task RunRegisteredPipeline(IDataPipeline pipeline)
	{
		var info = new PipelineRunInfo { Name = pipeline.Name, Phase = pipeline.Phase };
		try
		{
			var pSw = Stopwatch.StartNew();
			bool success = await pipeline.RunAsync();
			pSw.Stop();
			info.DurationMs = pSw.ElapsedMilliseconds;
			info.Status = success ? PipelineStatus.Success : PipelineStatus.Failed;
			Plugin.Log($"Pipeline [{pipeline.Name}]: {info.Status} in {info.DurationMs}ms.");
		}
		catch (Exception ex)
		{
			info.Status = PipelineStatus.Failed;
			info.Error = ex.Message;
			Plugin.Log($"Pipeline [{pipeline.Name}]: FAILED — {ex.Message}");
		}
		_runHistory[pipeline.Name] = info;
	}

	private async Task RunPipeline(string name, Func<Task> action)
	{
		var info = new PipelineRunInfo { Name = name };
		try
		{
			var pSw = Stopwatch.StartNew();
			await action();
			pSw.Stop();
			info.DurationMs = pSw.ElapsedMilliseconds;
			info.Status = PipelineStatus.Success;
			Plugin.Log($"Pipeline [{name}]: completed in {info.DurationMs}ms.");
		}
		catch (Exception ex)
		{
			info.Status = PipelineStatus.Failed;
			info.Error = ex.Message;
			Plugin.Log($"Pipeline [{name}]: FAILED — {ex.Message}");
		}
		_runHistory[name] = info;
	}
}

public class PipelineRunInfo
{
	public string Name { get; set; }
	public int Phase { get; set; }
	public PipelineStatus Status { get; set; } = PipelineStatus.NotRun;
	public long DurationMs { get; set; }
	public string Error { get; set; }
}
