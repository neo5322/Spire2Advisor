using System;
using System.Collections.Generic;
using Godot;
using QuestceSpire.Core;
using QuestceSpire.GameBridge;

namespace QuestceSpire.UI.Injectors;

/// <summary>
/// Event screen injector — shows event choice recommendations and general event tips.
/// </summary>
public class EventInjector : BaseScreenInjector
{
	public override string ScreenName => "EVENT";

	private DeckAnalysis _deckAnalysis;
	private int _currentHP, _maxHP, _gold, _actNumber, _floor;
	private string _eventId;
	private List<(string icon, string text, Color color)> _advice;

	public EventInjector(OverlaySettings settings) : base(settings) { }

	public void Show(Node gameNode, DeckAnalysis deckAnalysis, int currentHP, int maxHP, int gold, int actNumber, int floor, string eventId)
	{
		_deckAnalysis = deckAnalysis;
		_currentHP = currentHP;
		_maxHP = maxHP;
		_gold = gold;
		_actNumber = actNumber;
		_floor = floor;
		_eventId = eventId;
		_advice = GenerateEventScreenAdvice(deckAnalysis, currentHP, maxHP, gold, actNumber, floor, eventId);

		Inject(gameNode);
		Rebuild();
	}

	protected override void BuildContent()
	{
		if (_advice == null || _advice.Count == 0) return;

		AddSectionHeader("이벤트 조언");
		foreach (var (icon, text, color) in _advice)
		{
			if (icon == "##")
			{
				AddSubSectionHeader(text, color);
				continue;
			}
			AddAdviceTip(icon, text, color);
		}

		// HP bar at bottom
		if (_maxHP > 0)
		{
			Content.AddChild(CreateHpBar(_currentHP, _maxHP), forceReadableName: false, Node.InternalMode.Disabled);
		}
	}

	private List<(string icon, string text, Color color)> GenerateEventScreenAdvice(DeckAnalysis deck, int hp, int maxHP, int gold, int act, int floor, string eventId)
	{
		var advice = new List<(string, string, Color)>();

		// Event-specific advice
		if (Settings.ShowEventAdvice && eventId != null && Plugin.EventAdvisor != null)
		{
			var entry = Plugin.EventAdvisor.GetAdvice(eventId);
			if (entry != null)
			{
				int deckSize = deck?.TotalCards ?? 0;
				var choices = Plugin.EventAdvisor.EvaluateChoices(entry, hp, maxHP, gold, deckSize, act);
				if (choices != null && choices.Count > 0)
				{
					advice.Add(("##", $"이벤트: {GameStateReader.GetLocalizedName("event", entry.EventId) ?? entry.EventName}", SharedResources.ClrAccent));
					foreach (var (label, rating, notes) in choices)
					{
						string icon = rating switch
						{
							"good" => "\u2714",
							"bad" => "\u2716",
							_ => "\u25c6"
						};
						Color color = rating switch
						{
							"good" => SharedResources.ClrPositive,
							"bad" => SharedResources.ClrNegative,
							_ => SharedResources.ClrExpensive
						};
						string text = string.IsNullOrEmpty(notes) ? label : $"{label} — {notes}";
						advice.Add((icon, text, color));
					}
				}
			}
		}

		// Generic event advice
		if (Settings.ShowEventAdvice)
		{
			var eventAdvice = GenerateGenericEventAdvice(deck, hp, maxHP, gold, act, floor);
			if (eventAdvice.Count > 0)
			{
				advice.Add(("##", "일반 팁", SharedResources.ClrAccent));
				advice.AddRange(eventAdvice);
			}
		}

		return advice;
	}

	private List<(string icon, string text, Color color)> GenerateGenericEventAdvice(DeckAnalysis deck, int hp, int maxHP, int gold, int act, int floor)
	{
		var advice = new List<(string, string, Color)>();
		float hpRatio = maxHP > 0 ? (float)hp / maxHP : 1f;
		int deckSize = deck?.TotalCards ?? 0;
		bool isDefined = deck != null && !deck.IsUndefined;

		if (hpRatio < 0.35f)
			advice.Add(("\u26a0", "HP 위험 — HP 소모 선택지 회피", SharedResources.ClrNegative));
		if (gold < 50)
			advice.Add(("\u26a0", "골드 부족 — 골드 소모 선택지 회피", SharedResources.ClrExpensive));
		if (deckSize >= 25)
			advice.Add(("\u2714", "덱이 큼 — 카드 제거가 가치 있음", SharedResources.ClrAqua));
		if (deckSize <= 15 && isDefined)
			advice.Add(("\u2714", "덱이 얇음 — 카드 추가 신중하게", SharedResources.ClrAqua));
		if (!isDefined)
			advice.Add(("\u2714", "덱 방향 불명확 — 카드 보상으로 방향 잡기", SharedResources.ClrCream));

		return advice;
	}
}
