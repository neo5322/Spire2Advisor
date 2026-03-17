using System.Collections.Generic;

namespace QuestceSpire.Tracking;

public interface IRunTracker
{
	void StartRun(string character, string seed, int ascensionLevel);
	void EndRun(RunOutcome outcome, int finalFloor, int finalAct);
	void RecordDecision(DecisionEventType eventType, List<string> offeredIds, string chosenId, List<string> deckSnapshot, List<string> relicSnapshot, int hp, int maxHp, int gold, int act, int floor);
	void RecordArchetypeSnapshot(int floor, Core.DeckAnalysis analysis);
	void UpdateLastDecisionChoice(string chosenId);
	int GetRelicTenure(string relicId, int currentFloor);
	IReadOnlyList<DecisionEvent> GetCurrentRunEvents();
	string CurrentCharacter { get; }
	string PlayerId { get; }
}
