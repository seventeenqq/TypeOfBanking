using CurrencyConverter.Core.Models;

namespace CurrencyConverter.Core.Services;

public interface IStateStore
{
    SavedState? Load();
    void Save(SavedState state);
}
