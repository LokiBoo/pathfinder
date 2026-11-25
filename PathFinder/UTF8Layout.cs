using PathFinder.Entities;
using System.Text;
using System.Text.Unicode;

namespace PathFinder;

internal class UTF8Layout
{
    private const byte Pipe = (byte)'|';
    private const byte Space = (byte)' ';
    private const int LevelBytes = 3;

    private static ReadOnlySpan<byte> Branch => "├──"u8;
    private static ReadOnlySpan<byte> LastBranch => "└──"u8;
    private static ReadOnlySpan<byte> DirectionMarker => "+ "u8;
    private static ReadOnlySpan<byte> ItemMarker => " Item: "u8;

    public byte LF { get; } = (byte)'\n';
    public byte CR { get; } = (byte)'\r';
      
    private static ReadOnlySpan<byte> AsciiWhitespace => " \t\r\n\v\f"u8;

    public bool Validate(ReadOnlySpan<byte> header, out int textStart)
    {
        textStart = 0;

        if (header.StartsWith<byte>([0xFF, 0xFE, 0x00, 0x00]))
            return false;

        if (header.StartsWith<byte>([0x00, 0x00, 0xFE, 0xFF]))
            return false;

        if (header.StartsWith<byte>([0xFF, 0xFE]))
            return false;

        if (header.StartsWith<byte>([0xFE, 0xFF]))
            return false;

        if (header.StartsWith<byte>([0xEF, 0xBB, 0xBF]))
            textStart = 3;   // UTF-8 BOM: skip it

        return true;
    }

    public bool TryParseLine(ReadOnlySpan<byte> line, out TreeLine result)
    {
        result = default;
        var offset = 0;
        var depth = 0;
        var pipeMask = 0;
        var isLastChild = false;

        while (offset < line.Length)
        {
            var b = line[offset];

            if (b == Pipe || b == Space)
            {
                if (offset + LevelBytes > line.Length)
                    return false; // Not enough bytes for level

                if (line[offset + 1] != Space || line[offset + 2] != Space)
                    return false; // Level byte should be followed by two spaces

                if (b == Pipe)
                    pipeMask |= 1 << depth; // Set bit for pipe

                offset += LevelBytes; // Skip level bytes
                depth++;
            }
            else if(b == Branch[0]) // Both tree connectors start with same byte
            {
                var rest = line.Slice(offset);

                if (rest.StartsWith(LastBranch))
                    isLastChild = true;
                else if (!rest.StartsWith(Branch))
                    return false; // Not a valid tree connector

                depth++;
                offset += Branch.Length; // Skip tree connector bytes
                break;
            }
            else
            {
                // Root case
                if (depth != 0) return false;
                break;
            }
        }

        var body = line.Slice(offset);

        LineKind kind;
        if (body.StartsWith(DirectionMarker))
        {
            kind = LineKind.Direction;
            offset += DirectionMarker.Length;
        }
        else if (body.StartsWith(ItemMarker))
        {
            kind = LineKind.Item;
            offset += ItemMarker.Length;
        }
        else
            return false;

        result = new TreeLine(kind, depth, offset, line.Length - offset, isLastChild, pipeMask);
        return true;
    }

    public bool IsValid(ReadOnlySpan<byte> row)
        => Utf8.IsValid(row);

    public string Decode(ReadOnlySpan<byte> buffer)
        => Encoding.UTF8.GetString(buffer);

    public bool IsBlank(ReadOnlySpan<byte> text)
        => text.IndexOfAnyExcept(AsciiWhitespace) < 0;
}
