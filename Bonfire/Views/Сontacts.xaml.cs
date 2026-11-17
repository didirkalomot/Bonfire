using System.Collections.ObjectModel;

namespace Bonfire.Views;

public partial class Contacts : ContentPage
{
    public ObservableCollection<PeerItem> Peers { get; } = new();

    public Contacts()
    {
        InitializeComponent();
        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateEmptyLabel();

        // TODO: здесь позже будем загружать из BleService список всех,
        // кого когда-либо слышали (PeerTable из SQLite)
        if (Peers.Count == 0)
        {
            // Временные тестовые данные, чтобы видеть, как выглядит список
            Peers.Add(new PeerItem
            {
                PeerId = "A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6E7F8A9B0C1D2E3F4A5B6C7D8E9F0A1B2",
                DisplayName = "Узел A1B2C3D4",
                StatusText = "недавно"
            });
            Peers.Add(new PeerItem
            {
                PeerId = "1122334455667788990011223344556677889900112233445566778899001122",
                DisplayName = "Узел 11223344",
                StatusText = "5 мин назад"
            });
            UpdateEmptyLabel();
        }
    }

    private void UpdateEmptyLabel()
    {
        EmptyLabel.IsVisible = Peers.Count == 0;
    }

    private async void OnPeerSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not PeerItem peer) return;

        // Снимаем выделение, чтобы можно было вернуться и выбрать снова
        PeersView.SelectedItem = null;

        // Переход на страницу чата с передачей ID через query-параметр
        var encodedId = Uri.EscapeDataString(peer.PeerId);
        await Shell.Current.GoToAsync($"chat?peerId={encodedId}");
    }
}

/// <summary>Отображение одного контакта в списке.</summary>
public class PeerItem
{
    public string PeerId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string StatusText { get; set; } = "";
}