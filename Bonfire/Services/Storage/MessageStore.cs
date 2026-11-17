using Microsoft.Data.Sqlite;
using Bonfire.Core;
using Bonfire.Models;
using System.Globalization;

namespace Bonfire.Services.Storage;

public static class MessageStore
{
    static readonly string ConnectionString;
    static readonly SemaphoreSlim Gate = new(1, 1);

    static MessageStore()
    {
        var path = Path.Combine(FileSystem.AppDataDirectory, "bonfire.db3");
        ConnectionString = $"Data Source={path}";
        Init();
    }

    static void Init()
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Messages (
                SenderKey    BLOB    NOT NULL,
                RecipientKey BLOB    NOT NULL,
                Number       INTEGER NOT NULL,
                Text         TEXT    NOT NULL,
                CreatedAt    TEXT    NOT NULL,
                Ttl          INTEGER NOT NULL,
                Status       INTEGER NOT NULL,
                PRIMARY KEY (SenderKey, RecipientKey, Number)
            );";
        cmd.ExecuteNonQuery();
    }

    public static async Task SaveAsync(List<Message.Wire> messages)
    {
        if (messages.Count == 0) return;
        await Gate.WaitAsync();
        try
        {
            using var conn = new SqliteConnection(ConnectionString);
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
            INSERT INTO Messages (SenderKey, RecipientKey, Number, Text, CreatedAt, Ttl, Status)
            VALUES ($s, $r, $n, $t, $c, $ttl, $st)
            ON CONFLICT(SenderKey, RecipientKey, Number) DO UPDATE SET
                Text = excluded.Text,
                Ttl = excluded.Ttl,
                Status = MAX(excluded.Status, Status);";

            var pS = cmd.Parameters.Add("$s", SqliteType.Blob);
            var pR = cmd.Parameters.Add("$r", SqliteType.Blob);
            var pN = cmd.Parameters.Add("$n", SqliteType.Integer);
            var pT = cmd.Parameters.Add("$t", SqliteType.Text);
            var pC = cmd.Parameters.Add("$c", SqliteType.Text);
            var pTtl = cmd.Parameters.Add("$ttl", SqliteType.Integer);
            var pSt = cmd.Parameters.Add("$st", SqliteType.Integer);

            foreach (var m in messages)
            {
                pS.Value = m.Id.SenderKey.Span.ToArray();
                pR.Value = m.Id.RecipientKey.Span.ToArray();
                pN.Value = m.Id.Number;
                pT.Value = m.Text;
                pC.Value = m.CreatedAt.ToString("O");
                pTtl.Value = m.Ttl;
                pSt.Value = m.Id.Role == Message.Role.Recipient
                    ? (int)Message.Status.Delivered
                    : (int)Message.Status.Queued;
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }
        finally { Gate.Release(); }
    }

    public static async Task<List<Message.UI>> GetChatAsync(Bytes peer)
    {
        var me = ThisDevice.PublicKey.Span.ToArray();
        var peerBytes = peer.Span.ToArray();

        await Gate.WaitAsync();
        try
        {
            using var conn = new SqliteConnection(ConnectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
            SELECT SenderKey, RecipientKey, Number, Text, CreatedAt
            FROM Messages
            WHERE (SenderKey = $me AND RecipientKey = $peer)
               OR (SenderKey = $peer AND RecipientKey = $me)
            ORDER BY CreatedAt;";
            cmd.Parameters.AddWithValue("$me", me);
            cmd.Parameters.AddWithValue("$peer", peerBytes);

            return await SqlToListAsync(cmd, r =>
            {
                var id = new Message.Id((byte[])r[0], (byte[])r[1], r.GetInt32(2));
                var created = DateTime.Parse(r.GetString(4), CultureInfo.InvariantCulture);

                return new Message.UI(
                    r.GetString(3),
                    created.ToLocalTime().ToString("HH:mm"),
                    id.Role == Message.Role.Sender);
            });
        }
        finally { Gate.Release(); }
    }

    public static async Task<List<Bytes>> GetQueuedRecipientsAsync()
    {
        await Gate.WaitAsync();
        try
        {
            using var conn = new SqliteConnection(ConnectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT RecipientKey FROM Messages WHERE Status = $st;";
            cmd.Parameters.AddWithValue("$st", (int)Message.Status.Queued);

            var list = new List<Bytes>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                list.Add((byte[])reader[0]);
            return list;
        }
        finally { Gate.Release(); }
    }

    public static async Task<List<Message.Wire>> TakeQueuedForRecipientsAsync(IEnumerable<Bytes> recipients)
    {
        var keys = recipients.Select(r => r.Span.ToArray()).ToList();
        if (keys.Count == 0) return new();

        var placeholders = string.Join(",", keys.Select((_, i) => $"$k{i}"));
        await Gate.WaitAsync();
        try
        {
            using var conn = new SqliteConnection(ConnectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
            UPDATE Messages SET Status = $transferred
            WHERE Status = $queued AND RecipientKey IN ({placeholders})
            RETURNING SenderKey, RecipientKey, Number, Text, CreatedAt, Ttl;";
            cmd.Parameters.AddWithValue("$transferred", (int)Message.Status.Transferred);
            cmd.Parameters.AddWithValue("$queued", (int)Message.Status.Queued);
            for (int i = 0; i < keys.Count; i++) cmd.Parameters.AddWithValue($"$k{i}", keys[i]);

            return await SqlToListAsync(cmd, r => new Message.Wire(
                new Message.Id((byte[])r[0], (byte[])r[1], r.GetInt32(2)),
                r.GetString(3),
                DateTime.Parse(r.GetString(4), CultureInfo.InvariantCulture),
                r.GetInt32(5)));
        }
        finally { Gate.Release(); }
    }

    static async Task<List<T>> SqlToListAsync<T>(SqliteCommand cmd, Func<SqliteDataReader, T> map)
    {
        using var reader = await cmd.ExecuteReaderAsync();
        var list = new List<T>();
        while (await reader.ReadAsync())
            list.Add(map(reader));
        return list;
    }
}