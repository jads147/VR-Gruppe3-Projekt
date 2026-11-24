using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Ein Eintrag der Bestenliste.</summary>
[Serializable]
public class LeaderboardEntry
{
    public string name;
    public int score;
    /// <summary>Benötigte Zeit in Sekunden.</summary>
    public float timeSeconds;
    /// <summary>Zeitpunkt des Eintrags (Unix-Sekunden, UTC).</summary>
    public long unixTime;

    public DateTime LocalTime => DateTimeOffset.FromUnixTimeSeconds(unixTime).LocalDateTime;
    public string FormattedTime => Leaderboard.FormatTime(timeSeconds);
}

/// <summary>
/// Persistente Bestenliste, getrennt pro Level. Wird als JSON in <see cref="Application.persistentDataPath"/>
/// gespeichert und bleibt damit über Neustarts erhalten (PC: %userprofile%\AppData\LocalLow\&lt;Firma&gt;\&lt;Produkt&gt;,
/// Quest: /sdcard/Android/data/&lt;Paket&gt;/files). Erst eine Deinstallation löscht sie.
///
/// Wertung: höchster Score zuerst, bei Gleichstand die kürzere Zeit, danach der ältere Eintrag.
/// </summary>
public static class Leaderboard
{
    /// <summary>So viele Einträge werden pro Level maximal gespeichert.</summary>
    public const int MaxStoredEntries = 100;

    public const int MaxNameLength = 16;

    /// <summary>Level-ID, wenn kein <see cref="LevelManager"/> in der Szene ist.</summary>
    public const string DefaultLevelId = "default";

    const string FileName = "leaderboard.json";

    [Serializable]
    class LevelBoard
    {
        public string levelId;
        public List<LeaderboardEntry> entries = new();
    }

    [Serializable]
    class SaveData
    {
        public int version = 2;
        public List<LevelBoard> levels = new();
        /// <summary>Nur Version 1 (vor den Leveln). Wird beim Laden nach <see cref="DefaultLevelId"/> übernommen.</summary>
        public List<LeaderboardEntry> entries = null;
    }

    static Dictionary<string, List<LeaderboardEntry>> s_Boards;
    static readonly List<LeaderboardEntry> s_Empty = new();

    /// <summary>Wird nach jeder Änderung (Eintrag, Löschen) ausgelöst.</summary>
    public static event Action Changed;

    /// <summary>Der zuletzt in dieser Sitzung eingetragene Eintrag (für Hervorhebung), sonst null.</summary>
    public static LeaderboardEntry LastSubmitted { get; private set; }

    /// <summary>Level, zu dem <see cref="LastSubmitted"/> gehört.</summary>
    public static string LastSubmittedLevelId { get; private set; }

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    // Statische Daten zurücksetzen, falls "Domain Reload" beim Play-Mode-Start deaktiviert ist.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        s_Boards = null;
        LastSubmitted = null;
        LastSubmittedLevelId = null;
        Changed = null;
    }

    /// <summary>Alle Level, für die es Einträge gibt.</summary>
    public static IEnumerable<string> LevelIds
    {
        get
        {
            EnsureLoaded();
            return s_Boards.Keys;
        }
    }

    /// <summary>Anzahl aller Einträge über alle Level.</summary>
    public static int TotalCount
    {
        get
        {
            EnsureLoaded();
            int count = 0;
            foreach (var board in s_Boards.Values)
                count += board.Count;
            return count;
        }
    }

    /// <summary>Einträge eines Levels, sortiert (Index 0 = Platz 1).</summary>
    public static IReadOnlyList<LeaderboardEntry> GetEntries(string levelId)
    {
        EnsureLoaded();
        return s_Boards.TryGetValue(NormalizeId(levelId), out var board) ? board : s_Empty;
    }

    /// <summary>Platz, den dieser Score/diese Zeit bekommen würde (1-basiert), ohne ihn einzutragen.</summary>
    public static int GetRank(string levelId, int score, float timeSeconds)
    {
        var candidate = new LeaderboardEntry { score = score, timeSeconds = timeSeconds, unixTime = long.MaxValue };
        int rank = 1;
        foreach (var entry in GetEntries(levelId))
        {
            if (Compare(entry, candidate) <= 0)
                rank++;
            else
                break;
        }
        return rank;
    }

    /// <summary>Platz eines vorhandenen Eintrags (1-basiert) oder 0, falls nicht in der Liste.</summary>
    public static int GetRank(string levelId, LeaderboardEntry entry)
    {
        var entries = GetEntries(levelId);
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] == entry)
                return i + 1;
        }
        return 0;
    }

    /// <summary>
    /// Trägt ein Ergebnis ein, speichert sofort und gibt den erreichten Platz zurück (1-basiert).
    /// </summary>
    public static int Submit(string levelId, string playerName, int score, float timeSeconds)
    {
        EnsureLoaded();
        levelId = NormalizeId(levelId);

        var entry = new LeaderboardEntry
        {
            name = SanitizeName(playerName),
            score = score,
            timeSeconds = Mathf.Max(0f, timeSeconds),
            unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };

        int rank = GetRank(levelId, score, entry.timeSeconds);

        if (!s_Boards.TryGetValue(levelId, out var board))
            s_Boards[levelId] = board = new List<LeaderboardEntry>();

        board.Insert(rank - 1, entry);
        if (board.Count > MaxStoredEntries)
            board.RemoveRange(MaxStoredEntries, board.Count - MaxStoredEntries);

        LastSubmitted = entry;
        LastSubmittedLevelId = levelId;
        Save();
        Changed?.Invoke();
        return rank;
    }

    /// <summary>Löscht die Einträge eines Levels.</summary>
    public static void Clear(string levelId)
    {
        EnsureLoaded();
        levelId = NormalizeId(levelId);
        s_Boards.Remove(levelId);
        if (LastSubmittedLevelId == levelId)
        {
            LastSubmitted = null;
            LastSubmittedLevelId = null;
        }
        Save();
        Changed?.Invoke();
    }

    /// <summary>Löscht alle Einträge aller Level.</summary>
    public static void ClearAll()
    {
        EnsureLoaded();
        s_Boards.Clear();
        LastSubmitted = null;
        LastSubmittedLevelId = null;
        Save();
        Changed?.Invoke();
    }

    /// <summary>Verwirft den Zwischenspeicher und liest die Datei neu ein.</summary>
    public static void Reload()
    {
        s_Boards = null;
        EnsureLoaded();
        Changed?.Invoke();
    }

    /// <summary>Formatiert Sekunden als "m:ss.f" (ab einer Stunde "h:mm:ss").</summary>
    public static string FormatTime(float seconds)
    {
        var time = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes}:{time.Seconds:00}.{time.Milliseconds / 100}";
    }

    /// <summary>Sortierung: Score absteigend, dann Zeit aufsteigend, dann älterer Eintrag zuerst.</summary>
    static int Compare(LeaderboardEntry a, LeaderboardEntry b)
    {
        if (a.score != b.score)
            return b.score.CompareTo(a.score);
        if (!Mathf.Approximately(a.timeSeconds, b.timeSeconds))
            return a.timeSeconds.CompareTo(b.timeSeconds);
        return a.unixTime.CompareTo(b.unixTime);
    }

    static string NormalizeId(string levelId) =>
        string.IsNullOrWhiteSpace(levelId) ? DefaultLevelId : levelId.Trim();

    static string SanitizeName(string playerName)
    {
        // Spitze Klammern entfernen, damit kein TMP-Rich-Text eingeschleust wird.
        string clean = (playerName ?? string.Empty).Replace("<", "").Replace(">", "").Trim();
        if (clean.Length > MaxNameLength)
            clean = clean.Substring(0, MaxNameLength).TrimEnd();
        return clean.Length > 0 ? clean : "Anonymous";
    }

    static void EnsureLoaded()
    {
        if (s_Boards != null)
            return;

        s_Boards = new Dictionary<string, List<LeaderboardEntry>>();

        string path = FilePath;
        string tempPath = path + ".tmp";
        // Absturz zwischen Löschen und Umbenennen beim Speichern? Dann die temporäre Datei nehmen.
        if (!File.Exists(path) && File.Exists(tempPath))
            path = tempPath;

        if (!File.Exists(path))
            return;

        try
        {
            var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            if (data == null)
                return;

            if (data.levels != null)
            {
                foreach (var board in data.levels)
                {
                    if (board?.entries != null)
                        AddLoaded(board.levelId, board.entries);
                }
            }

            // Version 1: Einträge ohne Level -> Standard-Level.
            if (data.entries != null && data.entries.Count > 0)
                AddLoaded(DefaultLevelId, data.entries);
        }
        catch (Exception e)
        {
            // Kaputte Datei nicht überschreiben, sondern zur Seite legen.
            Debug.LogWarning($"[Leaderboard] Datei konnte nicht gelesen werden, starte leer. ({e.Message})");
            try { File.Copy(path, path + ".corrupt", true); } catch { /* egal */ }
        }
    }

    static void AddLoaded(string levelId, List<LeaderboardEntry> entries)
    {
        levelId = NormalizeId(levelId);
        if (!s_Boards.TryGetValue(levelId, out var board))
            s_Boards[levelId] = board = new List<LeaderboardEntry>();

        foreach (var entry in entries)
        {
            if (entry != null)
                board.Add(entry);
        }

        // Stabil sortieren (falls die Datei von Hand bearbeitet oder zusammengeführt wurde).
        var sorted = new List<LeaderboardEntry>(board);
        sorted.Sort(Compare);
        board.Clear();
        board.AddRange(sorted);
    }

    static void Save()
    {
        string path = FilePath;
        string tempPath = path + ".tmp";
        try
        {
            var data = new SaveData();
            foreach (var pair in s_Boards)
                data.levels.Add(new LevelBoard { levelId = pair.Key, entries = pair.Value });

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string json = JsonUtility.ToJson(data, true);

            // Erst komplett in eine temporäre Datei schreiben, dann austauschen,
            // damit ein Absturz mitten im Schreiben die Liste nicht zerstört.
            File.WriteAllText(tempPath, json);
            if (File.Exists(path))
                File.Delete(path);
            File.Move(tempPath, path);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Leaderboard] Speichern fehlgeschlagen: {e.Message}");
        }
    }
}
