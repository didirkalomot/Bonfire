using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Content;
using Android.OS;
using Microsoft.Maui.ApplicationModel;

namespace Bonfire.Platforms.Android;

public class ExtendedAdvertiser : AdvertisingSetCallback
{
    BluetoothLeAdvertiser? advertiser;
    AdvertisingSet? currentSet;

    static readonly ParcelUuid ServiceUuid16 = ParcelUuid.FromString("0000B0F1-0000-1000-8000-00805F9B34FB")!;

    public bool IsSupported =>
        OperatingSystem.IsAndroidVersionAtLeast(26) &&
        GetAdapter()?.IsLeExtendedAdvertisingSupported == true;

    public int MaxDataLength =>
        OperatingSystem.IsAndroidVersionAtLeast(26)
            ? GetAdapter()?.LeMaximumAdvertisingDataLength ?? 0
            : 0;

    static BluetoothAdapter? GetAdapter()
    {
        var manager = MauiApplication.Current.GetSystemService(Context.BluetoothService) as BluetoothManager;
        return manager?.Adapter;
    }

    public void Start(byte[] payload, ushort manufacturerId = 0xFFFF)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return;

        var adapter = GetAdapter();
        if (adapter is null) return;

        advertiser = adapter.BluetoothLeAdvertiser;
        if (advertiser is null) return;

        var paramBuilder = new AdvertisingSetParameters.Builder();
        paramBuilder.SetLegacyMode(false);
        paramBuilder.SetConnectable(true);                        // Advertising может начать Gatt соединение
        paramBuilder.SetInterval(AdvertisingSetParameters.IntervalMedium);
        paramBuilder.SetTxPowerLevel(AdvertiseTxPower.Medium);
        var parameters = paramBuilder.Build()!;

        var dataBuilder = new AdvertiseData.Builder();
        dataBuilder.AddManufacturerData(manufacturerId, payload);   // AnnouncePacket
        dataBuilder.AddServiceUuid(ServiceUuid16);                  // 16-битный UUID
        dataBuilder.SetIncludeDeviceName(false);                    // не шлём имя
        dataBuilder.SetIncludeTxPowerLevel(false);                  // не шлём TX power
        var data = dataBuilder.Build()!;

        advertiser.StartAdvertisingSet(parameters, data, null, null, null, this);
    }

    public void Stop()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(26) && advertiser is not null) advertiser.StopAdvertisingSet(this);
    }

    public override void OnAdvertisingSetStarted(AdvertisingSet? advertisingSet, int txPower, AdvertiseResult status)
    {
        if (status == AdvertiseResult.Success) currentSet = advertisingSet;
    }

    public override void OnAdvertisingSetStopped(AdvertisingSet? advertisingSet) => currentSet = null;
}