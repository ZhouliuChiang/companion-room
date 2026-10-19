using System;
using System.Collections.Generic;
using UnityEngine;

namespace CompanionRoom
{
    /// <summary>
    /// 凡安's lines, loaded from Resources/CompanionRoom/Dialogue/room_lines.json. Each topic (a key
    /// such as "photo" or "window_night") has several interchangeable sets; a set is a short run of
    /// lines said one after another. Picking avoids repeating the set used last time for that topic.
    /// </summary>
    public sealed class DialogueLibrary
    {
        public const string ResourcePath = "CompanionRoom/Dialogue/room_lines";

        [Serializable]
        public class Set
        {
            public string[] lines;
        }

        [Serializable]
        public class Topic
        {
            public string key;
            public string note;
            public Set[] sets;
        }

        [Serializable]
        class FileData
        {
            public string speaker;
            public Topic[] topics;
        }

        readonly Dictionary<string, Topic> topics = new();
        readonly Dictionary<string, int> lastPick = new();
        readonly System.Random random = new();

        public string Speaker { get; }

        DialogueLibrary(FileData data)
        {
            // The dialogue data supplies the character's displayed name.
            Speaker = data?.speaker ?? "";
            if (data?.topics == null)
                return;
            foreach (var topic in data.topics)
            {
                if (topic == null || string.IsNullOrEmpty(topic.key) || topic.sets == null || topic.sets.Length == 0)
                    continue;
                topics[topic.key] = topic;
            }
        }

        public static DialogueLibrary Load()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Debug.LogError($"[CompanionRoom] Missing dialogue file Resources/{ResourcePath}.json");
                return new DialogueLibrary(null);
            }
            try
            {
                return new DialogueLibrary(JsonUtility.FromJson<FileData>(asset.text));
            }
            catch (Exception e)
            {
                Debug.LogError($"[CompanionRoom] Could not read {ResourcePath}.json: {e.Message}");
                return new DialogueLibrary(null);
            }
        }

        public bool Has(string key) => key != null && topics.ContainsKey(key);

        public IEnumerable<string> Keys => topics.Keys;

        /// <summary>A random set from the first topic that exists; a "..." line when none do.</summary>
        public string[] Pick(params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!Has(key))
                    continue;
                var sets = topics[key].sets;
                int previous = lastPick.TryGetValue(key, out var p) ? p : -1;
                int index = sets.Length == 1 ? 0 : random.Next(sets.Length - (previous >= 0 ? 1 : 0));
                if (previous >= 0 && sets.Length > 1 && index >= previous)
                    index++;
                lastPick[key] = index;
                return sets[index].lines ?? Array.Empty<string>();
            }
            Debug.LogWarning($"[CompanionRoom] No dialogue for: {string.Join(", ", keys)}");
            return new[] { "……" };
        }

        /// <summary>The single line of a topic's first set, with {0}.. filled in (for templates).</summary>
        public string Format(string key, params object[] args)
        {
            var lines = Pick(key);
            string template = lines.Length > 0 ? lines[0] : "";
            try
            {
                return string.Format(template, args);
            }
            catch (FormatException)
            {
                return template;
            }
        }
    }
}
