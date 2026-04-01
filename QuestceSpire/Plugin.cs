using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

	public static PipelineOrchestrator PipelineOrchestrator { get; private set; }

	public static SpireCodexSync SpireCodexSync { get; private set; }

	public static PatchNotesTracker PatchNotesTracker { get; private set; }

	public static SteamLeaderboardSync SteamLeaderboardSync { get; private set; }

	public static CoPickSynergyComputer CoPickSynergyComputer { get; private set; }

	public static FloorTierComputer FloorTierComputer { get; private set; }

	public static UpgradeValueComputer UpgradeValueComputer { get; private set; }

	public static RunHealthComputer RunHealthComputer { get; private set; }

	public static CombatLogger CombatLogger { get; private set; }

	public static CardUsageTracker CardUsageTracker { get; private set; }

	public static PotionTracker PotionTracker { get; private set; }

	public static AutoTierGenerator AutoTierGenerator { get; private set; }

	public static MetaArchetypeComputer MetaArchetypeComputer { get; private set; }

	public static PotionAdvisor PotionAdvisor { get; private set; }

	public static RelicCardCrossRef RelicCardCrossRef { get; private set; }

	public static RuntimeCardExtractor RuntimeCardExtractor { get; private set; }

	public static OfflineDataManager OfflineDataManager { get; private set; }

	public static CommunityData CommunityData { get; private set; }

	/// <summary>
	/// Number of compatibility issues detected at startup. Non-zero means some
	/// features may not work with the current game version.
	/// </summary>
	public static int CompatibilityIssues { get; private set; }

	public static BadgeManager BadgeManager { get; set; }
	public static OverlayCoordinator Coordinator { get; set; }

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
		ValidateGameCompatibility();
		try
		{
		var dataPath = Path.Combine(PluginFolder, "Data");
		Log("Loading tier data...");
		TierEngine = new TierEngine(dataPath);
		Log("Tier data loaded.");
		CardPropertyScorer = new CardPropertyScorer(Path.Combine(dataPath, "CardProperties"));
		DeckAnalyzer = new DeckAnalyzer(CardPropertyScorer);
		SynergyScorer = new SynergyScorer(CardPropertyScorer);
		RunDatabase = new RunDatabase(PluginFolder);
		RunTracker = new RunTracker(RunDatabase);
		RunTracker.Initialize(PluginFolder);
		LocalStats = new LocalStatsComputer(RunDatabase, TierEngine);
		LocalStats.RecomputeAll();
		new GameDataImporter(RunDatabase).ImportAll();
		AdaptiveScorer = new AdaptiveScorer(RunDatabase);
		ArchetypeDefinitions.LoadFromJson(Path.Combine(dataPath, "archetypes.json"));
		BossAdvisor.LoadFromJson(Path.Combine(dataPath, "BossData", "bosses.json"));
		BossAdvisor.LoadMonsterCodex(dataPath);
		CommunityData = CommunityData.Load(dataPath);
		EventAdvisor = new EventAdvisor(dataPath);
		EnemyAdvisor = new EnemyAdvisor(dataPath);
		CloudSync = new CloudSync(RunDatabase, RunTracker.PlayerId);
		DataUpdater = new DataUpdater(dataPath);

		// Initialize all data pipelines
		SpireCodexSync = new SpireCodexSync(dataPath);
		PatchNotesTracker = new PatchNotesTracker(RunDatabase);
		SteamLeaderboardSync = new SteamLeaderboardSync(dataPath);
		CoPickSynergyComputer = new CoPickSynergyComputer(RunDatabase);
		FloorTierComputer = new FloorTierComputer(RunDatabase);
		UpgradeValueComputer = new UpgradeValueComputer(RunDatabase);
		RunHealthComputer = new RunHealthComputer(RunDatabase);
		CombatLogger = new CombatLogger(RunDatabase);
		CardUsageTracker = new CardUsageTracker(RunDatabase);
		PotionTracker = new PotionTracker(RunDatabase);
		AutoTierGenerator = new AutoTierGenerator(RunDatabase, dataPath);
		MetaArchetypeComputer = new MetaArchetypeComputer(RunDatabase, dataPath);
		PotionAdvisor = new PotionAdvisor(RunDatabase, dataPath);
		RelicCardCrossRef = new RelicCardCrossRef(RunDatabase);
		RuntimeCardExtractor = new RuntimeCardExtractor(dataPath);
		OfflineDataManager = new OfflineDataManager(dataPath);
		OfflineDataManager.VerifyRequiredFiles();
		OfflineDataManager.CleanupOldCache(TimeSpan.FromDays(30));

		var overlaySettings = OverlaySettings.Load();
		PipelineOrchestrator = new PipelineOrchestrator(overlaySettings);

		// Background init: run all pipelines via orchestrator
		Task.Run(async () =>
		{
			try
			{
				await PipelineOrchestrator.RunAll();
				RunHealthComputer.ComputeBenchmarks();
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
		var patchCount = _harmony.GetPatchedMethods().Count();
		Log($"Harmony: {patchCount} patches applied successfully.");
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
	/// Validates that critical game types and members exist for this mod version.
	/// Logs warnings for each missing target so users can report compatibility issues.
	/// </summary>
	private static void ValidateGameCompatibility()
	{
		int missing = 0;

		void Check(string description, bool exists)
		{
			if (!exists)
			{
				Log($"COMPAT WARNING: {description} — some features may not work");
				missing++;
			}
		}

		try
		{
			var allTypes = AppDomain.CurrentDomain.GetAssemblies()
				.SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
				.ToArray();

			// Check critical types
			var runManagerType = allTypes.FirstOrDefault(t => t.Name == "RunManager");
			Check("RunManager type", runManagerType != null);
			if (runManagerType != null)
			{
				Check("RunManager.State property",
					runManagerType.GetProperty("State",
						BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null);
			}

			var cardModelType = allTypes.FirstOrDefault(t => t.Name == "CardModel");
			Check("CardModel type", cardModelType != null);

			var relicModelType = allTypes.FirstOrDefault(t => t.Name == "RelicModel");
			Check("RelicModel type", relicModelType != null);

			var combatManagerType = allTypes.FirstOrDefault(t => t.Name == "CombatManager");
			Check("CombatManager type", combatManagerType != null);
		}
		catch (Exception ex)
		{
			Log($"COMPAT: Validation failed with error: {ex.Message}");
			missing++;
		}

		CompatibilityIssues = missing;
		if (missing > 0)
			Log($"COMPAT: {missing} compatibility issue(s) detected. Mod may not function correctly with this game version.");
		else
			Log("COMPAT: All critical game types verified.");
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
			CommunityData = CommunityData.Load(dataPath);
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
