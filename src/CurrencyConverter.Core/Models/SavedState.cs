namespace CurrencyConverter.Core.Models;

public class SavedState
{
    public DateTime SelectedDate { get; set; } = DateTime.Today;
    public string FromCode { get; set; } = "RUB";
    public string ToCode { get; set; } = "USD";
    public string FromAmount { get; set; } = "1";
    public string ToAmount { get; set; } = "";
    public bool LastEditedFrom { get; set; } = true;
    public Rates? Rates { get; set; }
}
