using System.Collections.ObjectModel;

namespace Bonfire.Views;

// Shell автоматически «впрыснет» значение из ?peerId=... в это свойство
[QueryProperty(nameof(PeerId), "peerId")]
public partial class Chat : ContentPage
{
    public ObservableCollection<Message> Messages { get; } = new();

    private string _peerId = "";
    public string PeerId
    {
        get => _peerId;
        set
        {
            _peerId = value;
            // Заголовок страницы: "Чат с A1B2C3D4"
            Title = $"Чат с {ShortenId(value)}";
        }
    }

    public Chat()
    {
        InitializeComponent();
        BindingContext = this;
    }

    private static string ShortenId(string id) =>
        string.IsNullOrEmpty(id) ? "?" :
        id.Length > 8 ? id[..8] : id;

    private void OnSendClicked(object sender, EventArgs e)
    {
        var text = MessageEntry.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        Messages.Add(new Message
        {
            Text = text,
            Time = DateTime.Now.ToString("HH:mm"),
            IsMine = true
        });

        MessageEntry.Text = string.Empty;

        // Прокрутка к последнему сообщению
        Dispatcher.Dispatch(() =>
        {
            if (Messages.Count > 0)
                MessagesView.ScrollTo(Messages.Count - 1,
                                      position: ScrollToPosition.End,
                                      animate: true);
        });

        // TODO: передать сообщение в BleService, чтобы отправить по сети
    }
}

/// <summary>Одно сообщение в чате.</summary>
public class Message
{
    public string Text { get; set; } = "";
    public string Time { get; set; } = "";
    public bool IsMine { get; set; }

    // Свои сообщения — оранжевые (Bonfire), чужие — серые
    public Color BubbleColor => IsMine ? Color.FromArgb("#FF5722") : Color.FromArgb("#E0E0E0");
    public Color TextColor => IsMine ? Colors.White : Colors.Black;

    // Свои — справа, чужие — слева
    public LayoutOptions Alignment => IsMine ? LayoutOptions.End : LayoutOptions.Start;
}