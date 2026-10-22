using SinalMap.Domain;

namespace SinalMap.Presentation;

/// <summary>Área ocupada pela imagem ajustada, sem confundir margens com posições na planta.</summary>
public readonly record struct FloorPlanViewport(double Left, double Top, double Width, double Height)
{
    public bool IsValid => double.IsFinite(Left) && double.IsFinite(Top) &&
        double.IsFinite(Width) && double.IsFinite(Height) && Width > 0 && Height > 0;

    public static FloorPlanViewport Fit(double availableWidth, double availableHeight, double imageWidth, double imageHeight)
    {
        if (!Positive(availableWidth) || !Positive(availableHeight) || !Positive(imageWidth) || !Positive(imageHeight)) return default;
        var scale = Math.Min(availableWidth / imageWidth, availableHeight / imageHeight);
        var width = imageWidth * scale;
        var height = imageHeight * scale;
        var result = new FloorPlanViewport((availableWidth - width) / 2, (availableHeight - height) / 2, width, height);
        return result.IsValid ? result : default;
    }

    public PlanPosition? ToNormalized(double x, double y)
    {
        if (!IsValid || !double.IsFinite(x) || !double.IsFinite(y) ||
            x < Left || y < Top || x > Left + Width || y > Top + Height) return null;
        return new((x - Left) / Width, (y - Top) / Height);
    }

    public (double X, double Y)? ToDisplay(PlanPosition? position) => IsValid && IsNormalized(position)
        ? (Left + position!.X * Width, Top + position.Y * Height) : null;

    public static bool IsNormalized(PlanPosition? position) => position is not null &&
        double.IsFinite(position.X) && double.IsFinite(position.Y) && position.X is >= 0 and <= 1 && position.Y is >= 0 and <= 1;

    private static bool Positive(double value) => double.IsFinite(value) && value > 0;
}
