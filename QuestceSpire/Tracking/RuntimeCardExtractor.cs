using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace QuestceSpire.Tracking;

/// <summary>
/// Reflects over sts2.dll at runtime to discover all card/relic/potion definitions.
/// Detects new entities that aren't in our data files and logs them for manual review.
/// </summary>
public class RuntimeCardExtractor
{
	private readonly string _dataPath;

	public RuntimeCardExtractor(string dataPath)
	{
		_dataPath = dataPath;
	}

	/// <summary>
	/// Extract all card/relic IDs from the game assembly and compare with known data.
	/// </summary>
	public ExtractorResult Extract()
	{
		var result = new ExtractorResult();

		try
		{
			// Find the game assembly
			Assembly gameAssembly = null;
			foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
			{
				if (asm.GetName().Name == "sts2" || asm.GetName().Name?.Contains("MegaCrit") == true)
				{
					gameAssembly = asm;
					break;
				}
			}
			if (gameAssembly == null)
			{
				Plugin.Log("RuntimeCardExtractor: game assembly not found.");
				return result;
			}

			// Extract card types
			ExtractCards(gameAssembly, result);
			ExtractRelics(gameAssembly, result);

			// Compare with known data
			var knownCards = LoadKnownIds("cards");
			var knownRelics = LoadKnownIds("relics");

			result.NewCards = result.AllCards.Where(c => !knownCards.Contains(c.Id)).ToList();
			result.NewRelics = result.AllRelics.Where(r => !knownRelics.Contains(r.Id)).ToList();

			if (result.NewCards.Count > 0)
			{
				Plugin.Log($"RuntimeCardExtractor: found {result.NewCards.Count} new cards not in data files.");
				SaveNewEntities("new_cards.json", result.NewCards);
			}
			if (result.NewRelics.Count > 0)
			{
				Plugin.Log($"RuntimeCardExtractor: found {result.NewRelics.Count} new relics not in data files.");
				SaveNewEntities("new_relics.json", result.NewRelics);
			}

			Plugin.Log($"RuntimeCardExtractor: {result.AllCards.Count} cards, {result.AllRelics.Count} relics total.");
		}
		catch (Exception ex)
		{
			Plugin.Log($"RuntimeCardExtractor: error — {ex.Message}");
		}

		return result;
	}

	private void ExtractCards(Assembly asm, ExtractorResult result)
	{
		try
		{
			var seenCardIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			// Look for card definition types — STS2 uses a base card class
			var baseCardType = asm.GetTypes().FirstOrDefault(t =>
				t.FullName?.Contains("CardModel") == true && !t.IsAbstract && t.IsPublic);

			// Also try to find card ID enum/constants
			var cardTypes = asm.GetTypes().Where(t =>
				t.Namespace?.Contains("Cards") == true &&
				!t.IsAbstract &&
				t.IsClass &&
				t.BaseType?.Name?.Contains("Card") == true
			).ToList();

			foreach (var type in cardTypes)
			{
				try
				{
					string id = type.Name;
					// Try to get the card's ID property/field
					var idProp = type.GetProperty("Id", BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
					var idField = type.GetField("Id", BindingFlags.Public | BindingFlags.Static);

					if (idField != null && idField.IsStatic)
						id = idField.GetValue(null)?.ToString() ?? type.Name;
					else if (idProp != null && idProp.GetGetMethod()?.IsStatic == true)
						id = idProp.GetValue(null)?.ToString() ?? type.Name;

					if (!seenCardIds.Add(id)) continue; // Skip duplicate
					result.AllCards.Add(new ExtractedEntity
					{
						Id = id,
						TypeName = type.FullName,
						EntityType = "card"
					});
				}
				catch (Exception ex) { Plugin.Log($"RuntimeCardExtractor: failed to extract card type '{type?.FullName}': {ex.Message}"); }
			}

			// Also look for string constants that look like card IDs
			var idRegistryType = asm.GetTypes().FirstOrDefault(t =>
				t.Name.Contains("CardId") || t.Name.Contains("CardRegistry") || t.Name.Contains("CardDatabase"));
			if (idRegistryType != null)
			{
				foreach (var field in idRegistryType.GetFields(BindingFlags.Public | BindingFlags.Static))
				{
					if (field.FieldType == typeof(string))
					{
						string val = field.GetValue(null) as string;
						if (!string.IsNullOrEmpty(val) && seenCardIds.Add(val))
						{
							result.AllCards.Add(new ExtractedEntity
							{
								Id = val,
								TypeName = $"{idRegistryType.FullName}.{field.Name}",
								EntityType = "card"
							});
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log($"RuntimeCardExtractor: card extraction error — {ex.Message}");
		}
	}

	private void ExtractRelics(Assembly asm, ExtractorResult result)
	{
		try
		{
			var seenRelicIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			var relicTypes = asm.GetTypes().Where(t =>
				t.Namespace?.Contains("Relic") == true &&
				!t.IsAbstract &&
				t.IsClass &&
				t.BaseType?.Name?.Contains("Relic") == true
			).ToList();

			foreach (var type in relicTypes)
			{
				try
				{
					string id = type.Name;
					var idField = type.GetField("Id", BindingFlags.Public | BindingFlags.Static);
					if (idField != null && idField.IsStatic)
						id = idField.GetValue(null)?.ToString() ?? type.Name;

					if (!seenRelicIds.Add(id)) continue; // Skip duplicate
					result.AllRelics.Add(new ExtractedEntity
					{
						Id = id,
						TypeName = type.FullName,
						EntityType = "relic"
					});
				}
				catch (Exception ex) { Plugin.Log($"RuntimeCardExtractor: failed to extract relic type '{type?.FullName}': {ex.Message}"); }
			}
		}
		catch (Exception ex)
		{
			Plugin.Log($"RuntimeCardExtractor: relic extraction error — {ex.Message}");
		}
	}

	private HashSet<string> LoadKnownIds(string entityType)
	{
		var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			// Load from tier files
			string tierDir = entityType == "cards" ? "CardTiers" : "RelicTiers";
			string dirPath = Path.Combine(_dataPath, tierDir);
			if (Directory.Exists(dirPath))
			{
				foreach (string file in Directory.GetFiles(dirPath, "*.json"))
				{
					try
					{
						var entries = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(File.ReadAllText(file));
						if (entries != null)
						{
							foreach (var e in entries)
							{
								if (e.TryGetValue("id", out var id) || e.TryGetValue("Id", out id))
									ids.Add(id.ToString());
							}
						}
					}
					catch (Exception ex) { Plugin.Log($"RuntimeCardExtractor: failed to parse tier file for known IDs: {ex.Message}"); }
				}
			}

			// Also load from codex files
			string codexFile = entityType == "cards" ? "codex_cards.json" : "codex_relics.json";
			string codexPath = Path.Combine(_dataPath, codexFile);
			if (File.Exists(codexPath))
			{
				var codexEntries = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(File.ReadAllText(codexPath));
				if (codexEntries != null)
				{
					foreach (var e in codexEntries)
					{
						if (e.TryGetValue("id", out var id) || e.TryGetValue("Id", out id))
							ids.Add(id.ToString());
					}
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log($"RuntimeCardExtractor: error loading known {entityType} — {ex.Message}");
		}
		return ids;
	}

	private void SaveNewEntities(string filename, List<ExtractedEntity> entities)
	{
		try
		{
			string path = Path.Combine(_dataPath, filename);
			string json = JsonConvert.SerializeObject(entities, Formatting.Indented);
			File.WriteAllText(path, json);
		}
		catch (Exception ex)
		{
			Plugin.Log($"RuntimeCardExtractor: save error — {ex.Message}");
		}
	}
}

public class ExtractorResult
{
	public List<ExtractedEntity> AllCards { get; set; } = new();
	public List<ExtractedEntity> AllRelics { get; set; } = new();
	public List<ExtractedEntity> NewCards { get; set; } = new();
	public List<ExtractedEntity> NewRelics { get; set; } = new();
}

public class ExtractedEntity
{
	public string Id { get; set; }
	public string TypeName { get; set; }
	public string EntityType { get; set; }
}
