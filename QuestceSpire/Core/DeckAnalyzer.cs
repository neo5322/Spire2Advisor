using System;
using System.Collections.Generic;
using QuestceSpire.GameBridge;

namespace QuestceSpire.Core;

public class DeckAnalyzer : IDeckAnalyzer
{
	// Injected dependency (falls back to Plugin singleton if not provided)
	private readonly CardPropertyScorer _cardPropertyScorer;

	public DeckAnalyzer(CardPropertyScorer cardPropertyScorer = null)
	{
		_cardPropertyScorer = cardPropertyScorer;
	}

	private CardPropertyScorer GetCardPropertyScorer() => _cardPropertyScorer ?? Plugin.CardPropertyScorer;

	// Job definitions: each job maps to tags that indicate coverage, with a threshold
	private static readonly Dictionary<string, (string[] Tags, int Threshold)> JobDefinitions = new()
	{
		["frontloaded_damage"] = (new[] { "damage", "multi_hit", "vulnerable" }, 3),
		["aoe"] = (new[] { "aoe" }, 2),
		["block"] = (new[] { "block", "dexterity", "weak" }, 3),
		["scaling"] = (new[] { "strength", "dexterity", "focus", "poison_scaling", "scaling", "orb", "shiv_synergy" }, 2),
		["draw"] = (new[] { "draw", "discard" }, 2),
	};

	public DeckAnalysis Analyze(string character, List<CardInfo> deck, TierEngine tierEngine = null, List<RelicInfo> relics = null)
	{
		DeckAnalysis deckAnalysis = new DeckAnalysis
		{
			Character = character,
			TotalCards = deck.Count
		};
		// Relic-aware archetype detection: feed relic synergy tags into tag counts
		// so relics like Shuriken (strength) or Snecko Eye boost archetype detection
		if (relics != null && tierEngine != null)
		{
			foreach (RelicInfo relic in relics)
			{
				var relicTier = tierEngine.GetRelicTier(character, relic.Id);
				if (relicTier?.Synergies != null)
				{
					foreach (string tag in relicTier.Synergies)
					{
						IncrementTag(deckAnalysis.TagCounts, tag.ToLowerInvariant());
					}
				}
			}
		}
		int totalCost = 0;
		int costCards = 0;
		foreach (CardInfo item3 in deck)
		{
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (string tag in item3.Tags ?? new List<string>())
			{
				string item = tag.ToLowerInvariant();
				hashSet.Add(item);
			}
			CardTierEntry cardTier = tierEngine?.GetCardTier(character, item3.Id);
			if (cardTier?.Synergies != null)
			{
				foreach (string synergy in cardTier.Synergies)
				{
					string item2 = synergy.ToLowerInvariant();
					hashSet.Add(item2);
				}
			}
			var cps = GetCardPropertyScorer();
			if (cps != null && (cardTier?.Synergies == null || cardTier.Synergies.Count == 0))
			{
				var computed = cps.ComputeScore(item3.Id);
				if (computed.SynergyTags != null)
				{
					foreach (string synTag in computed.SynergyTags)
						hashSet.Add(synTag.ToLowerInvariant());
				}
			}
			foreach (string item4 in hashSet)
			{
				IncrementTag(deckAnalysis.TagCounts, item4);
			}
			// Energy curve bucketing (0-5+)
			int costBucket = Math.Max(0, Math.Min(item3.Cost, 5));
			if (deckAnalysis.EnergyCurve.ContainsKey(costBucket))
				deckAnalysis.EnergyCurve[costBucket]++;
			else
				deckAnalysis.EnergyCurve[costBucket] = 1;
			// Average cost tracking
			if (item3.Cost >= 0)
			{
				totalCost += item3.Cost;
				costCards++;
			}
			// Type counting
			string typeLower = item3.Type?.ToLowerInvariant() ?? "";
			if (typeLower == "attack") deckAnalysis.AttackCount++;
			else if (typeLower == "skill") deckAnalysis.SkillCount++;
			else if (typeLower == "power") deckAnalysis.PowerCount++;
		}
		// Compute average cost
		deckAnalysis.AverageCost = costCards > 0 ? (float)totalCost / costCards : 1.0f;
		// Compute job coverage
		foreach (var (jobName, jobDef) in JobDefinitions)
		{
			int count = 0;
			foreach (string tag in jobDef.Tags)
			{
				if (deckAnalysis.TagCounts.TryGetValue(tag, out int c))
					count += c;
			}
			deckAnalysis.JobCoverage[jobName] = Math.Min(1f, (float)count / jobDef.Threshold);
		}
		string key = character?.ToLowerInvariant() ?? "";
		if (!ArchetypeDefinitions.ByCharacter.TryGetValue(key, out var value))
		{
			return deckAnalysis;
		}
		foreach (Archetype item5 in value)
		{
			int num = 0;
			foreach (string coreTag in item5.CoreTags)
			{
				if (deckAnalysis.TagCounts.TryGetValue(coreTag, out var value2))
				{
					num += value2;
				}
			}
			int num2 = 0;
			foreach (string supportTag in item5.SupportTags)
			{
				if (deckAnalysis.TagCounts.TryGetValue(supportTag, out var value3))
				{
					num2 += value3;
				}
			}
			bool num3 = num >= item5.CoreThreshold;
			bool flag = num2 >= item5.SupportThreshold;
			if (num3 || (num >= 2 && flag))
			{
				float num4 = item5.CoreThreshold > 0 ? (float)num / ((float)item5.CoreThreshold * 2f) : 0f;
				if (flag)
				{
					num4 += 0.2f;
				}
				// Density normalization: scale by how concentrated the archetype is.
				// Target deck size is ~20 cards. Smaller decks get density bonus (capped at 1.3x),
				// larger decks get density penalty (floored at 0.7x).
				if (deckAnalysis.TotalCards > 0)
				{
					var cfg = ScoringConfig.Instance;
					float rawDensity = cfg.DensityTargetDeckSize / Math.Max(deckAnalysis.TotalCards, cfg.DensityTargetDeckSize * 0.75f);
					float densityFactor = Math.Clamp(rawDensity, cfg.DensityMinFactor, cfg.DensityMaxFactor);
					num4 *= densityFactor;
				}
				num4 = Math.Min(num4, 1f);
				deckAnalysis.DetectedArchetypes.Add(new ArchetypeMatch
				{
					Archetype = item5,
					Strength = num4,
					CoreCount = num,
					SupportCount = num2
				});
			}
		}
		deckAnalysis.DetectedArchetypes.Sort((ArchetypeMatch a, ArchetypeMatch b) => b.Strength.CompareTo(a.Strength));

		// Community archetype detection layer
		try
		{
			var cd = Plugin.CommunityData;
			if (cd != null && cd.IsLoaded)
			{
				// Build Korean name list for all deck cards
				var deckKoreanNames = new List<string>(deck.Count);
				foreach (CardInfo card in deck)
				{
					string localized = GameStateReader.GetLocalizedName("card", card.Id);
					deckKoreanNames.Add(localized ?? card.Name ?? card.Id);
				}

				// Detect community archetype
				var communityArch = cd.DetectArchetype(character, deckKoreanNames);
				if (communityArch != null)
				{
					deckAnalysis.CommunityArchetype = communityArch;

					// Get build completion state
					string archId = communityArch.Id;
					if (archId != null)
					{
						var completion = cd.GetBuildCompletion(character, archId, deckKoreanNames);
						if (completion != null)
						{
							deckAnalysis.BuildCompletion = completion;
							// Extract missing must/rec cards
							if (completion.MissingMust != null)
								deckAnalysis.MissingMustCards.AddRange(completion.MissingMust.Where(m => m != null));
							if (completion.MissingRec != null)
								deckAnalysis.MissingRecCards.AddRange(completion.MissingRec.Where(r => r != null));
						}
					}
				}
			}
		}
		catch (Exception ex) { Plugin.Log($"DeckAnalyzer: community archetype detection error: {ex.Message}"); }

		return deckAnalysis;
	}

	private static void IncrementTag(Dictionary<string, int> tagCounts, string key)
	{
		if (tagCounts.ContainsKey(key))
		{
			tagCounts[key]++;
		}
		else
		{
			tagCounts[key] = 1;
		}
	}
}
