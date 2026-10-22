using System.IO;
using System.Windows;
using Microsoft.Win32;
using SinalMap.Presentation;

namespace SinalMap.Desktop.Wpf.Services;

internal sealed class FloorPlanDialogs(Func<Window> owner) : IFloorPlanDialogs
{
    public string? ChooseImage()
    {
        var dialog = new OpenFileDialog { Title = "Importar foto da planta da loja", Filter = "Imagem da planta (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg", CheckFileExists = true, Multiselect = false, AddToRecent = false };
        return dialog.ShowDialog(owner()) == true ? dialog.FileName : null;
    }

    public string? ChooseProject()
    {
        var dialog = new OpenFileDialog { Title = "Abrir projeto de planta", Filter = "Projeto de planta (*.sinalmap-planta.json)|*.sinalmap-planta.json", CheckFileExists = true, Multiselect = false, AddToRecent = false };
        return dialog.ShowDialog(owner()) == true ? dialog.FileName : null;
    }

    public string? ChooseSavePath(string? currentPath)
    {
        var dialog = new SaveFileDialog { Title = "Salvar projeto da planta", Filter = "Projeto de planta (*.sinalmap-planta.json)|*.sinalmap-planta.json", DefaultExt = ".sinalmap-planta.json", AddExtension = true, OverwritePrompt = true, AddToRecent = false,
            FileName = currentPath is null ? "planta-loja.sinalmap-planta.json" : Path.GetFileName(currentPath) };
        if (currentPath is not null) dialog.InitialDirectory = Path.GetDirectoryName(currentPath);
        return dialog.ShowDialog(owner()) == true ? dialog.FileName : null;
    }

    public UnsavedProjectChoice ConfirmUnsavedChanges() => MessageBox.Show(owner(),
        "O projeto da planta tem alterações não salvas. Deseja salvá-las? Os JSONs de coleta já gravados não são apagados ao descartar as alterações da planta.",
        "Salvar projeto da planta", MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) switch
        {
            MessageBoxResult.Yes => UnsavedProjectChoice.Save,
            MessageBoxResult.No => UnsavedProjectChoice.Discard,
            _ => UnsavedProjectChoice.Cancel
        };
}
