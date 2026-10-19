using System;
using System.Collections.Generic;
using UnityEngine;

namespace CompanionRoom.Tarot
{
    [Serializable]
    public class TarotMeaning
    {
        [Tooltip("Short keywords shown under the card name.")]
        public string keywords;
        [Tooltip("What the card says in this position (凡安 reads it out).")]
        public string meaning;
        [Tooltip("凡安's gentle advice after the meaning.")]
        public string advice;
    }

    [Serializable]
    public class TarotCard
    {
        public string id;
        public int number;
        public string name;
        public string english;
        public string numeral;
        public TarotMeaning upright;
        public TarotMeaning reversed;

        public TarotMeaning Meaning(bool isReversed) => isReversed ? reversed : upright;
    }

    /// <summary>A way of laying out cards: one position name per card.</summary>
    public sealed class TarotSpread
    {
        public readonly string id;
        public readonly string name;
        public readonly string[] positions;

        public TarotSpread(string id, string name, params string[] positions)
        {
            this.id = id;
            this.name = name;
            this.positions = positions;
        }

        public static readonly TarotSpread Single = new("single", "單張指引", "指引");
        public static readonly TarotSpread ThreeCards = new("three", "過去・現在・未來", "過去", "現在", "未來");
        public static readonly TarotSpread[] All = { Single, ThreeCards };

        public static TarotSpread Find(string id)
        {
            foreach (var spread in All)
                if (spread.id == id)
                    return spread;
            return null;
        }
    }

    /// <summary>One card as it landed in a reading.</summary>
    [Serializable]
    public class TarotDraw
    {
        public string cardId;
        public bool reversed;
        public string position;
    }

    /// <summary>
    /// The full 78-card deck, loaded from Resources/CompanionRoom/Tarot/tarot_cards.json.
    /// Card art, when present, is
    /// Resources/CompanionRoom/Tarot/Cards/&lt;id&gt;.png imported as a Sprite.
    /// </summary>
    public sealed class TarotDeck
    {
        public const string ResourcePath = "CompanionRoom/Tarot/tarot_cards";
        public const string ArtFolder = "CompanionRoom/Tarot/Cards/";

        [Serializable]
        class FileData
        {
            public string version;
            public float reversedChance = 0.35f;
            public TarotCard[] cards;
        }

        readonly Dictionary<string, TarotCard> byId = new();
        readonly Dictionary<string, Sprite> art = new();

        public IReadOnlyList<TarotCard> Cards { get; }
        public string Version { get; }
        public float ReversedChance { get; }

        TarotDeck(FileData data)
        {
            var cards = new List<TarotCard>();
            if (data?.cards != null)
                foreach (var card in data.cards)
                    if (card != null && !string.IsNullOrEmpty(card.id) && card.upright != null && card.reversed != null)
                    {
                        cards.Add(card);
                        byId[card.id] = card;
                    }
            Cards = cards;
            Version = data?.version ?? "";
            ReversedChance = Mathf.Clamp01(data?.reversedChance ?? 0.35f);
        }

        public static TarotDeck Load()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Debug.LogError($"[CompanionRoom] Missing tarot deck Resources/{ResourcePath}.json");
                return new TarotDeck(null);
            }
            return new TarotDeck(JsonUtility.FromJson<FileData>(asset.text));
        }

        public TarotCard Find(string id) => id != null && byId.TryGetValue(id, out var card) ? card : null;

        /// <summary>The card's illustration, or null to use the drawn placeholder face.</summary>
        public Sprite ArtFor(TarotCard card)
        {
            if (card == null)
                return null;
            if (!art.TryGetValue(card.id, out var sprite))
            {
                sprite = Resources.Load<Sprite>(ArtFolder + card.id);
                if (sprite == null)
                {
                    var texture = Resources.Load<Texture2D>(ArtFolder + card.id);
                    if (texture != null)
                        sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                }
                art[card.id] = sprite;
            }
            return sprite;
        }

        /// <summary>Shuffles and deals one card per position, each upright or reversed.</summary>
        public List<TarotDraw> Deal(TarotSpread spread, System.Random random)
        {
            var order = new List<TarotCard>(Cards);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            var draws = new List<TarotDraw>();
            for (int i = 0; i < spread.positions.Length && i < order.Count; i++)
                draws.Add(new TarotDraw
                {
                    cardId = order[i].id,
                    reversed = random.NextDouble() < ReversedChance,
                    position = spread.positions[i],
                });
            return draws;
        }
    }
}
