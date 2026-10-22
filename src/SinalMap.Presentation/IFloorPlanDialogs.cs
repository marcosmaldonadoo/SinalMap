namespace SinalMap.Presentation;

public enum UnsavedProjectChoice { Save, Discard, Cancel }

public interface IFloorPlanDialogs
{
    string? ChooseImage();
    string? ChooseProject();
    string? ChooseSavePath(string? currentPath);
    UnsavedProjectChoice ConfirmUnsavedChanges();
}

public sealed record FloorPlanImage(string FileName, byte[] Bytes, int PixelWidth, int PixelHeight);

public interface IFloorPlanImageLoader
{
    Task<FloorPlanImage> ReadAsync(string path, CancellationToken cancellationToken);
    void Validate(FloorPlanImage image);
}
