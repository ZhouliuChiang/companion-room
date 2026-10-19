using System.Collections;
using System.Collections.Generic;
using CompanionRoom.Tarot;
using UnityEngine;

namespace CompanionRoom
{
    /// <summary>
    /// 桌上的塔羅: 凡安 offers a reading (one card, or past · present · future), asks whether you have
    /// your question in mind (nothing is typed — answer 想好了，請發牌 or 退出), shuffles and deals, you
    /// turn each card over, and she reads it. Every finished reading is kept in the record journal.
    /// </summary>
    public class TarotDeckItem : RoomItem
    {
        static readonly string[] SpreadChoices = { "單張指引", "過去・現在・未來", "下次吧" };
        static readonly string[] ReadyChoices = { "想好了，請發牌", "退出" };
        static readonly string[] AgainChoices = { "再占一次", "先這樣" };
        static readonly string[] Ordinals = { "第一張", "第二張", "第三張", "第四張", "第五張" };

        readonly System.Random random = new();

        /// <summary>The last reading's cards (for the self test).</summary>
        public List<TarotDraw> LastDraws { get; private set; }

        protected override void Awake()
        {
            if (GetComponent<TarotDeckVisual>() == null)
                gameObject.AddComponent<TarotDeckVisual>();
            base.Awake();
        }

        /// <summary>
        /// The deck waits on the table while 凡安 asks, RPG-style, whether the question is in mind:
        /// <paramref name="answered"/> gets true for 想好了，請發牌 and false for 退出.
        /// </summary>
        public static IEnumerator AskReady(CompanionRoomApp app, System.Action<bool> answered)
        {
            app.UI.Tarot.ShowDeck();
            int pick = -1;
            yield return app.Dialogue.Ask(app.Lines.Pick("tarot_question"), ReadyChoices, i => pick = i);
            answered?.Invoke(pick == 0);
        }

        public override IEnumerator Interact(CompanionRoomApp app)
        {
            var dialogue = app.Dialogue;
            var lines = app.Lines;
            var stage = app.UI.Tarot;
            bool first = true;
            while (true)
            {
                int pick = -1;
                yield return dialogue.Ask(lines.Pick(first ? "tarot_intro" : "tarot_again"), SpreadChoices, i => pick = i);
                if (pick < 0 || pick >= TarotSpread.All.Length)
                {
                    stage.Hide();
                    yield return dialogue.Say(lines.Pick("tarot_cancel"));
                    yield break;
                }
                var spread = TarotSpread.All[pick];

                bool ready = false;
                yield return AskReady(app, answer => ready = answer);
                if (!ready)
                {
                    stage.Hide();
                    yield return dialogue.Say(lines.Pick("tarot_cancel"));
                    yield break;
                }

                dialogue.Show(lines.Pick("tarot_shuffle")[0]);
                yield return stage.ShuffleAndDeal(spread);

                var draws = app.Deck.Deal(spread, random);
                LastDraws = draws;
                for (int i = 0; i < draws.Count; i++)
                {
                    var draw = draws[i];
                    var card = app.Deck.Find(draw.cardId);
                    dialogue.Show(lines.Format(i == 0 ? "tarot_click_first" : "tarot_click_next", Ordinals[Mathf.Min(i, Ordinals.Length - 1)], draw.position));
                    yield return stage.WaitAndReveal(i, card, draw.reversed, app.Deck.ArtFor(card));
                    if (card == null)
                        continue;
                    var meaning = card.Meaning(draw.reversed);
                    string orientation = draw.reversed ? "逆位" : "正位";
                    yield return dialogue.Say(lines.Format(spread.positions.Length == 1 ? "tarot_reveal_single" : "tarot_reveal", draw.position, card.name, orientation));
                    yield return dialogue.Say(meaning.meaning);
                    yield return dialogue.Say(meaning.advice);
                }

                TarotRecordStore.Append(new TarotRecord
                {
                    castAtUtc = System.DateTime.UtcNow.ToString("o"),
                    spread = spread.id,
                    timeOfDay = app.Lighting != null ? RoomLighting.Key(app.Lighting.Current) : "",
                    draws = draws,
                    deckVersion = app.Deck.Version,
                });

                if (draws.Count >= 3)
                {
                    var names = new string[3];
                    for (int i = 0; i < 3; i++)
                        names[i] = app.Deck.Find(draws[i].cardId)?.name ?? "?";
                    yield return dialogue.Say(lines.Format("tarot_summary_three", names[0], names[1], names[2]));
                }
                yield return dialogue.Say(lines.Pick("tarot_closing"));

                int next = -1;
                yield return dialogue.Ask(lines.Pick("tarot_more"), AgainChoices, i => next = i);
                if (next != 0)
                    break;
                first = false;
            }
            stage.Hide();
            yield return dialogue.Say(lines.Pick("tarot_end"));
        }
    }
}
