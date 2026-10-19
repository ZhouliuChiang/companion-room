using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace CompanionRoom.Tarot
{
    /// <summary>A finished reading, as kept in the cabinet.</summary>
    [Serializable]
    public class TarotRecord
    {
        public string id;
        public string castAtUtc;
        public string spread;
        public string question = "";
        public string timeOfDay = "";
        public List<TarotDraw> draws = new();
        public string deckVersion = "";

        public DateTime CastAtLocal => DateTime.TryParse(castAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time)
            ? time.ToLocalTime()
            : DateTime.MinValue;

        public string SpreadName => TarotSpread.Find(spread)?.name ?? spread;
    }

    /// <summary>
    /// Reading history in persistentDataPath/CompanionRoom/tarot_records.json, newest last. Writes go
    /// through a temp file so a crash never leaves half a file; an unreadable file is set aside as
    /// .bad rather than overwritten.
    /// </summary>
    public static class TarotRecordStore
    {
        public const int SchemaVersion = 1;
        public const int MaxRecords = 200;
        const string FileName = "tarot_records.json";

        [Serializable]
        class FileData
        {
            public int schemaVersion = SchemaVersion;
            public List<TarotRecord> records = new();
        }

        /// <summary>Tests and previews point this elsewhere so they never touch the player's history.</summary>
        public static string DirectoryOverride { get; set; }

        public static string Directory => string.IsNullOrEmpty(DirectoryOverride)
            ? Path.Combine(Application.persistentDataPath, "CompanionRoom")
            : DirectoryOverride;

        public static string FilePath => Path.Combine(Directory, FileName);

        public static event Action Changed;

        public static List<TarotRecord> Load()
        {
            var path = FilePath;
            if (!File.Exists(path))
                return new List<TarotRecord>();
            try
            {
                var data = JsonUtility.FromJson<FileData>(File.ReadAllText(path));
                if (data?.records == null)
                    throw new FormatException("No records list.");
                // Also drops the pendulum readings older versions wrote: they have no cards.
                data.records.RemoveAll(r => r == null || r.draws == null || r.draws.Count == 0);
                return data.records;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CompanionRoom] Tarot records unreadable, set aside: {e.Message}");
                try
                {
                    File.Copy(path, path + ".bad", true);
                }
                catch (Exception) { }
                return new List<TarotRecord>();
            }
        }

        public static void Append(TarotRecord record)
        {
            var records = Load();
            if (string.IsNullOrEmpty(record.id))
                record.id = Guid.NewGuid().ToString("N");
            records.Add(record);
            if (records.Count > MaxRecords)
                records.RemoveRange(0, records.Count - MaxRecords);
            Save(records);
        }

        public static void Delete(string id)
        {
            var records = Load();
            if (records.RemoveAll(r => r.id == id) > 0)
                Save(records);
        }

        public static void Clear() => Save(new List<TarotRecord>());

        static void Save(List<TarotRecord> records)
        {
            System.IO.Directory.CreateDirectory(Directory);
            var path = FilePath;
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(new FileData { records = records }, true));
            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);
            Changed?.Invoke();
        }
    }
}
