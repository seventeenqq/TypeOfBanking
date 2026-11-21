namespace CurrencyConverter.App.Services;

public static class Settings
{
    public static string ServerUrl => DeviceInfo.Platform == DevicePlatform.Android
        ? "http://10.0.2.2:5057/"
        : "http://localhost:5057/";
}
