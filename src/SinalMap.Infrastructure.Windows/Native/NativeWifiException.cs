using System.ComponentModel;
using SinalMap.Domain;

namespace SinalMap.Infrastructure.Windows.Native;

public sealed class NativeWifiException : Exception
{
    public ProbeIssue Issue { get; }

    internal NativeWifiException(uint code, string operation) : this(ToIssue(code, operation)) { }
    internal NativeWifiException(ProbeIssue issue) : base(issue.Message) => Issue = issue;

    internal static NativeWifiException InvalidData(string operation, string message) =>
        new(new ProbeIssue(IssueKind.InvalidData, operation, message));

    internal static NativeWifiException NotSupported(string operation, string message) =>
        new(new ProbeIssue(IssueKind.NotSupported, operation, message));

    private static ProbeIssue ToIssue(uint code, string operation)
    {
        var kind = code switch
        {
            5 => IssueKind.PermissionDenied,
            50 => IssueKind.NotSupported,
            1062 => IssueKind.ServiceUnavailable,
            5023 => IssueKind.NotConnected,
            1168 => IssueKind.InterfaceMissing,
            _ => IssueKind.NativeError
        };
        var message = new Win32Exception(unchecked((int)code)).Message;
        if (code == 27 && operation == "WlanQueryInterface:Rssi")
            message += " Retorno da consulta Wi-Fi de RSSI. A causa não foi determinada; esta mensagem isolada não comprova falha de disco.";
        if (code == 5)
            message += " Check Windows Settings > Privacy & security > Location and desktop app access. Administrator rights do not replace location consent.";
        return new(kind, operation, message, code);
    }
}
