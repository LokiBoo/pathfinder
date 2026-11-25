namespace PathFinder.Entities;

internal class DirectionNode
{
    public required (long Offset, int Length) Source { get; init; }
    public DirectionNode? Parent { get; init; }
}
