namespace CurrencyConverter.Core.Services;
public class RatesApiException : Exception
{
    public RatesApiException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
