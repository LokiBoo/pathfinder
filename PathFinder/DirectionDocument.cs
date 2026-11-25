using PathFinder.Entities;

namespace PathFinder;

internal class DirectionDocument : IDisposable
{    
    private readonly UTF8Layout _layout; // Possible to make an abstraction for different layouts in the future
    private readonly FileStream _fileStream;
    private bool _disposed;

    public List<Item> Items { get; }

    private DirectionDocument(FileStream fileStream, UTF8Layout layout, List<Item> items)
    {
        _fileStream = fileStream;
        _layout = layout;
        Items = items;
    }

    public static async Task<DirectionDocument> LoadAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var layout = new UTF8Layout();
        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, useAsync: true, bufferSize: 0);

        try
        {
            var items = await new TreeReader(layout).ReadAsync(stream, ct);
            return new DirectionDocument(stream, layout, items);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    public async Task<List<string>> GetDirectionAsync(Item item, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var path = new List<DirectionNode>();
        for (var n = item.Leaf; n is not null; n = n.Parent)
            path.Add(n);

        path.Reverse();

        var result = new List<string>(path.Count);
        foreach (var n in path)
            result.Add(await ReadAsync(n.Source.Offset, n.Source.Length, ct));

        return result;
    }

    private async Task<string> ReadAsync(long offset, int length, CancellationToken ct)
    {
        _fileStream.Position = offset;
        var buffer = new byte[length];
        await _fileStream.ReadExactlyAsync(buffer, 0, length, ct);
        return _layout.Decode(buffer).Trim();
    }

    // Not async, nothing to flush
    public void Dispose()
    {
        if (_disposed)
            return;

        _fileStream.Dispose();
        _disposed = true;
    }
}
