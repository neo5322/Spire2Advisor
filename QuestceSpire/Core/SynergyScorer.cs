using System;
using System.Collections.Generic;
using System.Linq;
using QuestceSpire.GameBridge;

namespace QuestceSpire.Core;

public class SynergyScorer : ICardScorer, IRelicScorer
{
	private ScoringConfig Cfg => ScoringConfig.Instance;

	// Injected dependency (falls back to Plugin singleton if not provided)
	private readonly CardPropertyScorer _cardPropertyScorer;

	public SynergyScorer(CardPropertyScorer cardPropertyScorer = null)
	{
		_cardPropertyScorer = cardPropertyScorer;
	}

	private CardPropertyScorer GetCardPropertyScorer() => _cardPropertyScorer ?? Plugin.CardPropertyScorer;

	/// <summary>Isolate RunDatabase access to a single helper. Core should not scatter Tracking references.</summary>
	private string GetRunDatabaseConnectionString() => Plugin.RunDatabase?.ConnectionString;

	/// <summary>Isolate upgrade value lookup from RunDatabase.</summary>
	private dynamic GetUpgradeValue(string cardId, string character) => Plugin.RunDatabase?.GetUpgradeValue(cardId, character);

	/// <summary>Isolate CoPickSynergyComputer access to a single helper.</summary>
	private float GetCoPickBonusFromComputer(string cardId, List<string> deckCardIds, string character)
		=> Plugin.CoPickSynergyComputer?.GetCoPickBonus(cardId, deckCardIds, character) ?? 0f;

	// Job-to-tag mapping for card evaluation
	private static readonly Dictionary<string, string[]> JobTags = new()
	{
		["frontloaded_damage"] = new[] { "damage", "multi_hit", "vulnerable" },
		["aoe"] = new[] { "aoe" },
		["block"] = new[] { "block", "dexterity", "weak" },
		["scaling"] = new[] { "strength", "dexterity", "focus", "poison_scaling", "scaling", "orb", "shiv_synergy" },
		["draw"] = new[] { "draw", "discard" },
	};

	private static readonly HashSet<string> ScalingTags = new HashSet<string> { "strength", "dexterity", "focus", "poison_scaling", "scaling", "orb", "shiv_synergy" };

	// --- ICardScorer implementation ---
	public List<ScoredCard> ScoreOfferings(List<CardInfo> offerings, DeckAnalysis deckAnalysis, string character, int actNumber, int floorNumber)
	{
		return ScoreOfferings(offerings, deckAnalysis, character, actNumber, floorNumber, null, null);
	}

	public List<ScoredCard> ScoreOfferings(List<CardInfo> offerings, DeckAnalysis deckAnalysis, string character, int actNumber, int floorNumber, TierEngine tierEngine, AdaptiveScorer adaptiveScorer = null)
	{
		// Input validation (#7)
		if (offerings == null || offerings.Count == 0 || deckAnalysis == null || character == null)
			return new List<ScoredCard>();

		// Build deck card ID list for co-pick synergy lookup
		List<string> deckCardIds = null;
		try
		{
			var gs = GameStateReader.ReadCurrentState();
			if (gs?.DeckCards != null && gs.DeckCards.Count > 0)
				deckCardIds = gs.DeckCards.ConvertAll(c => c.Id);
		}
		catch (Exception ex) { Plugin.Log($"SynergyScorer: failed to read game state for deck cards: {ex.Message}"); }

		List<ScoredCard> list = new List<ScoredCard>();
		foreach (CardInfo offering in offerings)
		{
			CardTierEntry cardTier = tierEngine?.GetCardTier(character, offering.Id);
			ScoredCard item = ScoreCard(offering, cardTier, deckAnalysis, actNumber, floorNumber, character, adaptiveScorer, deckCardIds);
			item.Price = offering.Price;
			list.Add(item);
		}
		// Mark best pick without reordering — preserve game's card order for badge alignment
		if (list.Count > 0)
		{
			int bestIdx = 0;
			for (int i = 1; i < list.Count; i++)
			{
				if (list[i].FinalScore > list[bestIdx].FinalScore)
					bestIdx = i;
			}
			list[bestIdx].IsBestPick = true;
		}
		return list;
	}

	/// <summary>
	/// Rank deck cards for removal using simple priority buckets.
	/// Returns list sorted by removal priority (best to remove first).
	/// </summary>
	public List<ScoredCard> ScoreForRemoval(List<CardInfo> deck, DeckAnalysis deckAnalysis, string character, int actNumber, int floorNumber, TierEngine tierEngine, AdaptiveScorer adaptiveScorer = null)
	{
		List<ScoredCard> list = new List<ScoredCard>();
		foreach (CardInfo card in deck)
		{
			string type = card.Type?.ToLowerInvariant() ?? "";
			bool isCurse = type == "curse" || type == "status";
			bool isStrike = card.Tags != null && card.Tags.Contains("strike");
			bool isDefend = card.Tags != null && card.Tags.Contains("defend");

			float score;
			string reason;
			TierGrade grade;

			if (isCurse)
			{
				score = 5.0f; grade = TierGrade.S;
				reason = "Curse/Status — always remove first";
			}
			else if ((isStrike || isDefend) && !card.Upgraded)
			{
				score = 4.0f; grade = TierGrade.A;
				reason = isStrike ? "Basic Strike — safe removal to thin deck" : "Basic Defend — safe removal to thin deck";
			}
			else if ((isStrike || isDefend) && card.Upgraded)
			{
				score = 3.0f; grade = TierGrade.B;
				reason = "Upgraded basic — still worth removing in lean decks";
			}
			else
			{
				// Non-basic: use adaptive data if available, else static tier
				CardTierEntry cardTier = tierEngine.GetCardTier(character, card.Id);
				TierGrade cardGrade = cardTier != null ? TierEngine.ParseGrade(cardTier.BaseTier) : TierGrade.C;
				float cardScore = (float)cardGrade;
				if (adaptiveScorer != null && character != null)
				{
					cardScore = adaptiveScorer.GetAdaptiveCardScore(character, card.Id, cardScore, deckAnalysis);
				}

				// v0.11.3: Card usage data — boost removal for rarely played cards
				float usageBonus = 0f;
				string usageReason = null;
				try
				{
					var connStr = GetRunDatabaseConnectionString();
					if (connStr != null && character != null)
					{
						using var conn = new Microsoft.Data.Sqlite.SqliteConnection(connStr);
						conn.Open();
						using var cmd = conn.CreateCommand();
						cmd.CommandText = "SELECT avg_plays_per_combat, effectiveness FROM card_usage_stats WHERE card_id=@id AND character=@char";
						cmd.Parameters.AddWithValue("@id", card.Id);
						cmd.Parameters.AddWithValue("@char", character);
						using var reader = cmd.ExecuteReader();
						if (reader.Read())
						{
							float avgPlays = reader.GetFloat(0);
							float effectiveness = reader.GetFloat(1);
							if (avgPlays < 0.5f)
							{
								usageBonus = 1.0f;
								usageReason = $"사용률 {avgPlays:F1}회/전투 — 제거 1순위";
							}
							else if (effectiveness < 0.3f)
							{
								usageBonus = 0.5f;
								usageReason = $"효율 {effectiveness:P0} — 제거 추천";
							}
						}
					}
				}
				catch (Exception ex) { Plugin.Log($"SynergyScorer: failed to compute card usage for removal scoring: {ex.Message}"); }

				// Invert: bad cards get high removal score (5 - score, so F=5→S removal, S=0→F removal)
				score = Math.Max(0f, 5.0f - cardScore + usageBonus);
				grade = TierEngine.ScoreToGrade(score);
				reason = usageReason ?? (cardScore < 2.0f ? "Weak card — strong removal candidate"
					: cardScore < 3.0f ? "Below average — consider removing"
					: "Decent card — probably keep");
			}

			list.Add(new ScoredCard
			{
				Id = card.Id,
				Name = card.Name ?? card.Id,
				Type = card.Type,
				Cost = card.Cost,
				BaseTier = grade,
				FinalScore = score,
				FinalGrade = grade,
				SynergyReasons = new List<string> { reason },
				AntiSynergyReasons = new List<string>(),
				Notes = "",
				Upgraded = card.Upgraded,
				ScoreSource = "removal"
			});
		}
		list.Sort((ScoredCard a, ScoredCard b) => b.FinalScore.CompareTo(a.FinalScore));
		if (list.Count > 0)
			list[0].IsBestPick = true;
		return list;
	}

	/// <summary>
	/// Score cards for upgrade value by computing the delta between unupgraded and upgraded scores.
	/// Returns list sorted by upgrade delta (best upgrade first).
	/// </summary>
	public List<ScoredCard> ScoreForUpgrade(List<CardInfo> candidates, DeckAnalysis deckAnalysis, string character, int actNumber, int floorNumber, TierEngine tierEngine, AdaptiveScorer adaptiveScorer = null)
	{
		var list = new List<ScoredCard>();
		foreach (var card in candidates)
		{
			string baseId = card.Id.EndsWith("+") ? card.Id.Substring(0, card.Id.Length - 1) : card.Id;

			// Skip already-upgraded cards — can't upgrade them again
			if (card.Upgraded)
			{
				var skip = new ScoredCard
				{
					Id = baseId, Name = card.Name ?? baseId, Type = card.Type, Cost = card.Cost,
					Upgraded = true, FinalScore = 0f, FinalGrade = TierGrade.F, UpgradeDelta = 0f,
					SynergyReasons = new List<string> { "Already upgraded" },
					AntiSynergyReasons = new List<string>(), Notes = "", ScoreSource = "static"
				};
				list.Add(skip);
				continue;
			}

			// Score the card as-is (unupgraded) to get its base strength
			var currentCard = new CardInfo
			{
				Id = baseId, Name = card.Name, Cost = card.Cost,
				Type = card.Type, Rarity = card.Rarity, Upgraded = false, Tags = card.Tags
			};
			CardTierEntry currentTier = tierEngine.GetCardTier(character, baseId);
			ScoredCard currentScored = ScoreCard(currentCard, currentTier, deckAnalysis, actNumber, floorNumber, character, adaptiveScorer);

			// Score the upgraded version
			var upgradedCard = new CardInfo
			{
				Id = baseId + "+", Name = (card.Name ?? baseId) + "+", Cost = card.Cost,
				Type = card.Type, Rarity = card.Rarity, Upgraded = true, Tags = card.Tags
			};
			CardTierEntry upgradedTier = tierEngine.GetCardTier(character, baseId + "+") ?? currentTier;
			ScoredCard upgradedScored = ScoreCard(upgradedCard, upgradedTier, deckAnalysis, actNumber, floorNumber, character, adaptiveScorer);

			float delta = upgradedScored.FinalScore - currentScored.FinalScore;

			// Upgrade priority = base card strength (better cards benefit more from upgrades)
			// Use the unupgraded score as the primary signal since delta is usually flat
			float baseStrength = currentScored.FinalScore;
			// Bonus for cards that have actual separate upgrade tier entries
			bool hasSeparateUpgradeTier = tierEngine.GetCardTier(character, baseId + "+") != null;
			float tierDeltaBonus = hasSeparateUpgradeTier ? delta * 2f : 0f;

			// Bonus from DB upgrade value data (win-rate delta when upgraded)
			float upgradeValueBonus = 0f;
			var upgradeData = GetUpgradeValue(baseId, character);
			if (upgradeData != null && upgradeData.SampleSize >= 3 && upgradeData.UpgradeWinDelta > 0.02f)
			{
				upgradeValueBonus = Math.Min(0.5f, upgradeData.UpgradeWinDelta * 3f);
			}

			// Final upgrade priority score: base strength + any real tier delta + DB upgrade value
			float upgradePriority = Math.Min(5.5f, baseStrength + tierDeltaBonus + upgradeValueBonus);

			upgradedScored.UpgradeDelta = delta;
			upgradedScored.Id = baseId;
			upgradedScored.Name = card.Name ?? baseId;
			upgradedScored.Upgraded = false;
			upgradedScored.FinalScore = Math.Max(0f, upgradePriority);
			upgradedScored.FinalGrade = TierEngine.ScoreToGrade(upgradedScored.FinalScore);
			upgradedScored.ScoreSource = currentScored.ScoreSource;
			upgradedScored.SynergyReasons.Insert(0, $"Base strength {baseStrength:F1} — upgrade your best cards first");
			if (upgradeValueBonus > 0.01f)
				upgradedScored.SynergyReasons.Add($"+{upgradeValueBonus:F2} upgrade win-rate boost (data: +{upgradeData.UpgradeWinDelta:P0})");
			list.Add(upgradedScored);
		}
		list.Sort((a, b) => b.FinalScore.CompareTo(a.FinalScore));
		if (list.Count > 0)
			list[0].IsBestPick = true;
		return list;
	}

	// --- IRelicScorer implementation ---
	public List<ScoredRelic> ScoreRelicOfferings(List<RelicInfo> offerings, DeckAnalysis deckAnalysis, string character)
	{
		return ScoreRelicOfferings(offerings, deckAnalysis, character, 1, 1, null, null);
	}

	public List<ScoredRelic> ScoreRelicOfferings(List<RelicInfo> offerings, DeckAnalysis deckAnalysis, string character, int actNumber, int floorNumber, TierEngine tierEngine, AdaptiveScorer adaptiveScorer = null)
	{
		List<ScoredRelic> list = new List<ScoredRelic>();
		foreach (RelicInfo offering in offerings)
		{
			RelicTierEntry relicTier = tierEngine?.GetRelicTier(character, offering.Id);
			ScoredRelic item = ScoreRelic(offering, relicTier, deckAnalysis, actNumber, floorNumber, character, adaptiveScorer);
			item.Price = offering.Price;
			list.Add(item);
		}
		// Mark best pick without reordering — preserve game's order for badge alignment
		if (list.Count > 0)
		{
			int bestIdx = 0;
			for (int i = 1; i < list.Count; i++)
			{
				if (list[i].FinalScore > list[bestIdx].FinalScore)
					bestIdx = i;
			}
			list[bestIdx].IsBestPick = true;
		}
		return list;
	}

	/// <summary>Returns saturation multiplier based on how many cards already have this tag.</summary>
	private float GetSaturationMult(DeckAnalysis deck, string tag)
	{
		if (!deck.TagCounts.TryGetValue(tag, out int count)) return 1f;
		if (count >= Cfg.SaturationHardCap) return Cfg.SaturationHardMult;
		if (count >= Cfg.SaturationSoftCap) return Cfg.SaturationSoftMult;
		return 1f;
	}

	/// <summary>Checks if a card's synergy tags fill any of the 5 functional "jobs".</summary>
	private float ComputeJobGapBonus(List<string> cardSynTags, DeckAnalysis deck, List<string> reasons)
	{
		float bestBonus = 0f;
		string bestJob = null;
		foreach (var (jobName, jobTags) in JobTags)
		{
			bool fills = false;
			foreach (string tag in cardSynTags)
			{
				if (Array.IndexOf(jobTags, tag) >= 0) { fills = true; break; }
			}
			if (!fills) continue;
			float gap = deck.JobGap(jobName);
			if (gap <= 0.1f) continue; // job already covered
			// Non-linear curve: reward filling critical gaps more (#5)
			float bonus = gap * Cfg.JobGapMaxBonus * (2f - gap);
			if (bonus > bestBonus) { bestBonus = bonus; bestJob = jobName; }
		}
		if (bestBonus > 0.05f && bestJob != null)
		{
			reasons.Add($"+{bestBonus:F1} fills {bestJob.Replace('_', ' ')} gap");
		}
		return bestBonus;
	}

	
	private ScoredCard ScoreCard(CardInfo card, CardTierEntry tierEntry, DeckAnalysis deckAnalysis, int actNumber, int floorNumber, string character = null, AdaptiveScorer adaptiveScorer = null, List<string> deckCardIds = null)
	{
		// 1. Base score resolution
		var (tierGrade, rawScore, computedSynTags, scoreSource) = ResolveBaseScore(card, tierEntry, character);
		float baseScore = ApplyAdaptiveScoring(rawScore, character, card.Id, deckAnalysis, adaptiveScorer, ref scoreSource);

		// 2. Prepare tag lists
		var cardSynTags = ResolveSynergyTags(tierEntry, computedSynTags, card);
		var cardAntiTags = tierEntry?.AntiSynergies ?? new List<string>();

		// 3. Apply scoring components
		float score = baseScore;
		float synergyDelta = 0f;
		float floorAdjust = 0f;
		float deckSizeAdjust = 0f;
		var synReasons = new List<string>();
		var antiReasons = new List<string>();

		synergyDelta += ApplyGraduatedSynergy(cardSynTags, deckAnalysis, ref score, synReasons);
		synergyDelta += ApplyAntiSynergy(cardAntiTags, deckAnalysis, ref score, antiReasons);
		synergyDelta = ClampAntiSynergy(synergyDelta, ref score);

		float jobBonus = ComputeJobGapBonus(cardSynTags, deckAnalysis, synReasons);
		score += jobBonus; synergyDelta += jobBonus;

		float coPickBonus = ApplyCoPickSynergy(card.Id, character, deckCardIds, ref score, synReasons);
		synergyDelta += coPickBonus;

		score += ApplyEnergyCurveAdjust(card.Cost, deckAnalysis, synReasons, antiReasons);
		score += ApplyCardTypeBalance(card.Type, deckAnalysis, floorNumber, synReasons, antiReasons);
		score += ApplyAoEBonus(cardSynTags, deckAnalysis, synReasons);

		floorAdjust = ApplyFloorScoring(cardSynTags, deckAnalysis, floorNumber, synReasons);
		score += floorAdjust;

		synergyDelta += ApplyMissingPieceBonus(cardSynTags, deckAnalysis, ref score, synReasons);

		deckSizeAdjust = ApplyDeckSizeAdjust(deckAnalysis, score, antiReasons);
		score += deckSizeAdjust;

		float upgradeAdjust = ApplyUpgradeBonus(card.Upgraded, ref score, synReasons);

		// 4. Clamp and build result
		score = Math.Clamp(score, 0f, 6.0f);
		return BuildScoredCard(card, tierGrade, score, scoreSource, baseScore,
			synergyDelta, floorAdjust, deckSizeAdjust, upgradeAdjust,
			synReasons, antiReasons, tierEntry?.Notes ?? "");
	}

	// --- Extracted scoring methods for ScoreCard ---

	/// <summary>Resolve base score from tier entry, computed properties, or default.</summary>
	private (TierGrade grade, float score, List<string> synTags, string source)
		ResolveBaseScore(CardInfo card, CardTierEntry tierEntry, string character)
	{
		if (tierEntry != null)
		{
			var grade = TierEngine.ParseGrade(tierEntry.BaseTier);
			return (grade, (float)grade, null, "static");
		}
		var cps = GetCardPropertyScorer();
		if (cps != null)
		{
			var computed = cps.ComputeScore(card.Id);
			return (TierEngine.ScoreToGrade(computed.Score), computed.Score, computed.SynergyTags, "computed");
		}
		return (TierGrade.C, (float)TierGrade.C, null, "default");
	}

	/// <summary>Blend raw score with adaptive community data if available.</summary>
	private float ApplyAdaptiveScoring(float rawScore, string character, string cardId,
		DeckAnalysis deck, AdaptiveScorer adaptive, ref string scoreSource)
	{
		bool usedAdaptive = adaptive != null && character != null;
		float baseScore = usedAdaptive ? adaptive.GetAdaptiveCardScore(character, cardId, rawScore, deck) : rawScore;
		if (usedAdaptive) scoreSource = "adaptive";
		return baseScore;
	}

	/// <summary>Resolve which synergy tags to use: tier > computed > card tags.</summary>
	private List<string> ResolveSynergyTags(CardTierEntry tierEntry, List<string> computedTags, CardInfo card)
	{
		if (tierEntry?.Synergies != null && tierEntry.Synergies.Count > 0)
			return tierEntry.Synergies;
		return computedTags
			?? (card.Tags != null ? card.Tags.ConvertAll((string t) => t.ToLowerInvariant()) : new List<string>());
	}

	/// <summary>Graduated synergy with diminishing returns + saturation. Shared by cards and relics.</summary>
	private float ApplyGraduatedSynergy(List<string> tags, DeckAnalysis deck, ref float score, List<string> reasons)
	{
		float totalBoost = 0f;
		int matchCount = 0;
		foreach (ArchetypeMatch arch in deck.DetectedArchetypes)
		{
			if (matchCount >= Cfg.SynergyDiminishing.Length) break;
			foreach (string tag in tags)
			{
				if (ArchetypeUtils.MatchesAnyTag(arch.Archetype, tag) || arch.Archetype.Id == tag)
				{
					float baseBoost = 0.3f + arch.Strength * 0.5f;
					float dimMult = matchCount < Cfg.SynergyDiminishing.Length ? Cfg.SynergyDiminishing[matchCount] : 0f;
					float satMult = GetSaturationMult(deck, tag);
					float boost = baseBoost * dimMult * satMult;
					score += boost;
					totalBoost += boost;
					string satNote = satMult < 1f ? $" (sat {satMult:P0})" : "";
					reasons.Add($"+{boost:F2} {arch.Archetype.DisplayName}{satNote}");
					matchCount++;
					break;
				}
			}
		}
		return totalBoost;
	}

	/// <summary>Anti-synergy detection against detected archetypes. Shared by cards and relics.</summary>
	private float ApplyAntiSynergy(List<string> antiTags, DeckAnalysis deck, ref float score, List<string> reasons)
	{
		float totalPenalty = 0f;
		foreach (ArchetypeMatch arch in deck.DetectedArchetypes)
		{
			foreach (string tag in antiTags)
			{
				if (ArchetypeUtils.MatchesAnyTag(arch.Archetype, tag) || arch.Archetype.Id == tag)
				{
					score -= Cfg.AntiSynergyPenalty;
					totalPenalty -= Cfg.AntiSynergyPenalty;
					reasons.Add($"-{Cfg.AntiSynergyPenalty:F1} conflicts with {arch.Archetype.DisplayName}");
					break;
				}
			}
		}
		return totalPenalty;
	}

	/// <summary>Clamp cumulative anti-synergy to cap, adjusting score to compensate. Shared by cards and relics.</summary>
	private float ClampAntiSynergy(float synergyDelta, ref float score)
	{
		if (synergyDelta < Cfg.AntiSynergyCap)
		{
			float excess = Cfg.AntiSynergyCap - synergyDelta;
			score += excess;
			return Cfg.AntiSynergyCap;
		}
		return synergyDelta;
	}

	/// <summary>Co-pick synergy bonus from community data for cards that pair well with existing deck.</summary>
	private float ApplyCoPickSynergy(string cardId, string character, List<string> deckCardIds, ref float score, List<string> reasons)
	{
		if (character == null || deckCardIds == null || deckCardIds.Count < 3)
			return 0f;
		float coPickBonus = GetCoPickBonusFromComputer(cardId, deckCardIds, character);
		if (coPickBonus <= 0.01f) return 0f;
		coPickBonus = Math.Min(Cfg.CoPickBonusCap, coPickBonus);
		score += coPickBonus;
		reasons.Add($"+{coPickBonus:F2} co-pick synergy (high win-rate pair in deck)");
		return coPickBonus;
	}

	/// <summary>Energy curve penalty/bonus based on card cost and deck average cost.</summary>
	private float ApplyEnergyCurveAdjust(int cost, DeckAnalysis deck, List<string> synReasons, List<string> antiReasons)
	{
		if (cost >= 3 && deck.AverageCost > 2.2f)
		{
			antiReasons.Add($"{Cfg.VeryExpensiveCardPenalty:F1} too expensive (avg cost {deck.AverageCost:F1})");
			return Cfg.VeryExpensiveCardPenalty;
		}
		if (cost >= 3 && deck.AverageCost > 1.8f)
		{
			antiReasons.Add($"{Cfg.ExpensiveCardPenalty:F1} expensive (avg cost {deck.AverageCost:F1})");
			return Cfg.ExpensiveCardPenalty;
		}
		if (cost == 0 && deck.AverageCost > 1.5f)
		{
			synReasons.Add($"+{Cfg.CheapCardBonus:F2} 0-cost helps energy curve");
			return Cfg.CheapCardBonus;
		}
		return 0f;
	}

	/// <summary>Card type balance: power gap bonus or power glut penalty.</summary>
	private float ApplyCardTypeBalance(string cardType, DeckAnalysis deck, int floorNumber, List<string> synReasons, List<string> antiReasons)
	{
		string type = cardType?.ToLowerInvariant() ?? "";
		if (type == "power" && deck.PowerCount == 0 && floorNumber > 6)
		{
			synReasons.Add($"+{Cfg.PowerGapBonus:F1} first power (thins draw pool)");
			return Cfg.PowerGapBonus;
		}
		if (type == "power" && deck.PowerCount >= 4)
		{
			antiReasons.Add($"{Cfg.PowerGlutPenalty:F1} too many powers already");
			return Cfg.PowerGlutPenalty;
		}
		return 0f;
	}

	/// <summary>AoE gap detection: bonus when deck lacks AoE coverage.</summary>
	private float ApplyAoEBonus(List<string> tags, DeckAnalysis deck, List<string> reasons)
	{
		if (!tags.Contains("aoe")) return 0f;
		deck.TagCounts.TryGetValue("aoe", out int deckAoE);
		if (deckAoE == 0)
		{
			reasons.Add($"+{Cfg.AoEGapBonus:F1} deck needs AoE");
			return Cfg.AoEGapBonus;
		}
		if (deckAoE == 1)
		{
			float smallAoE = 0.1f;
			reasons.Add($"+{smallAoE:F1} backup AoE");
			return smallAoE;
		}
		return 0f;
	}

	/// <summary>Floor-aware scoring: early flexibility, mid defense, late scaling.</summary>
	private float ApplyFloorScoring(List<string> tags, DeckAnalysis deck, int floorNumber, List<string> reasons)
	{
		float adjust = 0f;
		bool hasScaling = tags.Any((string s) => ScalingTags.Contains(s));
		bool hasDefense = tags.Any((string s) => s == "block" || s == "dexterity" || s == "weak");
		if (floorNumber >= 1 && floorNumber <= 6 && deck.IsUndefined)
		{
			if (tags.Count >= 2)
			{
				adjust = Cfg.EarlyFloorDamageBonus;
				reasons.Add($"+{Cfg.EarlyFloorDamageBonus:F1} flexible (early floors)");
			}
		}
		else if (floorNumber >= 7 && floorNumber <= 18)
		{
			if (!deck.IsUndefined && hasDefense)
			{
				adjust = Cfg.MidFloorBlockBonus;
				reasons.Add($"+{Cfg.MidFloorBlockBonus:F1} defense (mid floors)");
			}
		}
		else if (floorNumber >= 19)
		{
			if (hasScaling)
			{
				adjust = Cfg.LateFloorScalingBonus;
				reasons.Add($"+{Cfg.LateFloorScalingBonus:F1} scaling (late floors)");
			}
		}
		return adjust;
	}

	/// <summary>Missing piece detection for incomplete archetypes (strength 0.3-0.7).</summary>
	private float ApplyMissingPieceBonus(List<string> tags, DeckAnalysis deck, ref float score, List<string> reasons)
	{
		foreach (ArchetypeMatch arch in deck.DetectedArchetypes)
		{
			if (arch.Strength <= 0.3f || arch.Strength >= 0.7f) continue;
			foreach (string tag in tags)
			{
				if (arch.Archetype.SupportTags.Contains(tag))
				{
					string key = tag.ToLowerInvariant();
					if (!deck.TagCounts.TryGetValue(key, out var val) || val == 0)
					{
						score += Cfg.MissingPieceBonus;
						reasons.Add($"+{Cfg.MissingPieceBonus:F1} fills gap: {tag}");
						return Cfg.MissingPieceBonus;
					}
				}
			}
		}
		return 0f;
	}

	/// <summary>Deck size penalty: thin decks should be selective, bloated decks only take great cards.</summary>
	private float ApplyDeckSizeAdjust(DeckAnalysis deck, float currentScore, List<string> antiReasons)
	{
		int deckSize = deck.TotalCards;
		if (deckSize <= Cfg.ThinDeckThreshold && currentScore < 2.5f)
		{
			antiReasons.Add($"{Cfg.ThinDeckPenalty:F1} be selective (thin deck)");
			return Cfg.ThinDeckPenalty;
		}
		if (deckSize >= Cfg.BloatedDeckThreshold && currentScore < 3.5f)
		{
			antiReasons.Add($"{Cfg.BloatedDeckPenalty:F1} only take great cards (bloated deck)");
			return Cfg.BloatedDeckPenalty;
		}
		return 0f;
	}

	/// <summary>Upgrade bonus for upgraded cards.</summary>
	private float ApplyUpgradeBonus(bool upgraded, ref float score, List<string> synReasons)
	{
		if (!upgraded) return 0f;
		score += Cfg.UpgradeBonus;
		synReasons.Add($"+{Cfg.UpgradeBonus:F1} upgraded");
		return Cfg.UpgradeBonus;
	}

	/// <summary>Build final ScoredCard from all computed components.</summary>
	private static ScoredCard BuildScoredCard(CardInfo card, TierGrade tierGrade, float score,
		string scoreSource, float baseScore, float synergyDelta, float floorAdjust,
		float deckSizeAdjust, float upgradeAdjust, List<string> synReasons,
		List<string> antiReasons, string notes)
	{
		return new ScoredCard
		{
			Id = card.Id,
			Name = card.Name ?? card.Id,
			Type = card.Type,
			Cost = card.Cost,
			BaseTier = tierGrade,
			FinalScore = score,
			FinalGrade = TierEngine.ScoreToGrade(score),
			SynergyReasons = synReasons,
			AntiSynergyReasons = antiReasons,
			Notes = notes,
			BaseScore = baseScore,
			SynergyDelta = synergyDelta,
			FloorAdjust = floorAdjust,
			DeckSizeAdjust = deckSizeAdjust,
			Upgraded = card.Upgraded,
			UpgradeAdjust = upgradeAdjust,
			ScoreSource = scoreSource
		};
	}

	private ScoredRelic ScoreRelic(RelicInfo relic, RelicTierEntry tierEntry, DeckAnalysis deckAnalysis, int actNumber, int floorNumber, string character = null, AdaptiveScorer adaptiveScorer = null)
	{
		TierGrade tierGrade = ((tierEntry != null) ? TierEngine.ParseGrade(tierEntry.BaseTier) : TierGrade.C);
		bool usedAdaptive = adaptiveScorer != null && character != null;
		float baseScore = usedAdaptive ? adaptiveScorer.GetAdaptiveRelicScore(character, relic.Id, (float)tierGrade, deckAnalysis) : (float)tierGrade;
		string scoreSource = tierEntry == null ? "default" : usedAdaptive ? "adaptive" : "static";
		float score = baseScore;
		float synergyDelta = 0f;
		float floorAdjust = 0f;
		List<string> synReasons = new List<string>();
		List<string> antiReasons = new List<string>();
		List<string> relicSynTags = tierEntry?.Synergies ?? new List<string>();
		List<string> relicAntiTags = tierEntry?.AntiSynergies ?? new List<string>();

		// Graduated synergy + anti-synergy + cap (shared with ScoreCard)
		synergyDelta += ApplyGraduatedSynergy(relicSynTags, deckAnalysis, ref score, synReasons);
		synergyDelta += ApplyAntiSynergy(relicAntiTags, deckAnalysis, ref score, antiReasons);
		synergyDelta = ClampAntiSynergy(synergyDelta, ref score);

		// Floor-aware: late-game scaling bonus for relics too
		if (floorNumber >= 19 && relicSynTags.Any((string s) => ScalingTags.Contains(s)))
		{
			score += Cfg.LateFloorScalingBonus;
			floorAdjust += Cfg.LateFloorScalingBonus;
			synReasons.Add($"+{Cfg.LateFloorScalingBonus:F1} scaling (late floors)");
		}
		score = Math.Max(0f, Math.Min(6.0f, score));
		return new ScoredRelic
		{
			Id = relic.Id,
			Name = (relic.Name ?? relic.Id),
			Rarity = relic.Rarity,
			BaseTier = tierGrade,
			FinalScore = score,
			FinalGrade = TierEngine.ScoreToGrade(score),
			SynergyReasons = synReasons,
			AntiSynergyReasons = antiReasons,
			Notes = (tierEntry?.Notes ?? ""),
			BaseScore = baseScore,
			SynergyDelta = synergyDelta,
			FloorAdjust = floorAdjust,
			DeckSizeAdjust = 0f,
			ScoreSource = scoreSource
		};
	}
}
