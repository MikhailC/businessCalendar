using System.Text;

namespace BusinessCalendarAPI.Services;

/// <summary>
/// Thread-safe storage for business calendar XML file (read/write raw bytes).
/// </summary>
public sealed class BusinessCalendarFileStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public BusinessCalendarFileStore(string filePath)
    {
        _filePath = filePath;
    }

    public string FilePath => _filePath;

    public async Task<byte[]?> TryReadAllBytesAsync(CancellationToken ct)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            if (!File.Exists(_filePath))
                return null;

            return await File.ReadAllBytesAsync(_filePath, ct);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task WriteAllBytesAsync(byte[] content, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath) ?? ".");

        await _mutex.WaitAsync(ct);
        try
        {
            // atomic-ish replace
            var tmp = _filePath + ".tmp";
            await File.WriteAllBytesAsync(tmp, content, ct);
            File.Copy(tmp, _filePath, overwrite: true);
            File.Delete(tmp);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<string?> TryReadAsStringAsync(CancellationToken ct)
    {
        var bytes = await TryReadAllBytesAsync(ct);
        if (bytes is null)
            return null;

        // Best effort. Actual parsing uses XmlReader which respects encoding in prolog.
        return Encoding.UTF8.GetString(bytes);
    }
}



