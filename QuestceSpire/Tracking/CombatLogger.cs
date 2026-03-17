using System;
using System.Collections.Generic;

namespace QuestceSpire.Tracking;

/// <summary>
/// Records turn-by-turn combat data from Harmony patches.
/// Aggregates per-turn stats (cards played, damage, block) and persists to combat_turns table.
/// </summary>
public class CombatLogger
{
	private readonly RunDatabase _db;

	// Current combat state
	private string _currentRunId;
	private int _currentFloor;
	private string _currentEnemyId;
	private int _turnNumber;
	private List<string> _cardsPlayedThisTurn;
	private int _damageDealtThisTurn;
	private int _damageTakenThisTurn;
	private int _blockGeneratedThisTurn;
	private int _playerHp;
	private Dictionary<string, int> _enemyHp;
	private bool _inCombat;

	public CombatLogger(RunDatabase db)
	{
		_db = db;
	}

	/// <summary>
	/// Called when combat begins.
	/// </summary>
	public void OnCombatStart(string runId, int floor, string enemyId)
	{
		_currentRunId = runId;
		_currentFloor = floor;
		_currentEnemyId = enemyId;
		_turnNumber = 0;
		_inCombat = true;
		_enemyHp = new Dictionary<string, int>();
		ResetTurnCounters();
	}

	/// <summary>
	/// Called at the start of player's turn.
	/// </summary>
	public void OnTurnStart(int playerHp, Dictionary<string, int> enemyHp)
	{
		if (!_inCombat) return;

		// Save previous turn if not the first
		if (_turnNumber > 0)
			FlushTurn();

		_turnNumber++;
		_playerHp = playerHp;
		if (enemyHp != null)
			_enemyHp = new Dictionary<string, int>(enemyHp);
		ResetTurnCounters();
	}

	/// <summary>
	/// Called when a card is played.
	/// </summary>
	public void OnCardPlayed(string cardId)
	{
		if (!_inCombat) return;
		_cardsPlayedThisTurn ??= new List<string>();
		_cardsPlayedThisTurn.Add(cardId);
	}

	/// <summary>
	/// Called when player deals damage.
	/// </summary>
	public void OnDamageDealt(int amount)
	{
		if (!_inCombat) return;
		_damageDealtThisTurn += amount;
	}

	/// <summary>
	/// Called when player takes damage.
	/// </summary>
	public void OnDamageTaken(int amount)
	{
		if (!_inCombat) return;
		_damageTakenThisTurn += amount;
	}

	/// <summary>
	/// Called when player generates block.
	/// </summary>
	public void OnBlockGenerated(int amount)
	{
		if (!_inCombat) return;
		_blockGeneratedThisTurn += amount;
	}

	/// <summary>
	/// Called when combat ends.
	/// </summary>
	public void OnCombatEnd()
	{
		if (!_inCombat) return;

		// Flush final turn
		if (_turnNumber > 0)
			FlushTurn();

		_inCombat = false;
		Plugin.Log($"CombatLogger: logged {_turnNumber} turns for floor {_currentFloor} vs {_currentEnemyId}");
	}

	private void FlushTurn()
	{
		try
		{
			_db.SaveCombatTurn(new CombatTurnRecord
			{
				RunId = _currentRunId,
				Floor = _currentFloor,
				EnemyId = _currentEnemyId,
				TurnNumber = _turnNumber,
				CardsPlayed = _cardsPlayedThisTurn ?? new List<string>(),
				DamageDealt = _damageDealtThisTurn,
				DamageTaken = _damageTakenThisTurn,
				BlockGenerated = _blockGeneratedThisTurn,
				PlayerHp = _playerHp,
				EnemyHp = _enemyHp
			});
		}
		catch (Exception ex)
		{
			Plugin.Log($"CombatLogger: failed to save turn {_turnNumber} — {ex.Message}");
		}
	}

	private void ResetTurnCounters()
	{
		_cardsPlayedThisTurn = new List<string>();
		_damageDealtThisTurn = 0;
		_damageTakenThisTurn = 0;
		_blockGeneratedThisTurn = 0;
	}
}
