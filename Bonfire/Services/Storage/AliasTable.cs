using Bonfire.Core;
using banditoth.MAUI.PreferencesExtension;

namespace Bonfire.Services.Storage;

public static class AliasTable
{
    const string StorageKey = "AliasTable";
    static readonly Dictionary<Bytes, string> Aliases = new();
    static readonly object Lock = new();
    static AliasTable() => Load();

    public static string GetPeerName(Bytes publicKey)
    {
        lock (Lock)
        {
            if (Aliases.GetValueOrDefault(publicKey) is { } alias) return alias;
            var hex = Convert.ToHexString(publicKey.Span);
            return hex.Length >= 8 ? $"Узел {hex[..8]}" : $"Узел {hex}";
        }
    }

    public static void Set(Bytes publicKey, string alias)
    {
        lock (Lock)
        {
            if (string.IsNullOrWhiteSpace(alias)) Aliases.Remove(publicKey);
            else Aliases[publicKey] = alias.Trim();
        }
        Save();
    }

    public static void Remove(Bytes publicKey) { lock (Lock) Aliases.Remove(publicKey); Save(); }

    private static void Load()
    {
        try
        {
            var stored = PreferencesExtension.GetObject<Dictionary<Bytes, string>>(StorageKey, default!);
            if (stored is null) return;
            lock (Lock) foreach (var kv in stored) Aliases[kv.Key] = kv.Value;
        }
        catch { }
    }

    private static void Save()
    {
        try
        {
            Dictionary<Bytes, string> snapshot;
            lock (Lock) snapshot = new(Aliases);
            PreferencesExtension.SetObject(StorageKey, snapshot);
        }
        catch { }
    }
}