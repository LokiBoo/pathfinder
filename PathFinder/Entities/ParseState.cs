namespace PathFinder.Entities;

internal class ParseState
{
    public readonly HashSet<string> ItemNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // Case could be moved to settings if needed
    public readonly List<Item> Items = new List<Item>();
    public readonly Stack<DirectionNode> Levels = new Stack<DirectionNode>();
    public int PreviousLineMask;
    public int LinesProcessed;
    public int Closed;
}
