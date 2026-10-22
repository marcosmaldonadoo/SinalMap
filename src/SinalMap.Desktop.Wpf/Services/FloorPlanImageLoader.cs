using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SinalMap.Application;
using SinalMap.Domain;
using SinalMap.Presentation;

namespace SinalMap.Desktop.Wpf.Services;

internal sealed class FloorPlanImageLoader : IFloorPlanImageLoader
{
    private static readonly ConditionalWeakTable<byte[], BitmapSource> previews = new();

    public async Task<FloorPlanImage> ReadAsync(string path, CancellationToken cancellationToken)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
        if (file.Length is <= 0 or > FloorPlanValidation.MaximumImageBytes)
            throw new InvalidDataException("Use uma imagem PNG ou JPEG de até 20 MB.");
        var bytes = new byte[checked((int)file.Length)];
        await file.ReadExactlyAsync(bytes, cancellationToken);
        using var stream = new MemoryStream(bytes, writable: false);
        try
        {
            var frame = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnDemand);
            var image = new FloorPlanImage(Path.GetFileName(path), bytes, frame.PixelWidth, frame.PixelHeight);
            Validate(image);
            return image;
        }
        catch (Exception ex) when (ex is NotSupportedException or FileFormatException or ArgumentException or OverflowException)
        {
            throw new InvalidDataException("A imagem não pôde ser lida. Exporte a planta como PNG ou JPEG e tente novamente.", ex);
        }
    }

    public void Validate(FloorPlanImage image)
    {
        FloorPlanValidation.Validate(new(1, Guid.NewGuid(), "Validação da imagem", image.FileName,
            image.Bytes, image.PixelWidth, image.PixelHeight, null, [], []));
        try { _ = GetPreview(image.Bytes); }
        catch (Exception ex) when (ex is NotSupportedException or FileFormatException or ArgumentException or OverflowException)
        {
            throw new InvalidDataException("A imagem está danificada ou usa uma codificação não suportada. Tente outro PNG ou JPEG.", ex);
        }
    }

    internal static BitmapSource GetPreview(byte[] bytes) => previews.GetValue(bytes, data =>
    {
        using var stream = new MemoryStream(data, writable: false);
        var frame = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnDemand);
        var width = frame.PixelWidth;
        var height = frame.PixelHeight;
        stream.Position = 0;
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        if (Math.Max(width, height) > 2048)
        {
            if (width >= height) bitmap.DecodePixelWidth = 2048;
            else bitmap.DecodePixelHeight = 2048;
        }
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        // Força a decodificação da prévia antes de substituir o projeto que já está na tela.
        var stride = checked((bitmap.PixelWidth * bitmap.Format.BitsPerPixel + 7) / 8);
        bitmap.CopyPixels(new byte[checked(stride * bitmap.PixelHeight)], stride, 0);
        bitmap.Freeze();
        return bitmap;
    });
}

public sealed class FloorPlanImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        value is byte[] bytes ? FloorPlanImageLoader.GetPreview(bytes) : null;
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}
