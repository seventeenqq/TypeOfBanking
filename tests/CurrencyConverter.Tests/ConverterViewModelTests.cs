using CurrencyConverter.Core.Models;
using CurrencyConverter.Core.Services;
using CurrencyConverter.Core.ViewModels;
using Moq;

namespace CurrencyConverter.Tests;

public class ConverterViewModelTests
{
    [Fact]
    public async Task EditingFirstAmountConvertsToSecondCurrency()
    {
        var date = new DateTime(2024, 10, 12);
        var service = new Mock<IRatesService>();
        service.Setup(s => s.GetRatesAsync(date))
            .ReturnsAsync(CreateRates(date, usdRate: 100));
        var vm = await CreateViewModel(service, date);

        vm.FromAmount = "250";

        Assert.Equal("2,5", vm.ToAmount);
    }

    [Fact]
    public async Task EditingSecondAmountConvertsBack()
    {
        var date = new DateTime(2024, 10, 12);
        var service = new Mock<IRatesService>();
        service.Setup(s => s.GetRatesAsync(date))
            .ReturnsAsync(CreateRates(date, usdRate: 100));
        var vm = await CreateViewModel(service, date);

        vm.ToAmount = "3,5";

        Assert.Equal("350", vm.FromAmount);
    }

    [Fact]
    public async Task ChangingCurrencyRecalculates()
    {
        var date = new DateTime(2024, 10, 12);
        var service = new Mock<IRatesService>();
        service.Setup(s => s.GetRatesAsync(date))
            .ReturnsAsync(CreateRates(date, usdRate: 100, eurRate: 125));
        var vm = await CreateViewModel(service, date);

        vm.FromAmount = "250";
        vm.ToCurrency = vm.Currencies.Find(c => c.Code == "EUR");

        Assert.Equal("2", vm.ToAmount);
    }

    [Fact]
    public async Task ChangingDateAutomaticallyLoadsRatesAndRecalculates()
    {
        var firstDate = new DateTime(2024, 10, 12);
        var secondDate = new DateTime(2024, 10, 15);
        var service = new Mock<IRatesService>();
        service.Setup(s => s.GetRatesAsync(firstDate))
            .ReturnsAsync(CreateRates(firstDate, usdRate: 100));

        var secondResponse = new TaskCompletionSource<Rates>();
        service.Setup(s => s.GetRatesAsync(secondDate))
            .Returns(secondResponse.Task);

        var vm = await CreateViewModel(service, firstDate);
        vm.FromAmount = "250";
        Assert.Equal("2,5", vm.ToAmount);

        vm.SelectedDate = secondDate;
        Assert.True(vm.IsBusy);

        secondResponse.SetResult(CreateRates(secondDate, usdRate: 125));
        await vm.CurrentLoad.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(secondDate, vm.SelectedDate);
        Assert.Equal("250", vm.FromAmount);
        Assert.Equal("2", vm.ToAmount);
        Assert.Equal("Курс на 15.10.2024. Выбрано: 15.10.2024.", vm.RateHint);
        Assert.False(vm.IsBusy);
        Assert.Empty(vm.ErrorMessage);

        service.Verify(s => s.GetRatesAsync(firstDate), Times.Once());
        service.Verify(s => s.GetRatesAsync(secondDate), Times.Once());
        service.VerifyNoOtherCalls();
    }

    private static async Task<ConverterViewModel> CreateViewModel(
        Mock<IRatesService> service, DateTime date)
    {
        var store = new Mock<IStateStore>();
        store.Setup(s => s.Load()).Returns((SavedState?)null);

        var vm = new ConverterViewModel(service.Object, store.Object);
        vm.SelectedDate = date;
        await vm.InitializeAsync();
        return vm;
    }

    private static Rates CreateRates(DateTime date, decimal usdRate, decimal eurRate = 125)
    {
        return new Rates
        {
            Date = date,
            Currencies = new List<Currency>
            {
                new() { Code = "RUB", Name = "Российский рубль", Value = 1 },
                new() { Code = "USD", Name = "Доллар США", Value = usdRate },
                new() { Code = "EUR", Name = "Евро", Value = eurRate }
            }
        };
    }
}
