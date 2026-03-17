using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using QuestceSpire.Core;

namespace QuestceSpire.Tracking;

/// <summary>
/// Orchestrates all data pipelines at startup and periodically.
/// Runs pipelines in dependency order, isolating failures per-pipeline.
/// </summary>
public class PipelineOrchestrator
{
	private readonly OverlaySettings _settings;

	public PipelineOrchestrator(OverlaySettings settings)
	{
		_settings = settings;
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
		if (phase2.Count > 0)
			await Task.WhenAll(phase2);

		// Phase 3: Derived data (depends on Phase 2)
		if (Plugin.AutoTierGenerator != null)
			await RunPipeline("AutoTierGenerator", () => { Plugin.AutoTierGenerator.Generate(); return Task.CompletedTask; });

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

	private static async Task RunPipeline(string name, Func<Task> action)
	{
		try
		{
			var sw = Stopwatch.StartNew();
			await action();
			sw.Stop();
			Plugin.Log($"Pipeline [{name}]: completed in {sw.ElapsedMilliseconds}ms.");
		}
		catch (Exception ex)
		{
			Plugin.Log($"Pipeline [{name}]: FAILED — {ex.Message}");
		}
	}
}
