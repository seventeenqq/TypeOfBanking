using CurrencyConverter.App.Services;
using CurrencyConverter.Core.Services;
using CurrencyConverter.Core.ViewModels;

namespace CurrencyConverter.App;

public partial class MainPage : ContentPage
{
    private readonly ConverterViewModel viewModel;

    public MainPage()
    {
        InitializeComponent();
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(Settings.ServerUrl),
            Timeout = TimeSpan.FromSeconds(120)
        };
        viewModel = new ConverterViewModel(new RatesApiClient(httpClient), new FileStateStore());
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.InitializeAsync();
    }

    private async void OnRefreshClicked(object? sender, EventArgs e)
    {
        await viewModel.LoadRatesAsync();
    }
}
