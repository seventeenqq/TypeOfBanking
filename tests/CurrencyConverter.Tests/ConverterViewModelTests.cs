using CurrencyConverter.Core.Models;
using CurrencyConverter.Core.Services;
using CurrencyConverter.Core.ViewModels;

namespace CurrencyConverter.Tests;

public class ConverterViewModelTests
{
    [Fact]
    public async Task EditingFirstAmountConvertsToSecondCurrency()
    {
        var vm = await CreateViewModel();
        vm.FromAmount = "250";
        Assert.Equal("2,5", vm.ToAmount);
    }

    [Fact]
    public async Task EditingSecondAmountConvertsBack()
    {
        var vm = await CreateViewModel();
        vm.ToAmount = "3,5";
        Assert.Equal("350", vm.FromAmount);
    }

    [Fact]
    public async Task ChangingCurrencyRecalculates()
    {
        var vm = await CreateViewModel();
        vm.FromAmount = "250";
        vm.ToCurrency = vm.Currencies.Find(c => c.Code == "EUR");
        Assert.Equal("2", vm.ToAmount);
    }

    private static async Task<ConverterViewModel> CreateViewModel()
    {
        var vm = new ConverterViewModel(new FakeRatesService(), new MemoryStore());
        vm.SelectedDate = new DateTime(2024, 10, 12);
        await vm.InitializeAsync();
        return vm;
    }

    // Вместо интернета возвращаем известные курсы: USD = 100 рублей, EUR = 125.
    private class FakeRatesService : IRatesService
    {
        public Task<Rates> GetRatesAsync(DateTime date)
        {
            var rates = new Rates
            {
                Date = date,
                Currencies = new List<Currency>
                {
                    new() { Code = "RUB", Name = "Российский рубль", Value = 1 },
                    new() { Code = "USD", Name = "Доллар США", Value = 100 },
                    new() { Code = "EUR", Name = "Евро", Value = 125 }
                }
            };
            return Task.FromResult(rates);
        }
    }

    // В тестах храним данные в памяти, чтобы не создавать файлы на компьютере.
    private class MemoryStore : IStateStore
    {
        private SavedState? state;
        public SavedState? Load() => state;
        public void Save(SavedState value) => state = value;
    }
}
