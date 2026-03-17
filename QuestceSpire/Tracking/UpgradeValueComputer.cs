using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

namespace QuestceSpire.Tracking;

/// <summary>
/// Analyzes upgrade value by comparing deck snapshots across decisions within the same run.
/// Detects when a card gets upgraded and tracks the run's outcome.
/// </summary>
public class UpgradeValueComputer
{
	private readonly RunDatabase _db;
	private const int MinSamples = 3;

	public UpgradeValueComputer(RunDatabase db)
	{
		_db = db;
	}

	public void Compute()
	{
		var connStr = _db.ConnectionString;
		if (connStr == null)
		{
			Plugin.Log("UpgradeValue: database not initialized — skipping.");
			return;
		}

		Plugin.Log("UpgradeValue: computing upgrade value statistics...");

		// Track: card_id → character → {upgraded_wins, upgraded_total, not_upgraded_wins, not_upgraded_total}
		var data = new Dictionary<(string cardId, string character), (int upgWins, int upgTotal, int noUpgWins, int noUpgTotal)>();

		using var conn = new SqliteConnection(connStr);
		conn.Open();

		// Get all runs with their decisions ordered by floor
		using var cmd = conn.CreateCommand();
		cmd.CommandText = @"
			SELECT d.deck_snapshot, r.character, r.outcome, r.run_id, d.floor
			FROM decisions d
			JOIN runs r ON d.run_id = r.run_id
			WHERE r.outcome IS NOT NULL
			ORDER BY r.run_id, d.floor";

		using var reader = cmd.ExecuteReader();

		string currentRunId = null;
		List<string> prevDeck = null;
		string currentChar = null;
		bool currentIsWin = false;
		var upgradedCards = new HashSet<string>();

		while (reader.Read())
		{
			string deckJson = reader.GetString(0);
			string character = reader.GetString(1);
			string outcome = reader.GetString(2);
			string runId = reader.GetString(3);

			if (runId != currentRunId)
			{
				// Process previous run's upgrade data
				if (currentRunId != null && prevDeck != null)
					RecordUpgrades(data, upgradedCards, prevDeck, currentChar, currentIsWin);

				currentRunId = runId;
				currentChar = character;
				currentIsWin = outcome == "Win";
				prevDeck = null;
				upgradedCards.Clear();
			}

			List<string> deck;
			try { deck = JsonConvert.DeserializeObject<List<string>>(deckJson); }
			catch { continue; }
			if (deck == null) continue;

			// Detect upgrades: cards that appear with "+" suffix that didn't have it before
			if (prevDeck != null)
			{
				var prevSet = new HashSet<string>(prevDeck);
				foreach (var card in deck)
				{
					if (card.EndsWith("+") && !prevSet.Contains(card))
					{
						string baseCard = card.TrimEnd('+');
						if (prevSet.Contains(baseCard))
							upgradedCards.Add(baseCard);
					}
				}
			}

			prevDeck = deck;
		}

		// Process last run
		if (currentRunId != null && prevDeck != null)
			RecordUpgrades(data, upgradedCards, prevDeck, currentChar, currentIsWin);

		// Convert to UpgradeValue list
		var values = new List<UpgradeValue>();
		foreach (var kvp in data)
		{
			int totalUpg = kvp.Value.upgTotal;
			int totalNoUpg = kvp.Value.noUpgTotal;
			if (totalUpg < MinSamples) continue;

			float upgWinRate = totalUpg > 0 ? (float)kvp.Value.upgWins / totalUpg : 0f;
			float noUpgWinRate = totalNoUpg > 0 ? (float)kvp.Value.noUpgWins / totalNoUpg : 0f;
			float totalRuns = totalUpg + totalNoUpg;

			values.Add(new UpgradeValue
			{
				CardId = kvp.Key.cardId,
				Character = kvp.Key.character,
				UpgradeWinDelta = upgWinRate - noUpgWinRate,
				UpgradeFrequency = totalRuns > 0 ? (float)totalUpg / totalRuns : 0f,
				SampleSize = totalUpg
			});
		}

		_db.SaveUpgradeValues(values);
		Plugin.Log($"UpgradeValue: computed upgrade values for {values.Count} cards.");
	}

	private static void RecordUpgrades(
		Dictionary<(string, string), (int, int, int, int)> data,
		HashSet<string> upgraded, List<string> finalDeck, string character, bool isWin)
	{
		// All unique base cards in the deck
		var baseCards = new HashSet<string>();
		foreach (var card in finalDeck)
		{
			baseCards.Add(card.TrimEnd('+'));
		}

		foreach (var baseCard in baseCards)
		{
			var key = (baseCard, character);
			var prev = data.TryGetValue(key, out var v) ? v : (0, 0, 0, 0);

			if (upgraded.Contains(baseCard))
				data[key] = (prev.Item1 + (isWin ? 1 : 0), prev.Item2 + 1, prev.Item3, prev.Item4);
			else
				data[key] = (prev.Item1, prev.Item2, prev.Item3 + (isWin ? 1 : 0), prev.Item4 + 1);
		}
	}
}
