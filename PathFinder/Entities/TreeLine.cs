namespace PathFinder.Entities;

internal enum LineKind { Direction, Item }

internal readonly record struct TreeLine(
    LineKind Kind,
    int Depth,
    int BodyOffset,
    int BodyLength,
    bool IsLastChild,
    int DepthMask);
