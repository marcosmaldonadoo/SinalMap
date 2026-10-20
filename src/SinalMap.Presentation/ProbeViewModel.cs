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
    private string outputFolder;
    private string savedPath = "";
    private WifiInterfaceInfo? selectedInterface;
    private IReadOnlyList<WifiInterfaceInfo> interfaces = [];
    private SessionResultViewModel? result;
    private int selectedTabIndex;

    public ProbeViewModel(IWifiMonitor wifi, IIcmpProbe icmp, IProbeClock clock, ISessionStore store,
        Func<string, CancellationToken, Task<ProbeSession>> read, IProbeDialogs dialogs,
        string outputFolder, Func<Exception, string>? describeError = null)
    {
        this.wifi = wifi;
        runner = new(wifi, icmp, clock);
        this.store = store;
        this.read = read;
        this.dialogs = dialogs;
        this.outputFolder = outputFolder;
        this.describeError = describeError;
        RefreshCommand = new(RefreshAsync, ShowError, () => CanReplaceSession);
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
    public AsyncCommand StartCommand { get; }
    public AsyncCommand OpenCommand { get; }
    public AsyncCommand ChooseFolderCommand { get; }
    public AsyncCommand CancelCommand { get; }
    public AsyncCommand RetrySaveCommand { get; }
    public AsyncCommand DiscardCommand { get; }
    public SignalHistoryViewModel Signal { get; private set; } = new();
    public int SelectedTabIndex { get => selectedTabIndex; set => Set(ref selectedTabIndex, value); }
    public bool HasResult => Result is not null;
    public ProbeOperation Operation { get => operation; private set { Set(ref operation, value); RefreshState(); } }
    public bool IsBusy => Operation != ProbeOperation.Idle;
    public bool HasPendingSave => pendingSession is not null;
    public bool CanReplaceSession => !IsBusy && !HasPendingSave && !closing;
    public bool CanEditFolder => !IsBusy && !closing;
    public string Status { get => status; private set => Set(ref status, value); }
    public string Error { get => error; private set => Set(ref error, value); }
    public string TargetText { get => targetText; set => Set(ref targetText, value); }
    public string OutputFolder { get => outputFolder; set => Set(ref outputFolder, value); }
    public string SavedPath { get => savedPath; private set => Set(ref savedPath, value); }
    public WifiInterfaceInfo? SelectedInterface { get => selectedInterface; set { Set(ref selectedInterface, value); StartCommand.NotifyCanExecuteChanged(); } }
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
                1 => "Interface conectada selecionada. Informe o IPv4 de destino e confira a pasta de resultados.",
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
            Result = null;
            SavedPath = "";
            SelectedTabIndex = 0;
            Signal.Begin();
            Status = "Medindo por 60 segundos. O sinal em dBm é atualizado a cada leitura concluída; cancelar salva a sessão parcial.";
            var updates = Channel.CreateUnbounded<ProbeSample>(new UnboundedChannelOptions
            {
                SingleReader = true, SingleWriter = false, AllowSynchronousContinuations = false
            });
            var display = ReadSignalAsync(updates.Reader);
            // O coletor mantém a agenda e o controle de rota existentes, fora da thread gráfica.
            try { pendingSession = await Task.Run(() => runner.RunAsync(options, token, updates.Writer)); }
            finally
            {
                updates.Writer.TryComplete();
                await display;
                Signal.End();
            }
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
        RefreshState();
        if (Operation == ProbeOperation.Collecting) Cancel();
        else operationCancellation?.Cancel();
        await activeOperation;
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
        Error = "";
        activeOperation = CompleteAsync(action);
        return activeOperation;
    }

    private async Task CompleteAsync(Func<CancellationToken, Task> action)
    {
        using var cancellation = new CancellationTokenSource();
        operationCancellation = cancellation;
        try { await action(cancellation.Token); }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { Status = "Operação cancelada."; }
        catch (Exception ex) { ShowError(ex); }
        finally
        {
            operationCancellation = null;
            Operation = ProbeOperation.Idle;
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
        RefreshCommand?.NotifyCanExecuteChanged(); StartCommand?.NotifyCanExecuteChanged(); OpenCommand?.NotifyCanExecuteChanged();
        ChooseFolderCommand?.NotifyCanExecuteChanged(); CancelCommand?.NotifyCanExecuteChanged(); RetrySaveCommand?.NotifyCanExecuteChanged();
        DiscardCommand?.NotifyCanExecuteChanged();
    }
}
