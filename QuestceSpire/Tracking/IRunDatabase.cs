using System.Collections.Generic;

namespace QuestceSpire.Tracking;

public interface IRunDatabase
{
	void InitializeDatabase();
	void SaveRun(RunLog run, List<DecisionEvent> decisions);
	List<(RunLog Run, List<DecisionEvent> Decisions)> GetUnsynced();
	void MarkSynced(string runId);
	(int wins, int total) GetCharacterWinRate(string character);
	int GetTotalRunCount();
	(float localWinRate, int localRuns, float communityWinRate, int communitySamples) GetStatsComparison(string character);
	CommunityCardStats GetCommunityCardStats(string character, string cardId);
	CommunityRelicStats GetCommunityRelicStats(string character, string relicId);
}
