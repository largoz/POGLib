using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace POGLib.Cosmetics.Model
{
    /// <summary>
    /// Which locked cosmetics this player has unlocked: a file of this library's own, never the game's list (that one is in the
    /// player's Steam Cloud). Ids of mods that are not installed stay in it. No Unity types, so the unit tests compile it.
    /// </summary>
    internal sealed class UnlockList
    {
        public const int Version = 1;

        private readonly string _path;
        private readonly Action<string> _warn;
        private readonly SortedSet<string> _ids = new SortedSet<string>(StringComparer.Ordinal);
        private bool _loaded, _readOnly;

        public UnlockList(string path, Action<string> warn) { _path = path; _warn = warn; }

        public bool Contains(string fullId) { Load(); return _ids.Contains(fullId); }

        /// <summary>True when it was not there before.</summary>
        public bool Add(string fullId)
        {
            Load();
            if (!_ids.Add(fullId)) return false;
            Save();
            return true;
        }

        private void Load()
        {
            if (_loaded) return;
            _loaded = true;
            if (!File.Exists(_path)) return;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(_path));
                int version = doc.RootElement.GetProperty("version").GetInt32();
                if (version > Version) { _readOnly = true; _warn($"{_path} is of version {version}, newer than this library: it is read, not written."); }
                foreach (var e in doc.RootElement.GetProperty("unlocked").EnumerateArray())
                    if (e.ValueKind == JsonValueKind.String && e.GetString().Length > 0) _ids.Add(e.GetString());
            }
            catch (Exception ex)
            {
                // Not thrown away: the player can put it right, and nothing is written over it.
                _readOnly = true;
                _warn($"{_path} cannot be read ({ex.GetType().Name}: {ex.Message}); what is unlocked from now on is kept only until the game closes.");
            }
        }

        private void Save()
        {
            if (_readOnly) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                var temp = _path + ".new";
                File.WriteAllText(temp, JsonSerializer.Serialize(new { version = Version, unlocked = _ids.ToArray() }, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(temp, _path, true);
            }
            catch (Exception ex) { _warn($"{_path} cannot be written ({ex.GetType().Name}: {ex.Message})."); }
        }
    }
}
