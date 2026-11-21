using System.Text.Json;
using CurrencyConverter.Core.Models;
using CurrencyConverter.Core.Services;

namespace CurrencyConverter.App.Services;

public class FileStateStore : IStateStore
{
    private readonly string path = Path.Combine(FileSystem.AppDataDirectory, "state.json");

    public SavedState? Load()
    {
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<SavedState>(File.ReadAllText(path));
    }

    public void Save(SavedState state)
    {
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(state));
        File.Move(path + ".tmp", path, overwrite: true);
    }
}
