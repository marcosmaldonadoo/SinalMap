namespace SinalMap.Presentation;

public interface IProbeDialogs
{
    string? ChooseOutputFolder(string currentFolder);
    string? ChooseSessionFile();
    bool ConfirmDiscard();
}
