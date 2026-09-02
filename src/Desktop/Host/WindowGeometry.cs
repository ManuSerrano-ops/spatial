namespace PlanoOpenSpaceIT.Windows;

internal readonly record struct WindowBounds(double Left, double Top, double Width, double Height)
{
    internal double CenterX => Left + Width / 2;
    internal double CenterY => Top + Height / 2;
    internal bool IsFiniteAndPositive => double.IsFinite(Left)
        && double.IsFinite(Top)
        && double.IsFinite(Width)
        && double.IsFinite(Height)
        && Width > 0
        && Height > 0;
}

internal static class WindowGeometry
{
    internal static readonly WindowBounds DefaultBounds = new(0, 0, 1400, 900);

    internal static WindowBounds Clamp(
        WindowBounds savedBounds,
        IReadOnlyList<WindowBounds> workingAreas,
        WindowBounds primaryWorkingArea)
    {
        if (workingAreas.Count == 0) return DefaultBounds;

        var primary = IsUsableWorkingArea(primaryWorkingArea) ? primaryWorkingArea : workingAreas.FirstOrDefault(IsUsableWorkingArea);
        if (!IsUsableWorkingArea(primary)) return DefaultBounds;

        if (!savedBounds.IsFiniteAndPositive) return CenterAndFit(DefaultBounds, primary);

        var targetArea = workingAreas.FirstOrDefault(area => Contains(area, savedBounds.CenterX, savedBounds.CenterY));
        if (!IsUsableWorkingArea(targetArea)) return CenterAndFit(DefaultBounds, primary);

        return FitAndPosition(savedBounds, targetArea);
    }

    private static WindowBounds CenterAndFit(WindowBounds bounds, WindowBounds area)
    {
        var width = Math.Min(bounds.Width, area.Width);
        var height = Math.Min(bounds.Height, area.Height);
        return new WindowBounds(
            area.Left + (area.Width - width) / 2,
            area.Top + (area.Height - height) / 2,
            width,
            height);
    }

    private static WindowBounds FitAndPosition(WindowBounds bounds, WindowBounds area)
    {
        var width = Math.Min(bounds.Width, area.Width);
        var height = Math.Min(bounds.Height, area.Height);
        return new WindowBounds(
            Math.Clamp(bounds.Left, area.Left, area.Left + area.Width - width),
            Math.Clamp(bounds.Top, area.Top, area.Top + area.Height - height),
            width,
            height);
    }

    private static bool Contains(WindowBounds area, double x, double y) => IsUsableWorkingArea(area)
        && x >= area.Left
        && x <= area.Left + area.Width
        && y >= area.Top
        && y <= area.Top + area.Height;

    private static bool IsUsableWorkingArea(WindowBounds area) => area.IsFiniteAndPositive;
}
