namespace CurrencyConverter.Core.Models;

public class Rates
{
    public DateTime Date { get; set; }
    public List<Currency> Currencies { get; set; } = new();
}
