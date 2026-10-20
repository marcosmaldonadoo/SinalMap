using System.IO;
using System.Windows;
using Microsoft.Win32;
using SinalMap.Presentation;

namespace SinalMap.Desktop.Wpf.Services;

internal sealed class ProbeDialogs(Window owner) : IProbeDialogs
{
    public string? ChooseOutputFolder(string currentFolder)
    {
        var dialog = new OpenFolderDialog { Title = "Escolher pasta para os JSONs locais", Multiselect = false, AddToRecent = false };
        if (Directory.Exists(currentFolder)) dialog.InitialDirectory = currentFolder;
        return dialog.ShowDialog(owner) == true ? dialog.FolderName : null;
    }

    public string? ChooseSessionFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Abrir coleta salva", Filter = "Coleta JSON (*.json)|*.json", Multiselect = false,
            CheckFileExists = true, AddToRecent = false
        };
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }

    public bool ConfirmDiscard() => MessageBox.Show(owner,
        "A sessão ainda não foi salva. Descartar os dados em memória? Essa ação não apaga arquivos já salvos.",
        "Descartar sessão não salva", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
}
