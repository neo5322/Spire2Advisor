using System;
using System.Collections.Generic;
using QuestceSpire.Tracking;

namespace QuestceSpire.Core;

public class AdaptiveScorer : IAdaptiveScorer
{
	private readonly RunDatabase _db;
	private bool _loggedUnavailable;

	public AdaptiveScorer(RunDatabase db)
	{
		_db = db;
	}

	private ScoringConfig Cfg => ScoringConfig.Instance;

	public float GetAdaptiveCardScore(string character, string cardId, float staticScore, DeckAnalysis deckAnalysis)
	{
		if (_db == null)
		{
			if (!_loggedUnavailable)
			{
				Plugin.Log("AdaptiveScorer: database unavailable, falling back to static scores.");
				_loggedUnavailable = true;
			}
			return staticScore;
		}
		float num = staticScore;
		CommunityCardStats communityCardStats = _db.GetCommunityCardStats(character, cardId);
		if (communityCardStats == null || communityCardStats.SampleSize < Cfg.MinSampleSize)
		{
			return num;
		}
		float num2 = WinRateToScore(communityCardStats.WinRateWhenPicked);
		float contextScore = GetContextScore(communityCardStats.ArchetypeContext, deckAnalysis);
		if (contextScore >= 0f)
		{
			num2 = num2 * 0.4f + contextScore * 0.6f;
		}
		float val = (communityCardStats.PickRate - 0.33f) * 0.9f;
		val = Math.Max(-0.3f, Math.Min(0.3f, val));
		num2 += val;
		float num3 = communityCardStats.WinRateWhenPicked - communityCardStats.WinRateWhenSkipped;
		if (Math.Abs(num3) > 0.03f)
		{
			num2 += num3 * 2f;
		}
		num2 = Math.Max(0f, Math.Min(5f, num2));
		float confidence = GetConfidence(communityCardStats.SampleSize);
		float val2 = num * (1f - confidence) + num2 * confidence;
		return Math.Max(0f, Math.Min(5f, val2));
	}

	public float GetAdaptiveRelicScore(string character, string relicId, float staticScore)
	{
		return GetAdaptiveRelicScore(character, relicId, staticScore, null);
	}

	public float GetAdaptiveRelicScore(string character, string relicId, float staticScore, DeckAnalysis deckAnalysis)
	{
		if (_db == null)
		{
			if (!_loggedUnavailable)
			{
				Plugin.Log("AdaptiveScorer: database unavailable, falling back to static scores.");
				_loggedUnavailable = true;
			}
			return staticScore;
		}
		float num = staticScore;
		CommunityRelicStats communityRelicStats = _db.GetCommunityRelicStats(character, relicId);
		if (communityRelicStats == null || communityRelicStats.SampleSize < Cfg.MinSampleSize)
		{
			return num;
		}
		float num2 = WinRateToScore(communityRelicStats.WinRateWhenPicked);
		float val = (communityRelicStats.PickRate - 0.33f) * 0.9f;
		val = Math.Max(-0.3f, Math.Min(0.3f, val));
		num2 += val;
		float num3 = communityRelicStats.WinRateWhenPicked - communityRelicStats.WinRateWhenSkipped;
		if (Math.Abs(num3) > 0.03f)
		{
			num2 += num3 * 2f;
		}
		num2 = Math.Max(0f, Math.Min(5f, num2));
		float confidence = GetConfidence(communityRelicStats.SampleSize);
		float val2 = num * (1f - confidence) + num2 * confidence;
		return Math.Max(0f, Math.Min(5f, val2));
	}

	private float GetConfidence(int sampleSize)
	{
		if (sampleSize < Cfg.MinSampleSize)
		{
			return 0f;
		}
		if (sampleSize >= Cfg.FullConfidenceSampleSize)
		{
			return 1f;
		}
		return (float)(sampleSize - Cfg.MinSampleSize) / (float)(Cfg.FullConfidenceSampleSize - Cfg.MinSampleSize);
	}

	private float WinRateToScore(float winRate)
	{
		float normalized = (winRate - Cfg.WinRateForF) / (Cfg.WinRateForS - Cfg.WinRateForF);
		normalized = Math.Max(0f, Math.Min(1f, normalized));
		// Sigmoid curve for more natural distribution
		float sigmoid = 1f / (1f + (float)Math.Exp(-6f * (normalized - 0.5f)));
		return sigmoid * 5f;
	}

	private float GetContextScore(Dictionary<string, float> contextStats, DeckAnalysis deckAnalysis)
	{
		if (contextStats == null || contextStats.Count == 0 || deckAnalysis == null)
		{
			return -1f;
		}

		// Fix #1: Find the single best matching archetype FIRST, then extract context score.
		// Previously the inner loop overwrote bestStrength per tag match.
		float bestStrength = 0f;
		ArchetypeMatch bestArch = null;

		foreach (ArchetypeMatch detectedArchetype in deckAnalysis.DetectedArchetypes)
		{
			// Check if any core tag has a matching context key
			bool hasContextMatch = false;
			foreach (string coreTag in detectedArchetype.Archetype.CoreTags)
			{
				foreach (var kvp in contextStats)
				{
					if (kvp.Key.StartsWith(coreTag + "_", StringComparison.OrdinalIgnoreCase))
					{
						hasContextMatch = true;
						break;
					}
				}
				if (hasContextMatch) break;
			}

			if (hasContextMatch && detectedArchetype.Strength > bestStrength)
			{
				bestStrength = detectedArchetype.Strength;
				bestArch = detectedArchetype;
			}
		}

		if (bestArch == null)
		{
			return -1f;
		}

		// Now extract the best context score from the winning archetype
		float bestWinRate = -1f;
		foreach (string coreTag in bestArch.Archetype.CoreTags)
		{
			int bestCount = 0;
			float candidateWinRate = -1f;
			foreach (var kvp in contextStats)
			{
				if (!kvp.Key.StartsWith(coreTag + "_", StringComparison.OrdinalIgnoreCase))
					continue;

				// Validate context key format (e.g., "poison_5+")
				string suffix = kvp.Key.Substring(coreTag.Length + 1).TrimEnd('+');
				if (!int.TryParse(suffix, out int count))
				{
					Plugin.Log($"AdaptiveScorer: malformed context key '{kvp.Key}' — expected format '<tag>_<number>+'");
					continue;
				}

				if (count > bestCount)
				{
					bestCount = count;
					candidateWinRate = kvp.Value;
				}
			}
			if (candidateWinRate > bestWinRate)
			{
				bestWinRate = candidateWinRate;
			}
		}

		return bestWinRate >= 0f ? WinRateToScore(bestWinRate) : -1f;
	}
}
