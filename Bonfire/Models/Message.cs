using Bonfire.Core;
using Bonfire.Services;
using System.Text.Json.Serialization;

public static class Message
{
    public enum Role { Sender, Recipient, Transit }
    public enum Status { Queued = 0, Transferred = 1, Delivered = 2 }

    public record Id(Bytes SenderKey, Bytes RecipientKey, int Number)
    {
        [JsonIgnore]
        public Role Role =>
            SenderKey == ThisDevice.PublicKey ? Role.Sender :
            RecipientKey == ThisDevice.PublicKey ? Role.Recipient :
            Role.Transit;
    }

    public readonly record struct Wire(Id Id, string Text, DateTime CreatedAt, int Ttl);
    public readonly record struct UI(string Text, string Time, bool IsMine);
}