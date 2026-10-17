using System.Runtime.InteropServices;
using System.Text;
using SinalMap.Infrastructure.Windows.Native;

namespace SinalMap.Tests.Fakes;

internal sealed class FakeWlanApi : IWlanApi
{
    public uint OpenCode { get; set; }
    public uint Version { get; set; } = 2;
    public nint RawHandle { get; set; } = 123;
    public uint EnumCode { get; set; }
    public uint BssCode { get; set; }
    public uint? BssDeclaredSize { get; set; }
    public uint? BssDeclaredCount { get; set; }
    public bool NullQueryPointer { get; set; }
    public HashSet<WlanOpcode> NullQueryOpcodes { get; } = [];
    public int CloseCount;
    public int FreeCount;
    public int AllocationCount;
    public int ConnectionReads;
    public List<WlanOpcode> Queries { get; } = [];
    public Action<WlanOpcode>? BeforeQuery { get; set; }
    public Dictionary<WlanOpcode, uint> QueryErrors { get; } = [];
    public Dictionary<WlanOpcode, uint> DeclaredQuerySizes { get; } = [];
    public WlanConnectionAttributes Connection { get; set; } = Connected();
    public Func<int, WlanConnectionAttributes>? ConnectionAtRead { get; set; }
    public int Rssi { get; set; } = -55;
    public uint Channel { get; set; } = 6;
    public int RealtimeIsMlo { get; set; }
    public uint? RealtimeDeclaredLinkCount { get; set; }
    public WlanRealtimeConnectionQualityLinkInfo[] RealtimeLinks { get; set; } = [new() { Rssi = -64 }];
    public WlanInterfaceInfo[] Interfaces { get; set; } =
        [new() { Id = TestData.InterfaceId, State = WlanInterfaceState.Connected }];
    public WlanBssEntry[] BssEntries { get; set; } = [Bss()];
    public TaskCompletionSource Closed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public uint OpenHandle(out uint negotiatedVersion, out nint handle)
    {
        negotiatedVersion = Version;
        handle = RawHandle;
        return OpenCode;
    }
    public uint CloseHandle(nint handle)
    {
        Interlocked.Increment(ref CloseCount);
        Closed.TrySetResult();
        return 0;
    }
    public void FreeMemory(nint pointer)
    {
        Marshal.FreeHGlobal(pointer);
        Interlocked.Increment(ref FreeCount);
    }
    public uint EnumerateInterfaces(SafeWlanHandle handle, out nint pointer)
    {
        pointer = AllocateList(Interfaces, (uint)(8 + Marshal.SizeOf<WlanInterfaceInfo>() * Interfaces.Length),
            (uint)Interfaces.Length, bss: false);
        return EnumCode;
    }
    public uint QueryInterface(SafeWlanHandle handle, Guid id, WlanOpcode opcode, out uint size, out nint pointer)
    {
        BeforeQuery?.Invoke(opcode);
        Queries.Add(opcode);
        switch (opcode)
        {
            case WlanOpcode.CurrentConnection:
                pointer = Allocate(ConnectionAtRead?.Invoke(ConnectionReads++) ?? Connection, out size);
                break;
            case WlanOpcode.Rssi: pointer = Allocate(Rssi, out size); break;
            case WlanOpcode.ChannelNumber: pointer = Allocate(Channel, out size); break;
            case WlanOpcode.RealtimeConnectionQuality: pointer = AllocateRealtimeQuality(out size); break;
            default: throw new ArgumentOutOfRangeException(nameof(opcode));
        }
        if (NullQueryPointer || NullQueryOpcodes.Contains(opcode)) { FreeMemory(pointer); pointer = 0; }
        if (DeclaredQuerySizes.TryGetValue(opcode, out var declared)) size = declared;
        // A consulta alternativa só tem suporte nos cenários que o habilitam explicitamente.
        return QueryErrors.GetValueOrDefault(opcode, opcode == WlanOpcode.RealtimeConnectionQuality ? 50u : 0u);
    }
    public uint GetBssList(SafeWlanHandle handle, Guid id, ref Dot11Ssid ssid,
        Dot11BssType bssType, bool securityEnabled, out nint pointer)
    {
        Assert.Equal(Dot11BssType.Infrastructure, bssType);
        Assert.True(securityEnabled);
        pointer = AllocateList(BssEntries,
            BssDeclaredSize ?? (uint)(8 + Marshal.SizeOf<WlanBssEntry>() * BssEntries.Length),
            BssDeclaredCount ?? (uint)BssEntries.Length, bss: true);
        return BssCode;
    }
    private nint Allocate<T>(T value, out uint size) where T : unmanaged
    {
        size = (uint)Marshal.SizeOf<T>();
        var pointer = Marshal.AllocHGlobal((int)size);
        Interlocked.Increment(ref AllocationCount);
        Marshal.StructureToPtr(value, pointer, false);
        return pointer;
    }
    private nint AllocateRealtimeQuality(out uint size)
    {
        var headerSize = Marshal.SizeOf<WlanRealtimeConnectionQualityHeader>();
        var linkSize = Marshal.SizeOf<WlanRealtimeConnectionQualityLinkInfo>();
        size = (uint)(headerSize + linkSize * RealtimeLinks.Length);
        var pointer = Marshal.AllocHGlobal((int)size);
        Interlocked.Increment(ref AllocationCount);
        var header = new WlanRealtimeConnectionQualityHeader
        {
            PhyType = 7,
            LinkQuality = 90,
            IsMloConnection = RealtimeIsMlo,
            NumberOfLinks = RealtimeDeclaredLinkCount ?? (uint)RealtimeLinks.Length
        };
        Marshal.StructureToPtr(header, pointer, false);
        for (var i = 0; i < RealtimeLinks.Length; i++)
            Marshal.StructureToPtr(RealtimeLinks[i], pointer + headerSize + i * linkSize, false);
        return pointer;
    }
    private nint AllocateList<T>(T[] values, uint declaredSize, uint count, bool bss) where T : unmanaged
    {
        var size = Marshal.SizeOf<T>();
        var pointer = Marshal.AllocHGlobal(8 + size * values.Length);
        Interlocked.Increment(ref AllocationCount);
        Marshal.WriteInt32(pointer, unchecked((int)(bss ? declaredSize : count)));
        Marshal.WriteInt32(pointer, 4, bss ? unchecked((int)count) : 0);
        for (var i = 0; i < values.Length; i++)
            Marshal.StructureToPtr(values[i], pointer + 8 + i * size, false);
        return pointer;
    }

    public static WlanConnectionAttributes Connected(string bssid = "020000000001", byte[]? ssid = null) => new()
    {
        State = WlanInterfaceState.Connected,
        Association = new()
        {
            Ssid = Dot11Ssid.FromBytes(ssid ?? Encoding.UTF8.GetBytes("test")),
            Bssid = Dot11MacAddress.FromBytes(Convert.FromHexString(bssid)),
            BssType = Dot11BssType.Infrastructure,
            PhyType = 7,
            SignalQuality = 90
        },
        Security = new() { SecurityEnabled = 1 }
    };
    public static WlanBssEntry Bss(string bssid = "020000000001", uint frequency = 2437000) => new()
    {
        Ssid = Dot11Ssid.FromBytes(Encoding.UTF8.GetBytes("test")),
        Bssid = Dot11MacAddress.FromBytes(Convert.FromHexString(bssid)),
        BssType = Dot11BssType.Infrastructure,
        PhyType = 7,
        Rssi = -99, // O RSSI do cache BSS não deve substituir o RSSI atual da conexão.
        CenterFrequencyKHz = frequency,
        HostTimestamp = 134116992000000000
    };
}
