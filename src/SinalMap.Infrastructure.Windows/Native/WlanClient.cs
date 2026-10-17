using System.Runtime.InteropServices;

namespace SinalMap.Infrastructure.Windows.Native;

internal sealed class WlanClient : IDisposable
{
    private readonly IWlanApi api;
    private readonly SafeWlanHandle handle;

    internal WlanClient(IWlanApi api)
    {
        this.api = api;
        var code = api.OpenHandle(out var version, out var rawHandle);
        handle = new(rawHandle, api.CloseHandle);
        if (code != 0 || handle.IsInvalid || version < 2)
        {
            handle.Dispose();
            if (code != 0)
                throw new NativeWifiException(code, "WlanOpenHandle");
            throw NativeWifiException.InvalidData("WlanOpenHandle", "Invalid handle or incompatible negotiated API version.");
        }
    }

    internal IReadOnlyList<WlanInterfaceInfo> EnumerateInterfaces()
    {
        var code = api.EnumerateInterfaces(handle, out var pointer);
        using var memory = new SafeWlanMemory(pointer, api.FreeMemory);
        Check(code, memory, "WlanEnumInterfaces");
        var count = unchecked((uint)Marshal.ReadInt32(pointer));
        // Esta API não informa o tamanho total. Limitar a contagem do contrato nativo antes de calcular offsets.
        if (count > 4096)
            throw NativeWifiException.InvalidData("WlanEnumInterfaces", "Unreasonable interface count.");
        var size = Marshal.SizeOf<WlanInterfaceInfo>();
        var result = new List<WlanInterfaceInfo>((int)count);
        for (var i = 0; i < count; i++)
            result.Add(Marshal.PtrToStructure<WlanInterfaceInfo>(pointer + checked(8 + i * size)));
        return result;
    }

    internal T Query<T>(Guid id, WlanOpcode opcode) where T : unmanaged
    {
        var code = api.QueryInterface(handle, id, opcode, out var size, out var pointer);
        using var memory = new SafeWlanMemory(pointer, api.FreeMemory);
        Check(code, memory, $"WlanQueryInterface:{opcode}");
        if (size < Marshal.SizeOf<T>())
            throw NativeWifiException.InvalidData($"WlanQueryInterface:{opcode}", "Truncated native response.");
        return Marshal.PtrToStructure<T>(pointer);
    }

    internal int QuerySingleLinkRealtimeRssi(Guid id)
    {
        const string operation = "WlanQueryInterface:RealtimeConnectionQuality";
        var code = api.QueryInterface(handle, id, WlanOpcode.RealtimeConnectionQuality, out var size, out var pointer);
        using var memory = new SafeWlanMemory(pointer, api.FreeMemory);
        Check(code, memory, operation);

        var headerSize = Marshal.SizeOf<WlanRealtimeConnectionQualityHeader>();
        var linkSize = Marshal.SizeOf<WlanRealtimeConnectionQualityLinkInfo>();
        if (size < headerSize)
            throw NativeWifiException.InvalidData(operation, "Cabeçalho da qualidade em tempo real truncado.");
        var header = Marshal.PtrToStructure<WlanRealtimeConnectionQualityHeader>(pointer);
        if (header.NumberOfLinks == 0 || header.NumberOfLinks > (size - headerSize) / linkSize)
            throw NativeWifiException.InvalidData(operation, "A quantidade de enlaces não cabe no buffer retornado.");

        // A Fase 0 não escolhe o enlace mais forte nem calcula uma média para representar o BSSID.
        if (header.IsMloConnection != 0 || header.NumberOfLinks != 1)
            throw NativeWifiException.NotSupported(operation,
                "A alternativa de RSSI exige uma conexão não MLO com um único enlace; o valor permanece indisponível.");
        return Marshal.PtrToStructure<WlanRealtimeConnectionQualityLinkInfo>(pointer + headerSize).Rssi;
    }

    internal WlanBssEntry? FindConnectedBss(Guid id, WlanConnectionAttributes connection)
    {
        var ssid = connection.Association.Ssid;
        _ = ssid.ToBytes(); // Validar a estrutura antes de passá-la novamente ao código nativo.
        var code = api.GetBssList(handle, id, ref ssid, connection.Association.BssType,
            connection.Security.SecurityEnabled != 0, out var pointer);
        using var memory = new SafeWlanMemory(pointer, api.FreeMemory);
        Check(code, memory, "WlanGetNetworkBssList");
        var totalSize = unchecked((uint)Marshal.ReadInt32(pointer));
        var count = unchecked((uint)Marshal.ReadInt32(pointer, 4));
        var entrySize = Marshal.SizeOf<WlanBssEntry>();
        if (totalSize < 8 || count > (totalSize - 8) / entrySize || count > 65536)
            throw NativeWifiException.InvalidData("WlanGetNetworkBssList", "BSS count exceeds the returned buffer.");
        var expectedBssid = connection.Association.Bssid.ToText();
        var expectedSsid = connection.Association.Ssid.ToBytes();
        WlanBssEntry? match = null;
        for (var i = 0; i < count; i++)
        {
            var entry = Marshal.PtrToStructure<WlanBssEntry>(pointer + checked(8 + i * entrySize));
            // A Fase 0 não interpreta elementos de informação variáveis cuja origem não é confiável.
            if (entry.Bssid.ToText() == expectedBssid &&
                entry.BssType == connection.Association.BssType &&
                entry.Ssid.ToBytes().AsSpan().SequenceEqual(expectedSsid) &&
                (match is null || entry.HostTimestamp > match.Value.HostTimestamp))
                match = entry;
        }
        return match;
    }

    private static void Check(uint code, SafeWlanMemory memory, string operation)
    {
        if (code != 0)
            throw new NativeWifiException(code, operation);
        if (memory.IsInvalid)
            throw NativeWifiException.InvalidData(operation, "Native API returned a null buffer on success.");
    }

    public void Dispose() => handle.Dispose();
}
