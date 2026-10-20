using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using SinalMap.Domain;

namespace SinalMap.Application;

// Análise e comparação usam os mesmos critérios de reconhecimento dos valores registrados.
internal static class RecordedContextValues
{
    internal static WifiSnapshot[] Connected(ProbeSession session) => session.Samples
        .Where(s => s.Wifi?.State == WifiConnectionState.Connected).Select(s => s.Wifi!).ToArray();

    internal static string? NormalizeGuid(Guid value) => value == Guid.Empty ? null : value.ToString("D");

    internal static string? NormalizeSsid(string? hex)
    {
        if (string.IsNullOrEmpty(hex) || hex.Length > 64 || hex.Length % 2 != 0) return null;
        try { return Convert.ToHexString(Convert.FromHexString(hex)); }
        catch (FormatException) { return null; }
    }

    internal static string? NormalizeBssid(string? text)
    {
        if (!PhysicalAddress.TryParse(text, out var address)) return null;
        var bytes = address.GetAddressBytes();
        return bytes.Length == 6 && bytes.Any(b => b != 0) ? Convert.ToHexString(bytes) : null;
    }

    internal static string? NormalizeTarget(string? text) =>
        IPAddress.TryParse(text, out var address) && address.AddressFamily == AddressFamily.InterNetwork && address.ToString() == text
            ? address.ToString() : null;

    internal static string? Channel(WifiSnapshot wifi) => wifi.Channel is > 0 &&
        wifi.ChannelSource == "WlanQueryInterface:channel_number" ? wifi.Channel.Value.ToString(CultureInfo.InvariantCulture) : null;

    internal static string? Frequency(WifiSnapshot wifi) => wifi.FrequencyKHz is > 0 &&
        wifi.FrequencySource == "WlanGetNetworkBssList:ulChCenterFrequency (cached)" ? wifi.FrequencyKHz.Value.ToString(CultureInfo.InvariantCulture) : null;

    internal static string? RssiSource(WifiSnapshot wifi) => wifi.RssiSource is "WlanQueryInterface:rssi" or
        "WlanQueryInterface:realtime_connection_quality.linksInfo[0].lRssi" ? wifi.RssiSource : null;
}
