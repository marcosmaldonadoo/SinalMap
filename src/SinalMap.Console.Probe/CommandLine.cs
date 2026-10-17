using System.Net;
using SinalMap.Application;

namespace SinalMap.Console.Probe;

internal sealed record CommandLine(bool Help, bool List, IPAddress? Target, Guid? InterfaceId, string? Output)
{
    internal static CommandLine Parse(string[] args)
    {
        if (args.Length == 0 || args is ["--help"] or ["-h"])
            return new(true, false, null, null, null);
        if (args is ["--list"])
            return new(false, true, null, null, null);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is not ("--target" or "--interface" or "--output") || i + 1 >= args.Length ||
                args[i + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Opção inválida ou sem valor: {args[i]}");
            if (!values.TryAdd(args[i], args[++i]))
                throw new ArgumentException("Uma opção foi informada mais de uma vez.");
        }
        if (!values.TryGetValue("--target", out var targetText) || !IPAddress.TryParse(targetText, out var target) ||
            !string.Equals(target.ToString(), targetText, StringComparison.Ordinal))
            throw new ArgumentException("Informe --target com um IPv4 em formato decimal completo (ex.: 192.168.10.1). Nomes DNS, formatos abreviados e octais não são aceitos.");
        Guid? interfaceId = null;
        if (values.TryGetValue("--interface", out var guidText))
            interfaceId = Guid.TryParse(guidText, out var parsed) && parsed != Guid.Empty
                ? parsed : throw new ArgumentException("--interface exige um GUID válido e não vazio.");
        // Validar as restrições do IP mesmo quando a interface só será selecionada depois.
        new ProbeOptions(target, interfaceId ?? Guid.Parse("00000000-0000-0000-0000-000000000001")).Validate();
        var output = values.GetValueOrDefault("--output");
        if (output is not null && string.IsNullOrWhiteSpace(output))
            throw new ArgumentException("--output não pode ser vazio.");
        return new(false, false, target, interfaceId, output);
    }
}
