using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Saves;
using QuestceSpire.Core;
using QuestceSpire.Tracking;
using QuestceSpire.UI;

namespace QuestceSpire;

[ModInitializer("Init")]
public static class Plugin
{
	public const string ModName = "Spire Advisor";

	public static string ModVersion => typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

	public const string HarmonyId = "com.spire.advisor";

	private static Harmony _harmony;

	private static bool _initialized;

	private static volatile bool _backgroundInitDone;

	private static volatile bool _backgroundInitFailed;

	public static bool IsBackgroundInitDone => _backgroundInitDone;

	public static bool IsBackgroundInitFailed => _backgroundInitFailed;

	public static string PluginFolder { get; private set; }

	public static string LogPath { get; private set; }

	public static TierEngine TierEngine { get; private set; }

	public static DeckAnalyzer DeckAnalyzer { get; private set; }

	public static SynergyScorer SynergyScorer { get; private set; }

	public static AdaptiveScorer AdaptiveScorer { get; private set; }

	public static RunTracker RunTracker { get; private set; }

	public static RunDatabase RunDatabase { get; private set; }

	public static LocalStatsComputer LocalStats { get; private set; }

	public static CardPropertyScorer CardPropertyScorer { get; private set; }

	public static CloudSync CloudSync { get; private set; }

	public static EventAdvisor EventAdvisor { get; private set; }

	public static EnemyAdvisor EnemyAdvisor { get; private set; }

	public static string LatestVersion { get; set; }

	public static string UpdateUrl { get; set; }

	public static DataUpdater DataUpdater { get; private set; }

	public static OverlayManager Overlay { get; set; }

	public static void Init()
	{
		if (_initialized) return;
		_initialized = true;
		PluginFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		if (string.IsNullOrEmpty(PluginFolder))
		{
			PluginFolder = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
		}
		if (string.IsNullOrEmpty(PluginFolder))
		{
			PluginFolder = Path.Combine(AppContext.BaseDirectory, "mods", "SpireAdvisor");
		}
		LogPath = Path.Combine(PluginFolder, "spire-advisor.log");
		AppDomain.CurrentDomain.AssemblyResolve += delegate(object? sender, ResolveEventArgs args)
		{
			AssemblyName assemblyName = new AssemblyName(args.Name);
			string text = Path.Combine(PluginFolder, assemblyName.Name + ".dll");
			return File.Exists(text) ? Assembly.LoadFrom(text) : null;
		};
		Log($"{ModName} v{ModVersion} initializing...");
		Log($"PluginFolder: {PluginFolder}");
		try
		{
		var dataPath = Path.Combine(PluginFolder, "Data");
		Log("Loading tier data...");
		TierEngine = new TierEngine(dataPath);
		Log("Tier data loaded.");
		CardPropertyScorer = new CardPropertyScorer(Path.Combine(dataPath, "CardProperties"));
		DeckAnalyzer = new DeckAnalyzer();
		SynergyScorer = new SynergyScorer();
		RunDatabase = new RunDatabase(PluginFolder);
		RunTracker = new RunTracker(RunDatabase);
		RunTracker.Initialize(PluginFolder);
		LocalStats = new LocalStatsComputer(RunDatabase, TierEngine);
		LocalStats.RecomputeAll();
		new GameDataImporter(RunDatabase).ImportAll();
		AdaptiveScorer = new AdaptiveScorer(RunDatabase);
		ArchetypeDefinitions.LoadFromJson(Path.Combine(dataPath, "archetypes.json"));
		BossAdvisor.LoadFromJson(Path.Combine(dataPath, "BossData", "bosses.json"));
		EventAdvisor = new EventAdvisor(dataPath);
		EnemyAdvisor = new EnemyAdvisor(dataPath);
		CloudSync = new CloudSync(RunDatabase, RunTracker.PlayerId);
		DataUpdater = new DataUpdater(dataPath);
		var overlaySettings = OverlaySettings.Load();
		// Background init: data update + cloud sync
		Task.Run(async () =>
		{
			try
			{
				// Auto-update tier/enemy/event data if enabled
				if (overlaySettings.AutoUpdateData)
				{
					bool updated = await DataUpdater.CheckAndUpdate();
					if (updated)
						ReloadAllData();
				}

				// Download community stats
				if (overlaySettings.CloudSyncEnabled)
				{
					await CloudSync.DownloadCommunityStats();
					new GameDataImporter(RunDatabase).ImportAll();
				}

				_backgroundInitDone = true;
			}
			catch (Exception ex)
			{
				Log("Background init error: " + ex.Message);
				_backgroundInitFailed = true;
				_backgroundInitDone = true;
			}
		});
		_harmony = new Harmony(HarmonyId);
		_harmony.PatchAll(typeof(GamePatches).Assembly);
		GamePatches.ApplyManualPatches(_harmony);
		MethodInfo methodInfo = typeof(UserDataPathProvider).GetProperty("IsRunningModded", BindingFlags.Static | BindingFlags.Public)?.GetGetMethod();
		if (methodInfo != null)
		{
			MethodInfo method = typeof(GamePatches).GetMethod("ForceNotModded", BindingFlags.Static | BindingFlags.Public);
			_harmony.Patch(methodInfo, null, new HarmonyMethod(method));
			Log("Patched IsRunningModded to false — using main profile.");
		}
		Log("Harmony patches applied.");
		// Fire-and-forget version check
		Task.Run(async () =>
		{
			try
			{
				using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
				var resp = await http.GetStringAsync("https://questcespire-api.questcespire.workers.dev/api/version");
				var ver = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(resp);
				if (ver != null && ver.TryGetValue("latest", out var latest))
				{
					if (CompareVersions(ModVersion, latest) < 0)
					{
						LatestVersion = latest;
						ver.TryGetValue("release_url", out var url);
						UpdateUrl = url;
						Log($"Update available: v{latest} (current: v{ModVersion})");
					}
					else
					{
						Log($"Version check: up to date (v{ModVersion})");
					}
				}
			}
			catch (Exception ex)
			{
				Log($"Version check failed: {ex.Message}");
			}
		});
		Log($"{ModName} initialized successfully. Waiting for scene tree...");
		}
		catch (Exception ex)
		{
			Log($"FATAL init error: {ex}");
			Godot.GD.PrintErr($"[SpireAdvisor] FATAL: {ex}");
		}
	}

	/// <summary>
	/// Reload all data files after a remote update.
	/// </summary>
	internal static void ReloadAllData()
	{
		try
		{
			var dataPath = Path.Combine(PluginFolder, "Data");
			TierEngine?.Reload();
			EventAdvisor?.Reload();
			EnemyAdvisor?.Reload();
			ArchetypeDefinitions.LoadFromJson(Path.Combine(dataPath, "archetypes.json"));
			BossAdvisor.LoadFromJson(Path.Combine(dataPath, "BossData", "bosses.json"));
			Log("All data reloaded after update.");
		}
		catch (Exception ex)
		{
			Log($"ReloadAllData error: {ex.Message}");
		}
	}

	internal static int CompareVersions(string a, string b)
	{
		var pa = a.Split('.');
		var pb = b.Split('.');
		int len = Math.Max(pa.Length, pb.Length);
		for (int i = 0; i < len; i++)
		{
			int va = i < pa.Length && int.TryParse(pa[i], out var x) ? x : 0;
			int vb = i < pb.Length && int.TryParse(pb[i], out var y) ? y : 0;
			if (va != vb) return va.CompareTo(vb);
		}
		return 0;
	}

	private static volatile StreamWriter _logWriter;
	private static readonly object _logLock = new object();
	private static bool _exitHandlerRegistered;

	public static void Log(string message)
	{
		string text = $"[{DateTime.Now:HH:mm:ss}] [QuestceSpire] {message}";
		try
		{
			lock (_logLock)
			{
				if (_logWriter == null)
				{
					_logWriter = new StreamWriter(LogPath, append: true) { AutoFlush = true };
					if (!_exitHandlerRegistered)
					{
						_exitHandlerRegistered = true;
						AppDomain.CurrentDomain.ProcessExit += (_, _) =>
						{
							lock (_logLock) { _logWriter?.Dispose(); _logWriter = null; }
						};
					}
				}
				_logWriter.WriteLine(text);
			}
		}
		catch (Exception ex)
		{
			// Last resort: log to Godot console since file logging failed
			Godot.GD.PrintErr($"[SpireAdvisor] Log write failed: {ex.Message}");
		}
	}
}
