using System.Collections.Generic;
using QuestceSpire.GameBridge;

namespace QuestceSpire.Core;

public interface IDeckAnalyzer
{
	DeckAnalysis Analyze(string character, List<CardInfo> deck, TierEngine tierEngine = null, List<RelicInfo> relics = null);
}
