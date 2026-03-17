using System.Collections.Generic;
using QuestceSpire.GameBridge;

namespace QuestceSpire.Core
{
    public interface ICardScorer
    {
        List<ScoredCard> ScoreOfferings(List<CardInfo> offerings, DeckAnalysis deckAnalysis, string character, int actNumber, int floorNumber);
    }

    public interface IRelicScorer
    {
        List<ScoredRelic> ScoreRelicOfferings(List<RelicInfo> offerings, DeckAnalysis deckAnalysis, string character);
    }

    public interface IAdaptiveScorer
    {
        float GetAdaptiveCardScore(string character, string cardId, float staticScore, DeckAnalysis deckAnalysis);
        float GetAdaptiveRelicScore(string character, string relicId, float staticScore);
    }
}
