using System;
using System.Collections.Generic;
using System.Linq;
using QuestceSpire.GameBridge;

namespace QuestceSpire.Core;

public class SynergyScorer : ICardScorer, IRelicScorer
{
	private ScoringConfig Cfg => ScoringConfig.Instance;

	private const float SaturationSoftMult = 0.7f;
	private const float SaturationHardMult = 0.4f;

	private const float AntiSynergyPenalty = 0.6f;

	private const float AntiSynergyCap = -1.2f;

	private const float ThinDeckPenalty = -0.2f;

	private const float BloatedDeckPenalty = -0.4f;

	private const float UpgradeBonus = 0.4f;

	// Energy curve: penalize expensive cards in expensive decks
	private const float ExpensiveCardPenalty = -0.3f;
	private const float VeryExpensiveCardPenalty = -0.5f;
	private const float CheapCardBonus = 0.15f;

	// Card type balance
	private const float PowerGapBonus = 0.2f;
	private const float PowerGlutPenalty = -0.2f;
	private const float AoEGapBonus = 0.3f;

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
		catch { }

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
				// Invert: bad cards get high removal score (5 - score, so F=5→S removal, S=0→F removal)
				score = Math.Max(0f, 5.0f - cardScore);
				grade = TierEngine.ScoreToGrade(score);
				reason = cardScore < 2.0f ? "Weak card — strong removal candidate"
					: cardScore < 3.0f ? "Below average — consider removing"
					: "Decent card — probably keep";
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
			var upgradeData = Plugin.RunDatabase?.GetUpgradeValue(baseId, character);
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
		if (count >= Cfg.SaturationHardCap) return SaturationHardMult;
		if (count >= Cfg.SaturationSoftCap) return SaturationSoftMult;
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

	private const float CoPickBonusCap = 0.4f;

	private ScoredCard ScoreCard(CardInfo card, CardTierEntry tierEntry, DeckAnalysis deckAnalysis, int actNumber, int floorNumber, string character = null, AdaptiveScorer adaptiveScorer = null, List<string> deckCardIds = null)
	{
		TierGrade tierGrade;
		float rawScore;
		List<string> computedSynTags = null;
		string scoreSource;
		if (tierEntry != null)
		{
			tierGrade = TierEngine.ParseGrade(tierEntry.BaseTier);
			rawScore = (float)tierGrade;
			scoreSource = "static";
		}
		else if (Plugin.CardPropertyScorer != null)
		{
			var computed = Plugin.CardPropertyScorer.ComputeScore(card.Id);
			rawScore = computed.Score;
			tierGrade = TierEngine.ScoreToGrade(rawScore);
			computedSynTags = computed.SynergyTags;
			scoreSource = "computed";
		}
		else
		{
			tierGrade = TierGrade.C;
			rawScore = (float)tierGrade;
			scoreSource = "default";
		}
		bool usedAdaptive = adaptiveScorer != null && character != null;
		float baseScore = usedAdaptive ? adaptiveScorer.GetAdaptiveCardScore(character, card.Id, rawScore, deckAnalysis) : rawScore;
		if (usedAdaptive) scoreSource = "adaptive";
		float num = baseScore;
		float synergyDelta = 0f;
		float floorAdjust = 0f;
		float deckSizeAdjust = 0f;
		List<string> synReasons = new List<string>();
		List<string> antiReasons = new List<string>();
		List<string> cardSynTags = (tierEntry?.Synergies != null && tierEntry.Synergies.Count > 0)
			? tierEntry.Synergies
			: computedSynTags
			?? (card.Tags != null ? card.Tags.ConvertAll((string t) => t.ToLowerInvariant()) : new List<string>());
		List<string> cardAntiTags = tierEntry?.AntiSynergies ?? new List<string>();

		// === GRADUATED SYNERGY with diminishing returns + saturation ===
		int matchCount = 0;
		foreach (ArchetypeMatch arch in deckAnalysis.DetectedArchetypes)
		{
			if (matchCount >= Cfg.SynergyDiminishing.Length) break;
			foreach (string tag in cardSynTags)
			{
				if (ArchetypeUtils.MatchesAnyTag(arch.Archetype, tag) || arch.Archetype.Id == tag)
				{
					// Graduated: scales continuously with archetype strength (0.3 at 0.0 → 0.8 at 1.0)
					float baseBoost = 0.3f + arch.Strength * 0.5f;
					// Diminishing per match
					float dimMult = matchCount < Cfg.SynergyDiminishing.Length ? Cfg.SynergyDiminishing[matchCount] : 0f;
					// Saturation: reduce if deck already has many of this tag
					float satMult = GetSaturationMult(deckAnalysis, tag);
					float boost = baseBoost * dimMult * satMult;
					num += boost;
					synergyDelta += boost;
					string satNote = satMult < 1f ? $" (sat {satMult:P0})" : "";
					synReasons.Add($"+{boost:F2} {arch.Archetype.DisplayName}{satNote}");
					matchCount++;
					break;
				}
			}
		}

		// === ANTI-SYNERGY — checks both CoreTags AND SupportTags ===
		foreach (ArchetypeMatch arch in deckAnalysis.DetectedArchetypes)
		{
			foreach (string tag in cardAntiTags)
			{
				if (ArchetypeUtils.MatchesAnyTag(arch.Archetype, tag) || arch.Archetype.Id == tag)
				{
					num -= AntiSynergyPenalty;
					synergyDelta -= AntiSynergyPenalty;
					antiReasons.Add($"-{AntiSynergyPenalty:F1} conflicts with {arch.Archetype.DisplayName}");
					break;
				}
			}
		}
		// Cap anti-synergy penalty to prevent excessive stacking
		if (synergyDelta < AntiSynergyCap)
		{
			float excess = AntiSynergyCap - synergyDelta;
			num += excess;
			synergyDelta = AntiSynergyCap;
		}

		// === JOB GAP BONUS — fills a missing functional role ===
		float jobBonus = ComputeJobGapBonus(cardSynTags, deckAnalysis, synReasons);
		num += jobBonus;
		synergyDelta += jobBonus;

		// === CO-PICK SYNERGY — bonus for cards that pair well with existing deck ===
		float coPickBonus = 0f;
		if (character != null && Plugin.CoPickSynergyComputer != null && deckCardIds != null && deckCardIds.Count >= 3)
		{
			coPickBonus = Plugin.CoPickSynergyComputer.GetCoPickBonus(card.Id, deckCardIds, character);
			if (coPickBonus > 0.01f)
			{
				coPickBonus = Math.Min(CoPickBonusCap, coPickBonus);
				num += coPickBonus;
				synergyDelta += coPickBonus;
				synReasons.Add($"+{coPickBonus:F2} co-pick synergy (high win-rate pair in deck)");
			}
		}

		// === ENERGY CURVE — penalize expensive cards in expensive decks ===
		float energyAdjust = 0f;
		if (card.Cost >= 3 && deckAnalysis.AverageCost > 2.2f)
		{
			energyAdjust = VeryExpensiveCardPenalty;
			antiReasons.Add($"{VeryExpensiveCardPenalty:F1} too expensive (avg cost {deckAnalysis.AverageCost:F1})");
		}
		else if (card.Cost >= 3 && deckAnalysis.AverageCost > 1.8f)
		{
			energyAdjust = ExpensiveCardPenalty;
			antiReasons.Add($"{ExpensiveCardPenalty:F1} expensive (avg cost {deckAnalysis.AverageCost:F1})");
		}
		else if (card.Cost == 0 && deckAnalysis.AverageCost > 1.5f)
		{
			energyAdjust = CheapCardBonus;
			synReasons.Add($"+{CheapCardBonus:F2} 0-cost helps energy curve");
		}
		num += energyAdjust;

		// === CARD TYPE BALANCE ===
		string cardType = card.Type?.ToLowerInvariant() ?? "";
		if (cardType == "power" && deckAnalysis.PowerCount == 0 && floorNumber > 6)
		{
			num += PowerGapBonus;
			synReasons.Add($"+{PowerGapBonus:F1} first power (thins draw pool)");
		}
		else if (cardType == "power" && deckAnalysis.PowerCount >= 4)
		{
			num += PowerGlutPenalty;
			antiReasons.Add($"{PowerGlutPenalty:F1} too many powers already");
		}

		// === AoE BONUS (STS2: harsher on single-target-only decks) ===
		bool cardHasAoE = cardSynTags.Contains("aoe");
		if (cardHasAoE)
		{
			int deckAoE = 0;
			deckAnalysis.TagCounts.TryGetValue("aoe", out deckAoE);
			if (deckAoE == 0)
			{
				num += AoEGapBonus;
				synReasons.Add($"+{AoEGapBonus:F1} deck needs AoE");
			}
			else if (deckAoE == 1)
			{
				float smallAoE = 0.1f;
				num += smallAoE;
				synReasons.Add($"+{smallAoE:F1} backup AoE");
			}
		}

		// === FLOOR-AWARE SCORING — explicit non-overlapping ranges (#4) ===
		bool hasScaling = cardSynTags.Any((string s) => ScalingTags.Contains(s));
		bool hasDefense = cardSynTags.Any((string s) => s == "block" || s == "dexterity" || s == "weak");
		if (floorNumber >= 1 && floorNumber <= 6 && deckAnalysis.IsUndefined)
		{
			// Early: floors 1-6
			if (cardSynTags.Count >= 2)
			{
				num += Cfg.EarlyFloorDamageBonus;
				floorAdjust += Cfg.EarlyFloorDamageBonus;
				synReasons.Add($"+{Cfg.EarlyFloorDamageBonus:F1} flexible (early floors)");
			}
		}
		else if (floorNumber >= 7 && floorNumber <= 18)
		{
			// Mid: floors 7-18
			if (!deckAnalysis.IsUndefined && hasDefense)
			{
				num += Cfg.MidFloorBlockBonus;
				floorAdjust += Cfg.MidFloorBlockBonus;
				synReasons.Add($"+{Cfg.MidFloorBlockBonus:F1} defense (mid floors)");
			}
		}
		else if (floorNumber >= 19)
		{
			// Late: floors 19+
			if (hasScaling)
			{
				num += Cfg.LateFloorScalingBonus;
				floorAdjust += Cfg.LateFloorScalingBonus;
				synReasons.Add($"+{Cfg.LateFloorScalingBonus:F1} scaling (late floors)");
			}
		}

		// === MISSING PIECE BONUS (#6: clearer condition) ===
		bool foundMissing = false;
		foreach (ArchetypeMatch arch in deckAnalysis.DetectedArchetypes)
		{
			if (foundMissing) break;
			if (arch.Strength <= 0.3f || arch.Strength >= 0.7f) continue;
			foreach (string tag in cardSynTags)
			{
				if (arch.Archetype.SupportTags.Contains(tag))
				{
					string key = tag.ToLowerInvariant();
					if (!deckAnalysis.TagCounts.TryGetValue(key, out var val) || val == 0)
					{
						num += Cfg.MissingPieceBonus;
						synergyDelta += Cfg.MissingPieceBonus;
						synReasons.Add($"+{Cfg.MissingPieceBonus:F1} fills gap: {tag}");
						foundMissing = true;
						break;
					}
				}
			}
		}

		// === DECK SIZE (tighter thresholds: 18 thin, 25 bloated) ===
		int deckSize = deckAnalysis.TotalCards;
		if (deckSize <= Cfg.ThinDeckThreshold && num < 2.5f)
		{
			num += ThinDeckPenalty;
			deckSizeAdjust += ThinDeckPenalty;
			antiReasons.Add($"{ThinDeckPenalty:F1} be selective (thin deck)");
		}
		else if (deckSize >= Cfg.BloatedDeckThreshold && num < 3.5f)
		{
			num += BloatedDeckPenalty;
			deckSizeAdjust += BloatedDeckPenalty;
			antiReasons.Add($"{BloatedDeckPenalty:F1} only take great cards (bloated deck)");
		}

		// === UPGRADE BONUS ===
		float upgradeAdjust = 0f;
		if (card.Upgraded)
		{
			num += UpgradeBonus;
			upgradeAdjust += UpgradeBonus;
			synReasons.Add($"+{UpgradeBonus:F1} upgraded");
		}

		num = Math.Max(0f, Math.Min(6.0f, num));
		return new ScoredCard
		{
			Id = card.Id,
			Name = (card.Name ?? card.Id),
			Type = card.Type,
			Cost = card.Cost,
			BaseTier = tierGrade,
			FinalScore = num,
			FinalGrade = TierEngine.ScoreToGrade(num),
			SynergyReasons = synReasons,
			AntiSynergyReasons = antiReasons,
			Notes = (tierEntry?.Notes ?? ""),
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
		float num = baseScore;
		float synergyDelta = 0f;
		float floorAdjust = 0f;
		List<string> synReasons = new List<string>();
		List<string> antiReasons = new List<string>();
		List<string> relicSynTags = tierEntry?.Synergies ?? new List<string>();
		List<string> relicAntiTags = tierEntry?.AntiSynergies ?? new List<string>();
		// Graduated synergy with diminishing returns + saturation (same as cards) (#3)
		int matchCount = 0;
		foreach (ArchetypeMatch arch in deckAnalysis.DetectedArchetypes)
		{
			if (matchCount >= Cfg.SynergyDiminishing.Length) break;
			foreach (string tag in relicSynTags)
			{
				if (ArchetypeUtils.MatchesAnyTag(arch.Archetype, tag) || arch.Archetype.Id == tag)
				{
					float baseBoost = 0.3f + arch.Strength * 0.5f;
					float dimMult = matchCount < Cfg.SynergyDiminishing.Length ? Cfg.SynergyDiminishing[matchCount] : 0f;
					// Apply saturation multiplier to relic synergy scoring too (#3)
					float satMult = GetSaturationMult(deckAnalysis, tag);
					float boost = baseBoost * dimMult * satMult;
					num += boost;
					synergyDelta += boost;
					string satNote = satMult < 1f ? $" (sat {satMult:P0})" : "";
					synReasons.Add($"+{boost:F2} {arch.Archetype.DisplayName}{satNote}");
					matchCount++;
					break;
				}
			}
		}
		// Anti-synergy — checks both CoreTags AND SupportTags
		foreach (ArchetypeMatch arch in deckAnalysis.DetectedArchetypes)
		{
			foreach (string tag in relicAntiTags)
			{
				if (ArchetypeUtils.MatchesAnyTag(arch.Archetype, tag) || arch.Archetype.Id == tag)
				{
					num -= AntiSynergyPenalty;
					synergyDelta -= AntiSynergyPenalty;
					antiReasons.Add($"-{AntiSynergyPenalty:F1} conflicts with {arch.Archetype.DisplayName}");
					break;
				}
			}
		}
		// Cap anti-synergy penalty to prevent excessive stacking
		if (synergyDelta < AntiSynergyCap)
		{
			float excess = AntiSynergyCap - synergyDelta;
			num += excess;
			synergyDelta = AntiSynergyCap;
		}
		// Floor-aware: late-game scaling bonus for relics too
		if (floorNumber >= 19 && relicSynTags.Any((string s) => ScalingTags.Contains(s)))
		{
			num += Cfg.LateFloorScalingBonus;
			floorAdjust += Cfg.LateFloorScalingBonus;
			synReasons.Add($"+{Cfg.LateFloorScalingBonus:F1} scaling (late floors)");
		}
		num = Math.Max(0f, Math.Min(6.0f, num));
		return new ScoredRelic
		{
			Id = relic.Id,
			Name = (relic.Name ?? relic.Id),
			Rarity = relic.Rarity,
			BaseTier = tierGrade,
			FinalScore = num,
			FinalGrade = TierEngine.ScoreToGrade(num),
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
