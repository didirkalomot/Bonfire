using System.Runtime.InteropServices;
using Bonfire.Core;
using Bonfire.Models;

namespace Bonfire.Services.Storage;

public static class PeerTable
{
    static readonly Dictionary<Bytes, Peer.Data> Data = new();
    static readonly object Lock = new();
    public static void UpdateFromBeacon(Bytes publicKey)
    {
        lock (Lock)
        {
            ref var peer = ref CollectionsMarshal.GetValueRefOrAddDefault(Data, publicKey, out var exists);
            if (exists) peer!.LastSeen = DateTime.UtcNow;
            else peer = new Peer.Data(publicKey);
        }
    }
    public static Peer.Data? Get(Bytes publicKey) { lock (Lock) return Data.GetValueOrDefault(publicKey); }
    public static IReadOnlyList<Peer.Data> GetAll() { lock (Lock) return Data.Values.ToList(); }
    public static void RemoveOld(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        lock (Lock)
        {
            foreach (var key in Data.Where(kv => kv.Value.LastSeen < cutoff).Select(kv => kv.Key).ToList())
                Data.Remove(key);
        }
    }
    public static int Count { get { lock (Lock) return Data.Count; } }
}