using System.Collections.Generic;
using Newtonsoft.Json;

namespace QuestceSpire.Core
{
    public class ScoringConfig
    {
        public static ScoringConfig Instance { get; private set; } = new();

        // Synergy
        public float[] SynergyDiminishing { get; set; } = { 1.0f, 0.6f, 0.3f };
        public int SaturationSoftCap { get; set; } = 4;
        public int SaturationHardCap { get; set; } = 7;

        // Floor bonuses
        public float EarlyFloorDamageBonus { get; set; } = 0.3f;
        public float MidFloorBlockBonus { get; set; } = 0.2f;
        public float LateFloorScalingBonus { get; set; } = 0.4f;
        public float MissingPieceBonus { get; set; } = 0.5f;

        // Deck thresholds
        public int ThinDeckThreshold { get; set; } = 18;
        public int BloatedDeckThreshold { get; set; } = 25;

        // Job gap
        public float JobGapMaxBonus { get; set; } = 0.6f;

        // Adaptive scoring
        public int MinSampleSize { get; set; } = 5;
        public int FullConfidenceSampleSize { get; set; } = 50;
        public float WinRateForS { get; set; } = 0.58f;
        public float WinRateForF { get; set; } = 0.35f;

        // Event thresholds
        public float LowHpRatio { get; set; } = 0.35f;
        public float HighHpRatio { get; set; } = 0.70f;
        public int LowGold { get; set; } = 50;
        public int HighGold { get; set; } = 200;
        public int LargeDeckSize { get; set; } = 25;
        public int SmallDeckSize { get; set; } = 15;

        // Energy curve thresholds
        public float HighAvgCostThreshold { get; set; } = 2.2f;
        public float MedAvgCostThreshold { get; set; } = 1.8f;
        public float LowAvgCostThreshold { get; set; } = 1.5f;

        // Energy curve adjustments
        public float ExpensiveCardPenalty { get; set; } = -0.3f;
        public float VeryExpensiveCardPenalty { get; set; } = -0.5f;
        public float CheapCardBonus { get; set; } = 0.15f;

        // Card type balance
        public float PowerGapBonus { get; set; } = 0.2f;
        public float PowerGlutPenalty { get; set; } = -0.2f;
        public float AoEGapBonus { get; set; } = 0.3f;
        public int PowerGlutThreshold { get; set; } = 4;

        // Deck size adjustments
        public float ThinDeckPenalty { get; set; } = -0.2f;
        public float BloatedDeckPenalty { get; set; } = -0.4f;

        // Synergy
        public float AntiSynergyPenalty { get; set; } = 0.6f;
        public float AntiSynergyCap { get; set; } = -1.2f;
        public float SaturationSoftMult { get; set; } = 0.7f;
        public float SaturationHardMult { get; set; } = 0.4f;
        public float CoPickBonusCap { get; set; } = 0.4f;

        // Upgrade
        public float UpgradeBonus { get; set; } = 0.4f;

        // Score clamp
        public float MaxScore { get; set; } = 6.0f;
        public float MinScore { get; set; } = 0f;

        // Graduated synergy base
        public float SynergyBaseMin { get; set; } = 0.3f;
        public float SynergyBaseScale { get; set; } = 0.5f;

        // Floor scoring thresholds
        public int EarlyFloorMax { get; set; } = 6;
        public int MidFloorMax { get; set; } = 18;

        // Removal thresholds
        public float RarelyPlayedThreshold { get; set; } = 0.5f;
        public float LowEffectivenessThreshold { get; set; } = 0.3f;

        // DeckAnalyzer
        public float DensityTargetDeckSize { get; set; } = 20f;
        public float DensityMinFactor { get; set; } = 0.7f;
        public float DensityMaxFactor { get; set; } = 1.3f;

        // Boss readiness
        public float BossBaseReadiness { get; set; } = 50f;

        // PotionAdvisor
        public int ActLengthFloors { get; set; } = 17;
        public int NearBossFloorThreshold { get; set; } = 15;

        // Adaptive scoring
        public float AdaptiveBlendContext { get; set; } = 0.6f;
        public float AdaptiveBlendWinRate { get; set; } = 0.4f;
        public float AdaptivePickRateMaxBonus { get; set; } = 0.3f;

        // Community data bonuses
        public float CommunityComboFullBonus { get; set; } = 0.5f;
        public float CommunityComboPartialBonus { get; set; } = 0.2f;
        public float CommunityBuildMustBonus { get; set; } = 0.6f;
        public float CommunityBuildRecBonus { get; set; } = 0.3f;
        public float CommunityOffBuildPenalty { get; set; } = -0.2f;
        public float CommunityAnchorScale { get; set; } = 0.003f;
        public float CommunityUniversalBonus { get; set; } = 0.3f;
        public float CommunityDuplicateOverride { get; set; } = 0.3f;
        public float CommunityBonusCap { get; set; } = 0.8f;

        // Per-character deck size thresholds (optional override)
        public Dictionary<string, int> ThinDeckByCharacter { get; set; } = new();
        public Dictionary<string, int> BloatedDeckByCharacter { get; set; } = new();

        public int GetThinDeckThreshold(string character)
        {
            return ThinDeckByCharacter.TryGetValue(character, out int val) ? val : ThinDeckThreshold;
        }

        public int GetBloatedDeckThreshold(string character)
        {
            return BloatedDeckByCharacter.TryGetValue(character, out int val) ? val : BloatedDeckThreshold;
        }

        public static void Load(string jsonPath)
        {
            if (!System.IO.File.Exists(jsonPath))
            {
                Instance = new ScoringConfig();
                return;
            }
            try
            {
                string json = System.IO.File.ReadAllText(jsonPath);
                Instance = JsonConvert.DeserializeObject<ScoringConfig>(json) ?? new ScoringConfig();
            }
            catch (Exception ex)
            {
                Plugin.Log($"ScoringConfig: failed to load: {ex.Message}");
                Instance = new ScoringConfig();
            }
        }
    }
}
