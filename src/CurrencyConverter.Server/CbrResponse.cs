namespace CurrencyConverter.Server;
public class CbrResponse
{
    public DateTimeOffset Date { get; set; }
    public Dictionary<string, CbrCurrency> Valute { get; set; } = new();
}

public class CbrCurrency
{
    public string CharCode { get; set; } = "";
    public string Name { get; set; } = "";
    public int Nominal { get; set; }
    public decimal Value { get; set; }
}
