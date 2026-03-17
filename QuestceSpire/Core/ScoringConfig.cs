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
            catch
            {
                Instance = new ScoringConfig();
            }
        }
    }
}
