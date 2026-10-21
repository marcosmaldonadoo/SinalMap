using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SinalMap.Infrastructure.Windows;

internal static class WindowsGatewayDiscovery
{
    internal static IReadOnlyDictionary<Guid, string> Read()
    {
        var result = new Dictionary<Guid, string>();
        try
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (!Guid.TryParse(adapter.Id, out var id)) continue;
                try
                {
                    var gateway = Select(adapter.GetIPProperties().GatewayAddresses.Select(g => g.Address));
                    if (gateway is not null) result[id] = gateway;
                }
                catch (NetworkInformationException) { /* A ausência de gateway não impede listar o Wi-Fi. */ }
            }
        }
        catch (NetworkInformationException) { /* Sem sugestão; o destino continua editável. */ }
        return result;
    }

    internal static string? Select(IEnumerable<IPAddress> addresses)
    {
        var candidates = addresses.Where(a => a.AddressFamily == AddressFamily.InterNetwork &&
            !IPAddress.IsLoopback(a) && a.GetAddressBytes()[0] is > 0 and < 224)
            .Select(a => a.ToString()).Distinct().ToArray();
        // Não escolher arbitrariamente entre gateways diferentes do mesmo adaptador.
        return candidates.Length == 1 ? candidates[0] : null;
    }
}
