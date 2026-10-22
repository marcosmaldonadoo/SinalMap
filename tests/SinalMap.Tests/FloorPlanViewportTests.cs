using SinalMap.Domain;
using SinalMap.Presentation;

namespace SinalMap.Tests;

public sealed class FloorPlanViewportTests
{
    [Theory]
    [InlineData(800, 600, 1600, 800, 0, 100, 800, 400)]
    [InlineData(800, 600, 800, 1600, 250, 0, 300, 600)]
    [InlineData(600, 600, 800, 800, 0, 0, 600, 600)]
    public void Fit_preserves_aspect_ratio_and_centers_the_image(double width, double height, double imageWidth,
        double imageHeight, double left, double top, double fittedWidth, double fittedHeight)
    {
        var viewport = FloorPlanViewport.Fit(width, height, imageWidth, imageHeight);
        Assert.True(viewport.IsValid);
        Assert.Equal(left, viewport.Left);
        Assert.Equal(top, viewport.Top);
        Assert.Equal(fittedWidth, viewport.Width);
        Assert.Equal(fittedHeight, viewport.Height);
        Assert.Equal(new PlanPosition(.5, .5), viewport.ToNormalized(width / 2, height / 2));
    }

    [Fact]
    public void Margins_do_not_place_markers_and_image_edges_are_valid()
    {
        var viewport = FloorPlanViewport.Fit(800, 600, 1600, 800);
        Assert.Null(viewport.ToNormalized(400, 99));
        Assert.Null(viewport.ToNormalized(400, 501));
        Assert.Null(viewport.ToNormalized(-1, 100));
        Assert.Null(viewport.ToNormalized(801, 100));
        Assert.Equal(new PlanPosition(0, 0), viewport.ToNormalized(0, 100));
        Assert.Equal(new PlanPosition(1, 1), viewport.ToNormalized(800, 500));
    }

    [Fact]
    public void Resizing_reprojects_the_same_normalized_position_without_changing_it()
    {
        var position = new PlanPosition(.25, .75);
        var wide = FloorPlanViewport.Fit(1000, 600, 1200, 800);
        var compact = FloorPlanViewport.Fit(600, 500, 1200, 800);
        var before = wide.ToDisplay(position)!.Value;
        var after = compact.ToDisplay(position)!.Value;
        Assert.NotEqual(before, after);
        Assert.Equal(position, wide.ToNormalized(before.X, before.Y));
        Assert.Equal(position, compact.ToNormalized(after.X, after.Y));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Invalid_dimensions_cannot_produce_positions(double invalid)
    {
        Assert.False(FloorPlanViewport.Fit(invalid, 500, 100, 100).IsValid);
        Assert.False(FloorPlanViewport.Fit(500, invalid, 100, 100).IsValid);
        Assert.False(FloorPlanViewport.Fit(500, 500, invalid, 100).IsValid);
        Assert.False(FloorPlanViewport.Fit(500, 500, 100, invalid).IsValid);
        Assert.Null(default(FloorPlanViewport).ToNormalized(0, 0));
        Assert.Null(default(FloorPlanViewport).ToDisplay(new(.5, .5)));
    }

    [Theory]
    [InlineData(double.NaN, .5)]
    [InlineData(.5, double.NegativeInfinity)]
    [InlineData(-.01, .5)]
    [InlineData(.5, 1.01)]
    public void Invalid_coordinates_are_never_projected_or_clamped_onto_the_image(double x, double y)
    {
        var viewport = FloorPlanViewport.Fit(800, 600, 1600, 800);
        Assert.Null(viewport.ToDisplay(new(x, y)));
        Assert.Null(viewport.ToNormalized(double.NaN, 200));
        Assert.Null(viewport.ToNormalized(200, double.PositiveInfinity));
    }
}
