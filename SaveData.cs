using System.Text.Json;

namespace TheAdventure;

public sealed class SaveDataException : Exception
{
    public SaveDataException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public interface ISaveStore<T>
{
    Task<T?> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(T data, CancellationToken cancellationToken);
}

public sealed class JsonFileStore<T> : ISaveStore<T>
    where T : class
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _filePath;

    public JsonFileStore(string filePath)
    {
        _filePath = filePath;
    }

    public async Task<T?> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            var raw = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            return JsonSerializer.Deserialize<T>(raw, SerializerOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new SaveDataException($"Failed to load save file '{_filePath}'.", ex);
        }
    }

    public async Task SaveAsync(T data, CancellationToken cancellationToken)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var raw = JsonSerializer.Serialize(data, SerializerOptions);
            await File.WriteAllTextAsync(_filePath, raw, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new SaveDataException($"Failed to save file '{_filePath}'.", ex);
        }
    }
}

public sealed record SnakeSaveData(int HighScore);
