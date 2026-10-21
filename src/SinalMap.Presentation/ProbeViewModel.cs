using System.Net;
using System.Text.Json;
using System.Threading.Channels;
using SinalMap.Application;
using SinalMap.Domain;

namespace SinalMap.Presentation;

public enum ProbeOperation { Idle, Listing, Collecting, Cancelling, Saving, Opening }

/// <summary>Operações iniciadas na thread da interface; as continuações preservam seu contexto.</summary>
public sealed class ProbeViewModel : ObservableObject
{
    private readonly IWifiMonitor wifi;
    private readonly ProbeRunner runner;
    private readonly ISessionStore store;
    private readonly Func<string, CancellationToken, Task<ProbeSession>> read;
    private readonly IProbeDialogs dialogs;
    private readonly Func<Exception, string>? describeError;
    private Task activeOperation = Task.CompletedTask;
    private CancellationTokenSource? operationCancellation;
    private ProbeSession? pendingSession;
    private bool closing;
    private ProbeOperation operation;
    private string status = "Atualize as interfaces para começar, ou abra um JSON salvo.";
    private string error = "";
    private string targetText = "";
    private bool targetEdited;
    private bool targetFromGateway;
    private string outputFolder;
    private string savedPath = "";
    private WifiInterfaceInfo? selectedInterface;
    private IReadOnlyList<WifiInterfaceInfo> interfaces = [];
    private SessionResultViewModel? result;
    private int selectedTabIndex;
    private bool monitoringRequested;
    private CancellationTokenSource? monitoringCancellation;
    private Task monitoringTask = Task.CompletedTask;
    private readonly IProbeClock clock;
    private readonly IWifiSignalRefresher? signalRefresher;

    public ProbeViewModel(IWifiMonitor wifi, IIcmpProbe icmp, IProbeClock clock, ISessionStore store,
        Func<string, CancellationToken, Task<ProbeSession>> read, IProbeDialogs dialogs,
        string outputFolder, Func<Exception, string>? describeError = null, IWifiSignalRefresher? signalRefresher = null)
    {
        this.wifi = wifi;
        this.clock = clock;
        this.signalRefresher = signalRefresher;
        runner = new(wifi, icmp, clock);
        this.store = store;
        this.read = read;
        this.dialogs = dialogs;
        this.outputFolder = outputFolder;
        this.describeError = describeError;
        RefreshCommand = new(RefreshAsync, ShowError, () => CanReplaceSession);
        MonitorCommand = new(() => { StartMonitoring(); return Task.CompletedTask; }, ShowError,
            () => CanReplaceSession && SelectedInterface?.State == WifiConnectionState.Connected);
        StartCommand = new(StartAsync, ShowError, () => CanReplaceSession && SelectedInterface?.State == WifiConnectionState.Connected);
        OpenCommand = new(() => { var path = dialogs.ChooseSessionFile(); return path is null ? Task.CompletedTask : OpenAsync(path); }, ShowError, () => CanReplaceSession);
        ChooseFolderCommand = new(() => { OutputFolder = dialogs.ChooseOutputFolder(OutputFolder) ?? OutputFolder; return Task.CompletedTask; }, ShowError, () => CanEditFolder);
        CancelCommand = new(() => { Cancel(); return Task.CompletedTask; }, ShowError, () => Operation == ProbeOperation.Collecting && !closing);
        RetrySaveCommand = new(RetrySaveAsync, ShowError, () => !IsBusy && HasPendingSave && !closing);
        DiscardCommand = new(() =>
        {
            if (dialogs.ConfirmDiscard())
            {
                pendingSession = null;
                Result = null;
                Signal = new();
                Notify(nameof(Signal));
                SavedPath = "";
                Status = "Sessão em memória descartada por solicitação do usuário.";
                Error = "";
                RefreshState();
            }
            return Task.CompletedTask;
        }, ShowError, () => !IsBusy && HasPendingSave && !closing);
    }

    public AsyncCommand RefreshCommand { get; }
    public AsyncCommand MonitorCommand { get; }
    public AsyncCommand StartCommand { get; }
    public AsyncCommand OpenCommand { get; }
    public AsyncCommand ChooseFolderCommand { get; }
    public AsyncCommand CancelCommand { get; }
    public AsyncCommand RetrySaveCommand { get; }
    public AsyncCommand DiscardCommand { get; }
    public SignalHistoryViewModel Signal { get; private set; } = new();
    public int SelectedTabIndex { get => selectedTabIndex; set => Set(ref selectedTabIndex, value); }
    public bool HasResult => Result is not null;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public bool HasSavedPath => !string.IsNullOrWhiteSpace(SavedPath);
    public string OperationLabel => Operation switch
    {
        ProbeOperation.Listing => "ATUALIZANDO", ProbeOperation.Collecting => "MEDINDO",
        ProbeOperation.Cancelling => "CANCELANDO", ProbeOperation.Saving => "SALVANDO", ProbeOperation.Opening => "ABRINDO",
        _ => HasPendingSave ? "NÃO SALVO" : HasError ? "VERIFICAR" : HasResult ? "RESULTADO DISPONÍVEL" : "PRONTO PARA MEDIR"
    };
    public ProbeOperation Operation { get => operation; private set { Set(ref operation, value); RefreshState(); } }
    public bool IsBusy => Operation != ProbeOperation.Idle;
    public bool HasPendingSave => pendingSession is not null;
    public bool CanReplaceSession => !IsBusy && !HasPendingSave && !closing;
    public bool CanEditFolder => !IsBusy && !closing;
    public string Status { get => status; private set => Set(ref status, value); }
    public string Error { get => error; private set { Set(ref error, value); Notify(nameof(HasError)); Notify(nameof(OperationLabel)); } }
    public string TargetText
    {
        get => targetText;
        set
        {
            if (!Set(ref targetText, value)) return;
            targetEdited = true;
            targetFromGateway = false;
            Notify(nameof(TargetHint));
        }
    }
    public string TargetHint => targetFromGateway ? "Gateway desta interface preenchido pelo Windows. Você pode editar." :
        "Informe um IPv4 autorizado. O gateway não será escolhido entre vários candidatos.";
    public string OutputFolder { get => outputFolder; set => Set(ref outputFolder, value); }
    public string SavedPath { get => savedPath; private set { Set(ref savedPath, value); Notify(nameof(HasSavedPath)); } }
    public WifiInterfaceInfo? SelectedInterface
    {
        get => selectedInterface;
        set
        {
            if (!Set(ref selectedInterface, value)) return;
            if (!targetEdited)
            {
                targetFromGateway = value is { State: WifiConnectionState.Connected, Ipv4Gateway: not null };
                Set(ref targetText, targetFromGateway ? value!.Ipv4Gateway! : "", nameof(TargetText));
                Notify(nameof(TargetHint));
            }
            StartCommand.NotifyCanExecuteChanged();
            MonitorCommand.NotifyCanExecuteChanged();
            RestartMonitoring();
        }
    }
    public IReadOnlyList<WifiInterfaceInfo> Interfaces { get => interfaces; private set => Set(ref interfaces, value); }
    public SessionResultViewModel? Result { get => result; private set { Set(ref result, value); Notify(nameof(HasResult)); } }

    public Task RefreshAsync()
    {
        if (!CanReplaceSession) return Task.CompletedTask;
        return RunOperationAsync(ProbeOperation.Listing, async token =>
        {
            Status = "Consultando interfaces Wi-Fi…";
            var previous = SelectedInterface?.Id;
            Interfaces = await wifi.ListInterfacesAsync(token);
            var connected = Interfaces.Where(i => i.State == WifiConnectionState.Connected).ToArray();
            SelectedInterface = connected.SingleOrDefault(i => i.Id == previous) ?? (connected.Length == 1 ? connected[0] : null);
            Status = connected.Length switch
            {
                0 => "Nenhuma interface Wi-Fi conectada. Conecte pelo Windows e atualize a lista.",
                1 => "Interface conectada selecionada. Confira o destino IPv4 e a pasta antes de iniciar.",
                _ => "Selecione explicitamente uma das interfaces Wi-Fi conectadas."
            };
        });
    }

    public Task StartAsync()
    {
        if (!CanReplaceSession) return Task.CompletedTask;
        return RunOperationAsync(ProbeOperation.Collecting, async token =>
        {
            var selected = SelectedInterface;
            if (selected is null || selected.State != WifiConnectionState.Connected || selected.Id == Guid.Empty)
                throw new ArgumentException("Selecione uma interface Wi-Fi conectada.");
            var text = TargetText.Trim();
            if (!IPAddress.TryParse(text, out var target) || target.ToString() != text)
                throw new ArgumentException("Informe um IPv4 decimal completo, sem DNS, abreviações ou formato octal.");
            var options = new ProbeOptions(target, selected.Id, selected.Description);
            try { options.Validate(); }
            catch (ArgumentException) { throw new ArgumentException("Use um IPv4 unicast; loopback, multicast, broadcast e endereço zero não são aceitos."); }
            ValidateOutputFolder();
            monitoringRequested = false;
            Result = null;
            SavedPath = "";
            SelectedTabIndex = 0;
            Signal.Begin();
            Status = signalRefresher is null ?
                "Medindo por 60 segundos. O sinal em dBm é atualizado a cada leitura concluída; cancelar salva a sessão parcial." :
                "Medindo por 60 segundos com varredura Wi-Fi a cada 5 s. A varredura pode aumentar o RTT; isso será registrado no JSON.";
            var updates = Channel.CreateUnbounded<ProbeSample>(new UnboundedChannelOptions
            {
                SingleReader = true, SingleWriter = false, AllowSynchronousContinuations = false
            });
            var display = ReadSignalAsync(updates.Reader);
            var scans = new List<WifiScanObservation>();
            using var refreshCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            var refresh = RefreshSignalLoopAsync(selected.Id, refreshCancellation.Token, scans);
            // O coletor mantém a agenda e o controle de rota existentes, fora da thread gráfica.
            try { pendingSession = await Task.Run(() => runner.RunAsync(options, token, signalUpdates: updates.Writer)); }
            finally
            {
                refreshCancellation.Cancel();
                await refresh;
                updates.Writer.TryComplete();
                await display;
                Signal.End();
            }
            if (signalRefresher is not null)
                pendingSession = pendingSession with { ActiveWifiScanning = true, WifiScans = scans.ToArray(), CompletedAt = clock.Capture() };
            Signal.Load(pendingSession);
            RefreshState();
            Result = new(pendingSession);
            await SavePendingCoreAsync();
        });
    }

    public Task OpenAsync(string path)
    {
        if (!CanReplaceSession) return Task.CompletedTask;
        return RunOperationAsync(ProbeOperation.Opening, async token =>
        {
            Status = "Abrindo e analisando o JSON local…";
            var session = await read(path, token);
            var loaded = new SessionResultViewModel(session);
            Result = loaded;
            monitoringRequested = false;
            Signal.Load(session);
            SelectedTabIndex = 0;
            SavedPath = Path.GetFullPath(path);
            Status = "JSON aberto para análise. O arquivo original foi preservado.";
        });
    }

    public Task RetrySaveAsync()
    {
        if (IsBusy || !HasPendingSave || closing) return Task.CompletedTask;
        return RunOperationAsync(ProbeOperation.Saving, _ => SavePendingCoreAsync());
    }

    private async Task ReadSignalAsync(ChannelReader<ProbeSample> reader)
    {
        // Retomar na thread gráfica; o produtor usa TryWrite e nunca espera a renderização.
        await foreach (var sample in reader.ReadAllAsync()) Signal.Accept(sample);
    }

    public void StartMonitoring()
    {
        if (!CanReplaceSession) return;
        monitoringRequested = true;
        Result = null;
        SavedPath = "";
        SelectedTabIndex = 0;
        Status = "Sinal da interface selecionada, sem ICMP e sem gravação. Inicie uma medição para salvar uma sessão de 60 s.";
        RestartMonitoring();
    }

    private void RestartMonitoring()
    {
        if (!monitoringRequested || !CanReplaceSession) return;
        monitoringCancellation?.Cancel();
        Signal.BeginPreview();
        if (SelectedInterface is not { State: WifiConnectionState.Connected } selected) return;
        var cancellation = new CancellationTokenSource();
        monitoringCancellation = cancellation;
        monitoringTask = MonitorAfterAsync(monitoringTask, selected.Id, cancellation);
    }

    private async Task MonitorAfterAsync(Task previous, Guid id, CancellationTokenSource cancellation)
    {
        using (cancellation)
        {
            var token = cancellation.Token;
            Task refreshTask = Task.CompletedTask;
            try
            {
                await previous;
                token.ThrowIfCancellationRequested();
                refreshTask = RefreshSignalLoopAsync(id, token);
                var began = clock.Capture();
                while (true)
                {
                    var started = clock.Capture();
                    WifiSnapshot snapshot;
                    try { snapshot = await wifi.ReadAsync(id, token); }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                        snapshot = WifiSnapshot.Unavailable(id, started, clock.Capture(),
                            new(IssueKind.AdapterError, "Wi-Fi", "Não foi possível consultar o adaptador."));
                    }
                    token.ThrowIfCancellationRequested();
                    Signal.AcceptPreview(snapshot, clock.ElapsedSince(began.MonotonicTicks));
                    var remaining = ProbeOptions.Interval - clock.ElapsedSince(started.MonotonicTicks);
                    await Task.Delay(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero, token);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            finally
            {
                await refreshTask;
                if (ReferenceEquals(monitoringCancellation, cancellation)) monitoringCancellation = null;
            }
        }
    }

    private async Task RefreshSignalLoopAsync(Guid id, CancellationToken token, List<WifiScanObservation>? scans = null)
    {
        if (signalRefresher is null) return;
        try
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                var started = clock.Capture();
                Signal.DescribeRefresh("Atualizando o sinal por varredura Wi-Fi… Pode elevar a latência.");
                var result = await signalRefresher.RefreshAsync(id, token);
                scans?.Add(result);
                token.ThrowIfCancellationRequested();
                Signal.DescribeRefresh(result.Completed ?
                    $"Varredura confirmada às {result.CompletedAt.Utc.ToLocalTime():HH:mm:ss} · atualização a cada 5 s" :
                    $"Varredura não confirmada: {result.Issue?.Kind} (código {result.Issue?.NativeCode?.ToString() ?? "n/a"}). O RSSI pode estar desatualizado.");
                var remaining = TimeSpan.FromSeconds(5) - clock.ElapsedSince(started.MonotonicTicks);
                await Task.Delay(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (!token.IsCancellationRequested)
                Signal.DescribeRefresh("Não foi possível atualizar por varredura. O RSSI pode estar desatualizado.");
        }
    }

    public void Cancel()
    {
        if (Operation != ProbeOperation.Collecting) return;
        Operation = ProbeOperation.Cancelling;
        Status = "Cancelamento solicitado. Aguardando operações pendentes para salvar a sessão parcial…";
        operationCancellation?.Cancel();
    }

    public async Task<bool> RequestCloseAsync()
    {
        closing = true;
        monitoringCancellation?.Cancel();
        RefreshState();
        if (Operation == ProbeOperation.Collecting) Cancel();
        else operationCancellation?.Cancel();
        await activeOperation;
        await monitoringTask;
        if (!HasPendingSave) return true;
        closing = false;
        Status = "A janela permanece aberta: há uma sessão não salva. Escolha uma pasta e tente salvar novamente, ou descarte explicitamente.";
        RefreshState();
        return false;
    }

    private Task RunOperationAsync(ProbeOperation next, Func<CancellationToken, Task> action)
    {
        if (IsBusy || closing) return Task.CompletedTask;
        Operation = next;
        monitoringCancellation?.Cancel();
        Error = "";
        activeOperation = CompleteAsync(action);
        return activeOperation;
    }

    private async Task CompleteAsync(Func<CancellationToken, Task> action)
    {
        using var cancellation = new CancellationTokenSource();
        operationCancellation = cancellation;
        try
        {
            if (!monitoringTask.IsCompleted) Status = "Aguardando a varredura em andamento antes de continuar…";
            await monitoringTask;
            await action(cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { Status = "Operação cancelada."; }
        catch (Exception ex) { ShowError(ex); }
        finally
        {
            operationCancellation = null;
            Operation = ProbeOperation.Idle;
            RestartMonitoring();
        }
    }

    private async Task SavePendingCoreAsync()
    {
        var session = pendingSession ?? throw new InvalidOperationException("Não há sessão pendente.");
        Operation = ProbeOperation.Saving;
        Status = "Salvando o JSON local. Aguarde antes de fechar…";
        var folder = ValidateOutputFolder();
        var path = Path.Combine(folder, $"sinalmap-{session.StartedAt.Utc:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
        // A persistência é independente do cancelamento da aquisição e não sobrescreve arquivos.
        await Task.Run(() => store.SaveAsync(session, path, CancellationToken.None));
        pendingSession = null;
        SavedPath = path;
        Status = session.Cancelled ? "Sessão parcial salva após cancelamento." : "Coleta concluída e salva. Examine as métricas e ocorrências abaixo.";
        RefreshState();
    }

    private string ValidateOutputFolder()
    {
        if (string.IsNullOrWhiteSpace(OutputFolder)) throw new ArgumentException("Informe uma pasta local de resultados.");
        return Path.GetFullPath(OutputFolder);
    }

    private void ShowError(Exception ex)
    {
        Error = ex switch
        {
            ArgumentException => ex.Message,
            UnauthorizedAccessException => "Acesso negado. Confira as permissões da pasta ou do arquivo.",
            InvalidDataException or JsonException => "JSON inválido, inconsistente ou de versão não suportada.",
            IOException => "Não foi possível ler ou salvar o arquivo. Confira a pasta, o espaço e as permissões.",
            TimeoutException => "A consulta excedeu o tempo de espera. Confira o Wi-Fi e tente atualizar novamente.",
            _ => describeError?.Invoke(ex) ?? "A operação falhou. Confira a interface Wi-Fi e tente novamente."
        };
        Status = HasPendingSave ? "Falha ao salvar. Os dados continuam em memória; escolha uma pasta e tente salvar novamente." : "A operação não foi concluída.";
    }

    private void RefreshState()
    {
        Notify(nameof(IsBusy)); Notify(nameof(HasPendingSave)); Notify(nameof(CanReplaceSession)); Notify(nameof(CanEditFolder));
        Notify(nameof(OperationLabel));
        RefreshCommand?.NotifyCanExecuteChanged(); StartCommand?.NotifyCanExecuteChanged(); OpenCommand?.NotifyCanExecuteChanged();
        MonitorCommand?.NotifyCanExecuteChanged();
        ChooseFolderCommand?.NotifyCanExecuteChanged(); CancelCommand?.NotifyCanExecuteChanged(); RetrySaveCommand?.NotifyCanExecuteChanged();
        DiscardCommand?.NotifyCanExecuteChanged();
    }
}
