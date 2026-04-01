using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace QuestceSpire.Core;

// 커뮤니티 카드 정보 모델
public class CommunityCardInfo
{
    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("cost")]
    public object Cost { get; set; } = 0;

    [JsonProperty("type")]
    public string Type { get; set; } = "";

    [JsonProperty("rarity")]
    public string Rarity { get; set; } = "";

    [JsonProperty("tier")]
    public string Tier { get; set; } = "";

    [JsonProperty("tip")]
    public string Tip { get; set; } = "";
}

// 커뮤니티 아키타입 모델
public class CommunityArchetype
{
    [JsonProperty("id")]
    public string Id { get; set; } = "";

    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("desc")]
    public string Desc { get; set; } = "";

    [JsonProperty("must")]
    public List<string> Must { get; set; } = new();

    [JsonProperty("rec")]
    public List<string> Rec { get; set; } = new();

    [JsonProperty("sourceType")]
    public string? SourceType { get; set; }

    [JsonProperty("sourceName")]
    public string? SourceName { get; set; }

    [JsonProperty("sourceUrl")]
    public string? SourceUrl { get; set; }

    [JsonProperty("sub")]
    public string? Sub { get; set; }
}

// 커뮤니티 콤보 모델
public class CommunityCombo
{
    [JsonProperty("cards")]
    public List<string> Cards { get; set; } = new();

    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("why")]
    public string Why { get; set; } = "";
}

/// <summary>
/// 빌드 완성도 상태 (아키타입 기준 덱 진행도)
/// </summary>
public class BuildCompletionState
{
    public int Percentage { get; set; }
    public int Level { get; set; } // 1-5
    public float Score { get; set; }
    public float MaxScore { get; set; }
    public List<string> MissingMust { get; set; } = new();
    public List<string> MissingRec { get; set; } = new();
    public string ArchetypeName { get; set; } = "";
}

/// <summary>
/// 커뮤니티 가이드 데이터 (한국 STS2 커뮤니티 "카드 뭐 뽑지?" 기반)
/// 한국어 카드명으로 매칭, 기존 TierEngine과 공존
/// </summary>
public class CommunityData
{
    // 캐릭터별 데이터
    private readonly Dictionary<string, List<CommunityCardInfo>> _cards = new();
    private readonly Dictionary<string, List<CommunityArchetype>> _archetypes = new();
    private readonly Dictionary<string, List<CommunityCombo>> _combos = new();
    private readonly Dictionary<string, List<string>> _universalCards = new();

    // 무색 카드
    private readonly List<CommunityCardInfo> _colorlessCards = new();

    // 스코어링 데이터
    private readonly Dictionary<string, float> _tierBaseScores = new();
    private readonly Dictionary<string, float> _buildPowerBonus = new();
    private readonly Dictionary<string, float> _buildTierBonus = new();
    private readonly HashSet<string> _duplicateExceptions = new();
    private readonly HashSet<string> _highPowerSingles = new();

    // 카드 팁: 한국어 이름 → 팁
    private readonly Dictionary<string, string> _cardTips = new();

    // 카드 티어: "캐릭터:카드명" → 티어
    private readonly Dictionary<string, string> _cardTiers = new();

    // 해금 조건: 한국어 카드명 → 조건
    private readonly Dictionary<string, string> _unlockConditions = new();

    // 빌드 유물: 아키타입 ID → 유물 설명 목록
    private readonly Dictionary<string, List<string>> _buildRelics = new();

    // 시작 덱: 캐릭터 → 카드명 목록
    private readonly Dictionary<string, List<string>> _startDecks = new();

    public bool IsLoaded { get; private set; }

    /// <summary>
    /// 데이터 파일에서 커뮤니티 데이터 로드
    /// </summary>
    public static CommunityData Load(string dataPath)
    {
        var instance = new CommunityData();
        var filePath = Path.Combine(dataPath, "community_guide.json");

        if (!File.Exists(filePath))
        {
            Plugin.Log("CommunityData: community_guide.json not found, community features disabled.");
            return instance;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var root = JObject.Parse(json);

            // 캐릭터 데이터 로드
            var characters = root["characters"] as JObject;
            if (characters != null)
            {
                foreach (var prop in characters.Properties())
                {
                    var charName = prop.Name;
                    var charData = prop.Value as JObject;
                    if (charData == null) continue;

                    // 카드 로드
                    var cards = charData["cards"]?.ToObject<List<CommunityCardInfo>>() ?? new();
                    instance._cards[charName] = cards;

                    // 팁과 티어 인덱스 구축
                    foreach (var card in cards)
                    {
                        if (!string.IsNullOrEmpty(card.Tip))
                            instance._cardTips[card.Name] = card.Tip;
                        instance._cardTiers[$"{charName}:{card.Name}"] = card.Tier;
                    }

                    // 아키타입 로드
                    instance._archetypes[charName] =
                        charData["archetypes"]?.ToObject<List<CommunityArchetype>>() ?? new();

                    // 콤보 로드
                    instance._combos[charName] =
                        charData["combos"]?.ToObject<List<CommunityCombo>>() ?? new();

                    // 유니버설 카드 로드
                    instance._universalCards[charName] =
                        charData["universal"]?.ToObject<List<string>>() ?? new();
                }
            }

            // 무색 카드 로드
            var colorless = root["colorless"]?["cards"]?.ToObject<List<CommunityCardInfo>>();
            if (colorless != null)
            {
                instance._colorlessCards.AddRange(colorless);
                foreach (var card in colorless)
                {
                    if (!string.IsNullOrEmpty(card.Tip))
                        instance._cardTips[card.Name] = card.Tip;
                    instance._cardTiers[$"colorless:{card.Name}"] = card.Tier;
                }
            }

            // 스코어링 데이터 로드
            var scoring = root["scoring"] as JObject;
            if (scoring != null)
            {
                var tierBase = scoring["tierBaseScores"]?.ToObject<Dictionary<string, float>>();
                if (tierBase != null)
                    foreach (var kv in tierBase)
                        instance._tierBaseScores[kv.Key] = kv.Value;

                var powerBonus = scoring["buildPowerBonus"]?.ToObject<Dictionary<string, float>>();
                if (powerBonus != null)
                    foreach (var kv in powerBonus)
                        instance._buildPowerBonus[kv.Key] = kv.Value;

                var tierBonus = scoring["buildTierBonus"]?.ToObject<Dictionary<string, float>>();
                if (tierBonus != null)
                    foreach (var kv in tierBonus)
                        instance._buildTierBonus[kv.Key] = kv.Value;

                var dupExceptions = scoring["duplicateExceptions"]?.ToObject<List<string>>();
                if (dupExceptions != null)
                    foreach (var s in dupExceptions)
                        instance._duplicateExceptions.Add(s);

                var highPower = scoring["highPowerSingles"]?.ToObject<List<string>>();
                if (highPower != null)
                    foreach (var s in highPower)
                        instance._highPowerSingles.Add(s);
            }

            // 빌드 유물 로드
            var buildRelics = root["buildRelics"]?.ToObject<Dictionary<string, List<string>>>();
            if (buildRelics != null)
                foreach (var kv in buildRelics)
                    instance._buildRelics[kv.Key] = kv.Value;

            // 해금 조건 로드
            var unlocks = root["unlockConditions"]?.ToObject<Dictionary<string, string>>();
            if (unlocks != null)
                foreach (var kv in unlocks)
                    instance._unlockConditions[kv.Key] = kv.Value;

            // 시작 덱 로드
            var startDecks = root["startDecks"]?.ToObject<Dictionary<string, List<string>>>();
            if (startDecks != null)
                foreach (var kv in startDecks)
                    instance._startDecks[kv.Key] = kv.Value;

            int totalCards = instance._cards.Values.Sum(c => c.Count) + instance._colorlessCards.Count;
            int totalArchetypes = instance._archetypes.Values.Sum(a => a.Count);
            int totalCombos = instance._combos.Values.Sum(c => c.Count);
            Plugin.Log($"CommunityData loaded: {totalCards} cards, {totalArchetypes} archetypes, {totalCombos} combos, {instance._unlockConditions.Count} unlock conditions.");
            instance.IsLoaded = true;
        }
        catch (Exception ex)
        {
            Plugin.Log($"CommunityData: failed to load ({ex.Message}), community features disabled.");
        }

        return instance;
    }

    /// <summary>
    /// 한국어 카드명으로 팁 조회
    /// </summary>
    public string? GetTip(string koreanCardName)
    {
        return _cardTips.TryGetValue(koreanCardName, out var tip) ? tip : null;
    }

    /// <summary>
    /// 해당 캐릭터에서 "무조건 픽" 유니버설 카드인지 확인
    /// </summary>
    public bool IsUniversal(string character, string koreanCardName)
    {
        if (string.IsNullOrEmpty(koreanCardName)) return false;
        return _universalCards.TryGetValue(character, out var list) && list.Contains(koreanCardName);
    }

    /// <summary>
    /// 빌드 파워 보너스 가중치 조회 (없으면 0)
    /// </summary>
    public float GetPowerBonus(string koreanCardName)
    {
        return _buildPowerBonus.TryGetValue(koreanCardName, out var bonus) ? bonus : 0f;
    }

    /// <summary>
    /// 중복 예외 카드인지 확인 (폼멜타격 등 2장 허용)
    /// </summary>
    public bool IsDuplicateException(string koreanCardName)
    {
        return _duplicateExceptions.Contains(koreanCardName);
    }

    /// <summary>
    /// 고파워 단일 카드인지 확인 (지옥검무 등 1장만)
    /// </summary>
    public bool IsHighPowerSingle(string koreanCardName)
    {
        return _highPowerSingles.Contains(koreanCardName);
    }

    /// <summary>
    /// 덱 카드 목록에서 가장 잘 맞는 아키타입 감지
    /// detectBuildScores 로직 포팅: must(72) + rec(30) + 티어보너스 + 파워보너스
    /// </summary>
    public CommunityArchetype? DetectArchetype(string character, IEnumerable<string> deckKoreanNames)
    {
        if (deckKoreanNames == null || string.IsNullOrEmpty(character))
            return null;
        if (!_archetypes.TryGetValue(character, out var archetypes) || archetypes.Count == 0)
            return null;

        var deckSet = new HashSet<string>(deckKoreanNames);
        CommunityArchetype? best = null;
        float bestScore = 0f;

        foreach (var arch in archetypes)
        {
            float score = 0f;

            // must 카드 가중치 72
            foreach (var must in arch.Must)
            {
                if (deckSet.Contains(must))
                {
                    score += 72f;
                    score += GetPowerBonus(must) * 0.45f;
                    score += GetTierBonusForCard(character, must);
                }
            }

            // rec 카드 가중치 30
            foreach (var rec in arch.Rec)
            {
                if (deckSet.Contains(rec))
                {
                    score += 30f;
                    score += GetPowerBonus(rec) * 0.3f;
                    score += GetTierBonusForCard(character, rec) * 0.5f;
                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = arch;
            }
        }

        return best;
    }

    /// <summary>
    /// 특정 아키타입의 빌드 완성도 계산
    /// must 기본 100, rec 기본 56, 파워보너스×0.45(상한38), 티어보너스: S=24, A=16, B=10, C=4, D=0
    /// </summary>
    public BuildCompletionState GetBuildCompletion(string character, string archetypeId,
        IEnumerable<string> deckKoreanNames)
    {
        var state = new BuildCompletionState();

        if (deckKoreanNames == null || string.IsNullOrEmpty(character) || string.IsNullOrEmpty(archetypeId))
            return state;
        if (!_archetypes.TryGetValue(character, out var archetypes))
            return state;

        var arch = archetypes.FirstOrDefault(a => a.Id == archetypeId);
        if (arch == null)
            return state;

        state.ArchetypeName = arch.Name;
        var deckSet = new HashSet<string>(deckKoreanNames);

        float totalScore = 0f;
        float maxScore = 0f;

        // must 카드 계산
        foreach (var must in arch.Must)
        {
            float cardWeight = 100f;
            float powerAdj = Math.Min(GetPowerBonus(must) * 0.45f, 38f);
            float tierAdj = GetCompletionTierBonus(character, must);
            float cardMax = cardWeight + powerAdj + tierAdj;
            maxScore += cardMax;

            if (deckSet.Contains(must))
            {
                totalScore += cardMax;
            }
            else
            {
                state.MissingMust.Add(must);
            }
        }

        // rec 카드 계산
        foreach (var rec in arch.Rec)
        {
            float cardWeight = 56f;
            float powerAdj = Math.Min(GetPowerBonus(rec) * 0.45f, 38f);
            float tierAdj = GetCompletionTierBonus(character, rec);
            float cardMax = cardWeight + powerAdj + tierAdj;
            maxScore += cardMax;

            if (deckSet.Contains(rec))
            {
                totalScore += cardMax;
            }
            else
            {
                state.MissingRec.Add(rec);
            }
        }

        state.Score = totalScore;
        state.MaxScore = maxScore;
        state.Percentage = maxScore > 0 ? (int)Math.Round(totalScore / maxScore * 100) : 0;

        // 레벨 결정: <40%=1, 40-60%=2, 60-85%=3, 85-100%=4, 100%=5
        state.Level = state.Percentage switch
        {
            >= 100 => 5,
            >= 85 => 4,
            >= 60 => 3,
            >= 40 => 2,
            _ => 1
        };

        return state;
    }

    /// <summary>
    /// 특정 카드와 관련된 콤보 조회 (full = 모든 카드 보유, partial = 일부)
    /// </summary>
    public (List<CommunityCombo> full, List<CommunityCombo> partial) GetMatchingCombos(
        string character, string koreanCardName, IEnumerable<string> deckKoreanNames)
    {
        var fullMatches = new List<CommunityCombo>();
        var partialMatches = new List<CommunityCombo>();

        if (string.IsNullOrEmpty(koreanCardName) || deckKoreanNames == null)
            return (fullMatches, partialMatches);
        if (!_combos.TryGetValue(character, out var combos))
            return (fullMatches, partialMatches);

        var deckSet = new HashSet<string>(deckKoreanNames);

        foreach (var combo in combos)
        {
            if (!combo.Cards.Contains(koreanCardName))
                continue;

            bool allPresent = combo.Cards.All(c => deckSet.Contains(c));
            if (allPresent)
                fullMatches.Add(combo);
            else
                partialMatches.Add(combo);
        }

        return (fullMatches, partialMatches);
    }

    /// <summary>
    /// 특정 카드를 참조하는 아키타입 목록 (must 또는 rec에 포함)
    /// </summary>
    public List<CommunityArchetype> GetCardArchetypeRefs(string character, string koreanCardName)
    {
        if (!_archetypes.TryGetValue(character, out var archetypes))
            return new List<CommunityArchetype>();

        return archetypes
            .Where(a => a.Must.Contains(koreanCardName) || a.Rec.Contains(koreanCardName))
            .ToList();
    }

    /// <summary>
    /// 빌드 관련 유물 추천 조회
    /// </summary>
    public List<string> GetBuildRelics(string archetypeId)
    {
        return _buildRelics.TryGetValue(archetypeId, out var relics) ? relics : new List<string>();
    }

    /// <summary>
    /// 카드 해금 조건 조회
    /// </summary>
    public string? GetUnlockCondition(string koreanCardName)
    {
        return _unlockConditions.TryGetValue(koreanCardName, out var cond) ? cond : null;
    }

    /// <summary>
    /// 커뮤니티 티어 조회 (캐릭터 카드 → 무색 카드 순서로 탐색)
    /// </summary>
    public string? GetCommunityTier(string character, string koreanCardName)
    {
        if (_cardTiers.TryGetValue($"{character}:{koreanCardName}", out var tier))
            return tier;
        if (_cardTiers.TryGetValue($"colorless:{koreanCardName}", out var colorlessTier))
            return colorlessTier;
        return null;
    }

    /// <summary>
    /// 캐릭터별 모든 아키타입 목록
    /// </summary>
    public List<CommunityArchetype> GetArchetypes(string character)
    {
        return _archetypes.TryGetValue(character, out var list) ? list : new List<CommunityArchetype>();
    }

    /// <summary>
    /// 캐릭터별 모든 카드 정보
    /// </summary>
    public List<CommunityCardInfo> GetCards(string character)
    {
        return _cards.TryGetValue(character, out var list) ? list : new List<CommunityCardInfo>();
    }

    /// <summary>
    /// 무색 카드 목록
    /// </summary>
    public List<CommunityCardInfo> GetColorlessCards() => _colorlessCards;

    /// <summary>
    /// 시작 덱 카드명 목록
    /// </summary>
    public List<string> GetStartDeck(string character)
    {
        return _startDecks.TryGetValue(character, out var deck) ? deck : new List<string>();
    }

    /// <summary>
    /// 티어 기반 스코어 기본값 (S=50, A=35 등)
    /// </summary>
    public float GetTierBaseScore(string tier)
    {
        return _tierBaseScores.TryGetValue(tier, out var score) ? score : 0f;
    }

    // 빌드 감지용 티어 보너스 (카드의 커뮤니티 티어에 기반)
    private float GetTierBonusForCard(string character, string koreanCardName)
    {
        var tier = GetCommunityTier(character, koreanCardName);
        if (tier == null) return 0f;

        return tier switch
        {
            "S" => 24f,
            "A" => 16f,
            "B" => 10f,
            "C" => 4f,
            "D" => 0f,
            _ => 0f
        };
    }

    // 빌드 완성도 계산용 티어 보너스 (원본 JS 알고리즘과 동일: S=24, A=16, B=10, C=4, D=0)
    private float GetCompletionTierBonus(string character, string koreanCardName)
    {
        var tier = GetCommunityTier(character, koreanCardName);
        if (tier == null) return 0f;

        return tier switch
        {
            "S" => 24f,
            "A" => 16f,
            "B" => 10f,
            "C" => 4f,
            "D" => 0f,
            _ => 0f
        };
    }
}
