namespace CurrencyConverter.Core.Models;

public class Currency
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int Nominal { get; set; } = 1;
    public decimal Value { get; set; }

    public decimal RublesPerUnit => Value / Nominal;
    public string DisplayName => $"{Name} ({Code})";
}
