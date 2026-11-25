using PathFinder.Entities;
using System.Diagnostics;

namespace PathFinder;

// One instance per read.
internal class TreeReader
{
    private const int BufferSize = 4096; // Used FileStream default buffer size, can be adjusted if needed

    private readonly UTF8Layout _layout; // Possible to make an abstraction for different layouts in the future

    private ParseState _parseState = new ParseState();

    public TreeReader(UTF8Layout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        _layout = layout;
    }

    public async Task<List<Item>> ReadAsync(FileStream stream, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        _parseState = new ParseState();
        stream.Position = 0;

        // Check file encoding from first 4 bytes
        var header = new byte[4];
        await stream.ReadAtLeastAsync(header, 4, throwOnEndOfStream: false, ct);
        if (!_layout.Validate(header, out var textStart))
        {
            throw new InvalidDataException("File is not in expected format.");
        }

        stream.Position = textStart;
        long bytesProcessed = textStart;
        var buffer = new byte[BufferSize];
        while (stream.Position < stream.Length)
        {
            var bytesRead = await stream.ReadAtLeastAsync(buffer, BufferSize, throwOnEndOfStream: false, ct);

            var isLast = stream.Position >= stream.Length;
            var processed = ProcessBuffer(buffer.AsSpan(0, bytesRead), bytesProcessed, isLast);

            if (processed == 0 && !isLast)
                throw new InvalidDataException($"Line {_parseState.LinesProcessed + 1} exceeds the {BufferSize}-byte buffer.");

            stream.Position -= (bytesRead - processed); // For scenarios when previous buffer ended in the middle of the row
            bytesProcessed = stream.Position;
        }

        return _parseState.Items;
    }

    private int ProcessBuffer(ReadOnlySpan<byte> buffer, long offset, bool isLast)
    {
        var processedBytes = 0;

        while (true)
        {
            var newLineIndex = buffer.IndexOf(_layout.LF);

            var lineLength = 0;
            var advance = 0; // Full line length with new line terminator

            if (newLineIndex >= 0)
            {
                lineLength = newLineIndex;
                advance = newLineIndex + 1; // Consume the terminator
            }
            else
            {
                if (!isLast) break; // Partial line
                if (buffer.IsEmpty) break; // File ended with new line
                lineLength = buffer.Length; // final line, unterminated
                advance = buffer.Length;
            }

            var rowSlice = buffer.Slice(0, lineLength).TrimEnd(_layout.CR);
            HandleRow(rowSlice, offset);

            offset += advance;
            processedBytes += advance;
            buffer = buffer.Slice(advance);
        }

        return processedBytes;
    }

    private void HandleRow(ReadOnlySpan<byte> rowSlice, long offset)
    {
        _parseState.LinesProcessed += 1;

        if (_layout.IsBlank(rowSlice))
            throw new InvalidDataException($"Invalid line format at line {_parseState.LinesProcessed}: Empty line.");

        if (!_layout.TryParseLine(rowSlice, out TreeLine line))
            throw new InvalidDataException($"Invalid line format at line {_parseState.LinesProcessed}.");

        var text = rowSlice.Slice(line.BodyOffset, line.BodyLength);
        if (!_layout.IsValid(text))
            throw new InvalidDataException($"Invalid UTF-8 at line {_parseState.LinesProcessed}.");

        if (line.Depth > _parseState.Levels.Count)
            throw new InvalidDataException($"Invalid line format at line {_parseState.LinesProcessed}: Depth is wrong."); // New direction depth is bigger then 1 level

        CheckFormatting(line.Depth, line.IsLastChild, line.DepthMask);

        while (_parseState.Levels.Count > line.Depth)
            _parseState.Levels.Pop(); // If new item is on level deeper then previous

        switch (line.Kind)
        {
            case LineKind.Direction:
                if (_layout.IsBlank(text))
                    throw new InvalidDataException($"Line {_parseState.LinesProcessed}: direction is empty or whitespace.");

                var node = new DirectionNode
                {
                    Source = (offset + line.BodyOffset, line.BodyLength),
                    Parent = _parseState.Levels.Count > 0 ? _parseState.Levels.Peek() : null
                };
                _parseState.Levels.Push(node);
                break;
            case LineKind.Item:
                var itemName = _layout.Decode(text).Trim();

                if (string.IsNullOrWhiteSpace(itemName))
                    throw new InvalidDataException($"Invalid line format at line {_parseState.LinesProcessed}: item name is empty or whitespace.");

                if (!_parseState.ItemNames.Add(itemName))
                    throw new InvalidDataException($"Duplicate item name '{itemName}' at line {_parseState.LinesProcessed}.");

                _parseState.Items.Add(new Item(itemName, _parseState.Levels.Count > 0 ? _parseState.Levels.Peek() : null));
                break;

            default:
                throw new NotImplementedException();

        }
    }

    // Verifies that this line vertical bars and connectors match previous line
    private void CheckFormatting(int depth, bool isLastChild, int depthMask)
    {
        // Max depth is 31 , because we use 32-bit int to store state, could be increased if needed
        if (depth >= 31)
            throw new InvalidDataException($"Invalid line format at line {_parseState.LinesProcessed}: depth exceeds maximum supported depth of 31.");

        // Create mask with bits we want to compare, exclude root and current depth bits
        var toCompare = ~(1 << depth) & ~1;

        // Compare selected bits of continuation with current line depthMask
        if ((_parseState.PreviousLineMask & toCompare) != (depthMask << 1))
            throw new InvalidDataException($"Invalid line format at line {_parseState.LinesProcessed}: Invalid pipe configuration.");

        // Compare same level connector
        if ((_parseState.Closed & (1 << depth)) != 0)
            throw new InvalidDataException($"Invalid line format at line {_parseState.LinesProcessed}: a last child connector already closed this level.");

        _parseState.Closed &= (1 << (depth + 1)) - 1; // Set if previous item had closed branch

        if (!isLastChild)
        {
            _parseState.PreviousLineMask |= 1 << depth; // Set continuation for this level
        }
        else
        {
            _parseState.Closed |= 1 << depth;
            _parseState.PreviousLineMask &= ~(1 << depth); // Clear continuation for this level
        }
    }
}
