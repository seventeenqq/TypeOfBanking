using CurrencyConverter.Core.Models;

namespace CurrencyConverter.Core.Services;

public interface IRatesService
{
    Task<Rates> GetRatesAsync(DateTime date);
}
