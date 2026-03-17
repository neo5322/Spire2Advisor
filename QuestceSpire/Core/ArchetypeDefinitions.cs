using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace QuestceSpire.Core;

public static class ArchetypeDefinitions
{
	public static Dictionary<string, List<Archetype>> ByCharacter { get; private set; } = BuildHardcodedDefaults();

	/// <summary>
	/// Load archetypes from JSON file, falling back to hardcoded defaults if file is missing or invalid.
	/// </summary>
	public static void LoadFromJson(string jsonPath)
	{
		if (!File.Exists(jsonPath))
		{
			Plugin.Log("ArchetypeDefinitions: archetypes.json not found, using hardcoded defaults.");
			ByCharacter = BuildHardcodedDefaults();
			return;
		}
		try
		{
			string json = File.ReadAllText(jsonPath);
			var loaded = JsonConvert.DeserializeObject<Dictionary<string, List<Archetype>>>(json);
			if (loaded != null && loaded.Count > 0)
			{
				ByCharacter = loaded;
				Plugin.Log($"ArchetypeDefinitions loaded {loaded.Count} characters from JSON.");
			}
			else
			{
				Plugin.Log("ArchetypeDefinitions: JSON was empty, using hardcoded defaults.");
				ByCharacter = BuildHardcodedDefaults();
			}
		}
		catch (Exception ex)
		{
			Plugin.Log($"ArchetypeDefinitions: failed to load JSON ({ex.Message}), using hardcoded defaults.");
			ByCharacter = BuildHardcodedDefaults();
		}
	}

	private static Dictionary<string, List<Archetype>> BuildHardcodedDefaults()
	{
		return new Dictionary<string, List<Archetype>>
		{
			["ironclad"] = new List<Archetype>
			{
				new Archetype
				{
					Id = "strength",
					DisplayName = "힘",
					CoreTags = new List<string> { "strength", "scaling" },
					SupportTags = new List<string> { "multi_hit", "vulnerable" },
					CoreThreshold = 3,
					SupportThreshold = 2
				},
				new Archetype
				{
					Id = "exhaust",
					DisplayName = "소멸",
					CoreTags = new List<string> { "exhaust" },
					SupportTags = new List<string> { "draw", "self_damage" },
					CoreThreshold = 4,
					SupportThreshold = 2
				},
				new Archetype
				{
					Id = "block",
					DisplayName = "바리케이드 / 방어",
					CoreTags = new List<string> { "block", "dexterity" },
					SupportTags = new List<string> { "scaling", "weak" },
					CoreThreshold = 4,
					SupportThreshold = 1
				},
				new Archetype
				{
					Id = "self_damage",
					DisplayName = "자해 / 부패",
					CoreTags = new List<string> { "self_damage" },
					SupportTags = new List<string> { "strength", "exhaust" },
					CoreThreshold = 3,
					SupportThreshold = 2
				}
			},
			["silent"] = new List<Archetype>
			{
				new Archetype
				{
					Id = "poison",
					DisplayName = "독",
					CoreTags = new List<string> { "poison", "poison_scaling" },
					SupportTags = new List<string> { "weak", "scaling" },
					CoreThreshold = 3,
					SupportThreshold = 1
				},
				new Archetype
				{
					Id = "shiv",
					DisplayName = "칼날",
					CoreTags = new List<string> { "shiv", "shiv_synergy" },
					SupportTags = new List<string> { "dexterity", "draw" },
					CoreThreshold = 3,
					SupportThreshold = 2
				},
				new Archetype
				{
					Id = "discard",
					DisplayName = "버리기",
					CoreTags = new List<string> { "discard", "discard_synergy" },
					SupportTags = new List<string> { "draw", "retain" },
					CoreThreshold = 3,
					SupportThreshold = 2
				},
				new Archetype
				{
					Id = "dexterity",
					DisplayName = "민첩 / 방어",
					CoreTags = new List<string> { "dexterity", "block" },
					SupportTags = new List<string> { "weak", "draw" },
					CoreThreshold = 3,
					SupportThreshold = 2
				}
			},
			["defect"] = new List<Archetype>
			{
				new Archetype
				{
					Id = "lightning",
					DisplayName = "번개 오브",
					CoreTags = new List<string> { "lightning", "orb" },
					SupportTags = new List<string> { "focus", "evoke" },
					CoreThreshold = 3,
					SupportThreshold = 2
				},
				new Archetype
				{
					Id = "frost",
					DisplayName = "냉기 / 집중",
					CoreTags = new List<string> { "frost", "focus" },
					SupportTags = new List<string> { "orb", "block" },
					CoreThreshold = 3,
					SupportThreshold = 2
				},
				new Archetype
				{
					Id = "dark",
					DisplayName = "어둠 오브",
					CoreTags = new List<string> { "dark", "orb" },
					SupportTags = new List<string> { "focus", "evoke" },
					CoreThreshold = 2,
					SupportThreshold = 2
				},
				new Archetype
				{
					Id = "all_orbs",
					DisplayName = "전체 오브 / 집중",
					CoreTags = new List<string> { "focus", "orb" },
					SupportTags = new List<string> { "lightning", "frost", "dark", "channel" },
					CoreThreshold = 3,
					SupportThreshold = 3
				},
				new Archetype
				{
					Id = "zero_cost",
					DisplayName = "0코스트",
					CoreTags = new List<string> { "zero_cost" },
					SupportTags = new List<string> { "draw", "scaling" },
					CoreThreshold = 4,
					SupportThreshold = 1
				}
			},
			["regent"] = new List<Archetype>
			{
				new Archetype
				{
					Id = "stellar",
					DisplayName = "항성 / 별",
					CoreTags = new List<string> { "stellar", "stars" },
					SupportTags = new List<string> { "draw", "scaling", "zero_cost" },
					CoreThreshold = 3,
					SupportThreshold = 2
				},
				new Archetype
				{
					Id = "authority",
					DisplayName = "권위 / 대장간",
					CoreTags = new List<string> { "authority", "forge" },
					SupportTags = new List<string> { "scaling", "damage" },
					CoreThreshold = 3,
					SupportThreshold = 2
				},
				new Archetype
				{
					Id = "minion",
					DisplayName = "소환수",
					CoreTags = new List<string> { "minion" },
					SupportTags = new List<string> { "damage", "scaling", "block" },
					CoreThreshold = 2,
					SupportThreshold = 2
				},
				new Archetype
				{
					Id = "cosmic",
					DisplayName = "우주 피해",
					CoreTags = new List<string> { "cosmic", "aoe" },
					SupportTags = new List<string> { "stellar", "stars", "scaling" },
					CoreThreshold = 3,
					SupportThreshold = 2
				}
			},
			["necrobinder"] = new List<Archetype>
			{
				new Archetype
				{
					Id = "doom",
					DisplayName = "파멸 / 디버프",
					CoreTags = new List<string> { "doom", "debuff" },
					SupportTags = new List<string> { "block", "scaling" },
					CoreThreshold = 3,
					SupportThreshold = 2
				},
				new Archetype
				{
					Id = "soul",
					DisplayName = "영혼 / 소멸 순환",
					CoreTags = new List<string> { "soul", "exhaust" },
					SupportTags = new List<string> { "draw", "scaling" },
					CoreThreshold = 3,
					SupportThreshold = 2
				},
				new Archetype
				{
					Id = "minion",
					DisplayName = "소환수",
					CoreTags = new List<string> { "minion", "summon" },
					SupportTags = new List<string> { "damage", "scaling" },
					CoreThreshold = 3,
					SupportThreshold = 2
				},
				new Archetype
				{
					Id = "death",
					DisplayName = "죽음 / 사신",
					CoreTags = new List<string> { "death", "exhaust" },
					SupportTags = new List<string> { "aoe", "damage" },
					CoreThreshold = 3,
					SupportThreshold = 2
				}
			}
		};
	}
}
