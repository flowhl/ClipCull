using System;
using System.Collections.Concurrent;
using System.IO;
using Microsoft.Data.Sqlite;

namespace ClipCull.Core.Import
{
    /// <summary>
    /// A single global SHA-256 cache stored next to the app settings
    /// (<c>…\ClipCull\Settings\hashcache.db</c>). Keyed by absolute file path plus size and
    /// created/modified timestamps, it lets duplicate detection reuse hashes across runs for both
    /// source (card) and target (library) files, and avoids writing anything into those folders.
    ///
    /// Overlapping folders (e.g. X:\Photos and X:\Photos\Trip) therefore share one cache instead of
    /// maintaining separate per-folder databases. Degrades to live hashing if the DB is unavailable.
    /// </summary>
    public static class HashCache
    {
        private static readonly object _lock = new();
        private static readonly ConcurrentDictionary<string, string> _mem = new(); // key -> hash
        private static SqliteConnection _conn;
        private static bool _opened;

        private static string DbPath => Path.Combine(Globals.SettingsPath, "hashcache.db");

        private static void EnsureOpen()
        {
            if (_opened) return;
            _opened = true;
            try
            {
                if (!Directory.Exists(Globals.SettingsPath))
                    Directory.CreateDirectory(Globals.SettingsPath);

                var conn = new SqliteConnection($"Data Source={DbPath}");
                conn.Open();
                using (var pragma = conn.CreateCommand())
                {
                    pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
                    pragma.ExecuteNonQuery();
                }
                using (var create = conn.CreateCommand())
                {
                    create.CommandText = @"
CREATE TABLE IF NOT EXISTS file_hashes (
    path     TEXT PRIMARY KEY,
    size     INTEGER NOT NULL,
    created  INTEGER NOT NULL,
    modified INTEGER NOT NULL,
    hash     TEXT NOT NULL
);";
                    create.ExecuteNonQuery();
                }
                _conn = conn;
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"Hash cache unavailable: {ex.Message}");
                _conn = null;
            }
        }

        private static string NormalizePath(string path)
        {
            try { return Path.GetFullPath(path).ToLowerInvariant(); }
            catch { return path?.ToLowerInvariant(); }
        }

        /// <summary>
        /// Returns the SHA-256 hex of the file, reusing a cached value when path + size + created +
        /// modified match a previous run. Computes and stores it on a miss.
        /// </summary>
        public static string GetHash(string fullPath)
        {
            if (!TryStat(fullPath, out var key, out var size, out var created, out var modified))
                return FileHasher.Compute(fullPath);

            if (_mem.TryGetValue(key, out var known))
                return known;

            lock (_lock)
            {
                EnsureOpen();
                var cached = Query(NormalizePath(fullPath), size, created, modified);
                if (cached != null)
                {
                    _mem[key] = cached;
                    return cached;
                }
            }

            var hash = FileHasher.Compute(fullPath);
            Store(fullPath, key, size, created, modified, hash);
            return hash;
        }

        /// <summary>
        /// Returns the hash only if it is already known (in memory or the DB) without hashing the
        /// file. Used to opportunistically prefill after a copy/move.
        /// </summary>
        public static string TryGetKnown(string fullPath)
        {
            if (!TryStat(fullPath, out var key, out var size, out var created, out var modified))
                return null;

            if (_mem.TryGetValue(key, out var known))
                return known;

            lock (_lock)
            {
                EnsureOpen();
                var cached = Query(NormalizePath(fullPath), size, created, modified);
                if (cached != null)
                    _mem[key] = cached;
                return cached;
            }
        }

        /// <summary>
        /// Records a known hash for a file (its current size/timestamps on disk). Used after copying
        /// a file whose content hash we already know, so the new location is cached without re-reading.
        /// </summary>
        public static void Prefill(string fullPath, string hash)
        {
            if (string.IsNullOrEmpty(hash)) return;
            if (!TryStat(fullPath, out var key, out var size, out var created, out var modified))
                return;
            Store(fullPath, key, size, created, modified, hash);
        }

        private static bool TryStat(string fullPath, out string key, out long size, out long created, out long modified)
        {
            key = null; size = 0; created = 0; modified = 0;
            try
            {
                var fi = new FileInfo(fullPath);
                if (!fi.Exists) return false;
                size = fi.Length;
                created = fi.CreationTimeUtc.Ticks;
                modified = fi.LastWriteTimeUtc.Ticks;
                key = $"{NormalizePath(fullPath)}|{size}|{created}|{modified}";
                return true;
            }
            catch { return false; }
        }

        private static void Store(string fullPath, string key, long size, long created, long modified, string hash)
        {
            _mem[key] = hash;
            lock (_lock)
            {
                EnsureOpen();
                if (_conn == null) return;
                try { Upsert(NormalizePath(fullPath), size, created, modified, hash); }
                catch (Exception ex) { Logger.LogDebug($"Hash cache write failed: {ex.Message}"); }
            }
        }

        private static string Query(string path, long size, long created, long modified)
        {
            if (_conn == null) return null;
            try
            {
                using var cmd = _conn.CreateCommand();
                cmd.CommandText =
                    "SELECT hash FROM file_hashes WHERE path=$p AND size=$s AND created=$c AND modified=$m LIMIT 1";
                cmd.Parameters.AddWithValue("$p", path);
                cmd.Parameters.AddWithValue("$s", size);
                cmd.Parameters.AddWithValue("$c", created);
                cmd.Parameters.AddWithValue("$m", modified);
                return cmd.ExecuteScalar() as string;
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"Hash cache read failed: {ex.Message}");
                return null;
            }
        }

        private static void Upsert(string path, long size, long created, long modified, string hash)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = @"
INSERT INTO file_hashes (path, size, created, modified, hash)
VALUES ($p, $s, $c, $m, $h)
ON CONFLICT(path) DO UPDATE SET size=$s, created=$c, modified=$m, hash=$h;";
            cmd.Parameters.AddWithValue("$p", path);
            cmd.Parameters.AddWithValue("$s", size);
            cmd.Parameters.AddWithValue("$c", created);
            cmd.Parameters.AddWithValue("$m", modified);
            cmd.Parameters.AddWithValue("$h", hash);
            cmd.ExecuteNonQuery();
        }

        public static void Shutdown()
        {
            lock (_lock)
            {
                try { _conn?.Dispose(); } catch { }
                _conn = null;
                _opened = false;
            }
        }
    }
}
