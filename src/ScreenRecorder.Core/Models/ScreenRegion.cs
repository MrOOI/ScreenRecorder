namespace ScreenRecorder.Core.Models;

/// <summary>
/// An axis-aligned rectangle in physical pixels.
/// </summary>
public readonly record struct ScreenRegion(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>
    /// Creates a normalized region from two arbitrary corner points.
    /// </summary>
    public static ScreenRegion FromPoints(int x1, int y1, int x2, int y2) =>
        new(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1));

    /// <summary>
    /// Returns the overlap of this region and <paramref name="other"/>, or an empty region.
    /// </summary>
    public ScreenRegion Intersect(ScreenRegion other)
    {
        var left = Math.Max(X, other.X);
        var top = Math.Max(Y, other.Y);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);

        return right <= left || bottom <= top
            ? default
            : new ScreenRegion(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// H.264 requires even frame dimensions; this trims at most one pixel per axis.
    /// </summary>
    public ScreenRegion WithEvenSize() => this with { Width = Width & ~1, Height = Height & ~1 };

    public override string ToString() => $"{Width}×{Height}";
}
