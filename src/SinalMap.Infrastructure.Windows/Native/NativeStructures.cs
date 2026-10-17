using System.Runtime.InteropServices;

namespace SinalMap.Infrastructure.Windows.Native;

internal enum WlanInterfaceState : uint
{
    NotReady, Connected, AdHocNetworkFormed, Disconnecting, Disconnected, Associating, Discovering, Authenticating
}

internal enum WlanOpcode : uint
{
    CurrentConnection = 7,
    ChannelNumber = 8,
    RealtimeConnectionQuality = 19,
    Rssi = 0x10000102
}
internal enum Dot11BssType : uint { Infrastructure = 1, Independent = 2, Any = 3 }

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal unsafe struct Dot11Ssid
{
    public uint Length;
    public fixed byte Bytes[32];

    public readonly byte[] ToBytes()
    {
        if (Length > 32)
            throw NativeWifiException.InvalidData("DOT11_SSID", "SSID length exceeds the native 32-byte buffer.");
        fixed (byte* pointer = Bytes)
            return new ReadOnlySpan<byte>(pointer, (int)Length).ToArray();
    }

    internal static Dot11Ssid FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > 32)
            throw new ArgumentOutOfRangeException(nameof(bytes));
        var result = new Dot11Ssid { Length = (uint)bytes.Length };
        for (var i = 0; i < bytes.Length; i++)
            result.Bytes[i] = bytes[i];
        return result;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal unsafe struct Dot11MacAddress
{
    public fixed byte Bytes[6];

    public readonly string ToText()
    {
        fixed (byte* pointer = Bytes)
            return string.Join(":", new ReadOnlySpan<byte>(pointer, 6).ToArray().Select(b => b.ToString("X2")));
    }

    internal static Dot11MacAddress FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 6)
            throw new ArgumentException("A BSSID has six octets.", nameof(bytes));
        var result = new Dot11MacAddress();
        for (var i = 0; i < 6; i++)
            result.Bytes[i] = bytes[i];
        return result;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Unicode)]
internal unsafe struct WlanInterfaceInfo
{
    public Guid Id;
    public fixed char Description[256];
    public WlanInterfaceState State;

    public readonly string GetDescription()
    {
        fixed (char* pointer = Description)
        {
            var chars = new ReadOnlySpan<char>(pointer, 256);
            var length = chars.IndexOf('\0');
            return new string(length < 0 ? chars : chars[..length]);
        }
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct WlanAssociationAttributes
{
    public Dot11Ssid Ssid;
    public Dot11BssType BssType;
    public Dot11MacAddress Bssid;
    public uint PhyType;
    public uint PhyIndex;
    public uint SignalQuality;
    public uint RxRate;
    public uint TxRate;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct WlanSecurityAttributes
{
    public int SecurityEnabled; // BOOL do Win32 ocupa 4 bytes; o tipo BOOLEAN ocupa 1 byte.
    public int OneXEnabled;
    public uint AuthenticationAlgorithm;
    public uint CipherAlgorithm;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Unicode)]
internal unsafe struct WlanConnectionAttributes
{
    public WlanInterfaceState State;
    public uint ConnectionMode;
    public fixed char ProfileName[256];
    public WlanAssociationAttributes Association;
    public WlanSecurityAttributes Security;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal unsafe struct WlanRateSet
{
    public uint Length;
    public fixed ushort Rates[126];
}

// O cabeçalho tem 24 bytes; os enlaces são uma matriz variável após esse cabeçalho.
[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct WlanRealtimeConnectionQualityHeader
{
    public uint PhyType;
    public uint LinkQuality;
    public uint RxRate;
    public uint TxRate;
    public int IsMloConnection;
    public uint NumberOfLinks;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct WlanRealtimeConnectionQualityLinkInfo
{
    public byte LinkId;
    public uint ChannelCenterFrequencyMhz;
    public uint Bandwidth;
    public int Rssi;
    public WlanRateSet RateSet;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct WlanBssEntry
{
    public Dot11Ssid Ssid;
    public uint PhyId;
    public Dot11MacAddress Bssid;
    public Dot11BssType BssType;
    public uint PhyType;
    public int Rssi;
    public uint LinkQuality;
    public byte InRegDomain; // O tipo BOOLEAN nativo ocupa 1 byte.
    public ushort BeaconPeriod;
    public ulong Timestamp;
    public ulong HostTimestamp;
    public ushort CapabilityInformation;
    public uint CenterFrequencyKHz;
    public WlanRateSet RateSet;
    public uint IeOffset;
    public uint IeSize;
}
