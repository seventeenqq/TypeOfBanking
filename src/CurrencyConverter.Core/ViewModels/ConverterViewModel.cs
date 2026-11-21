using System.ComponentModel;
using System.Globalization;
using CurrencyConverter.Core.Models;
using CurrencyConverter.Core.Services;

namespace CurrencyConverter.Core.ViewModels;

public class ConverterViewModel : INotifyPropertyChanged
{
    private readonly IRatesService ratesService;
    private readonly IStateStore stateStore;
    private SavedState state = new();
    private bool initialized;

    public ConverterViewModel(IRatesService ratesService, IStateStore stateStore)
    {
        this.ratesService = ratesService;
        this.stateStore = stateStore;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    public bool IsBusy { get; private set; }
    public bool CanEdit => !IsBusy;
    public DateTime MaximumDate => DateTime.Today;
    public string ErrorMessage { get; private set; } = "";
    public string InputError { get; private set; } = "";
    public List<Currency> Currencies => state.Rates?.Currencies ?? new List<Currency>();
    public bool CanConvert => !IsBusy && state.Rates != null &&
        state.Rates.Date.Date <= SelectedDate && FromCurrency != null && ToCurrency != null;

    public string RateHint
    {
        get
        {
            if (IsBusy) return "Загрузка курсов…";
            if (state.Rates == null || state.Rates.Date.Date > SelectedDate)
                return "Нет курсов для выбранной даты";
            return $"Курс на {state.Rates.Date:dd.MM.yyyy}. Выбрано: {SelectedDate:dd.MM.yyyy}.";
        }
    }

    public DateTime SelectedDate
    {
        get => state.SelectedDate;
        set
        {
            if (IsBusy || state.SelectedDate == value.Date) return;
            state.SelectedDate = value.Date;
            if (initialized) _ = LoadRatesAsync();
            else Notify();
        }
    }

    public Currency? FromCurrency
    {
        get => Currencies.Find(c => c.Code == state.FromCode);
        set
        {
            if (IsBusy || value == null || value.Code == state.FromCode) return;
            state.FromCode = value.Code;
            Update();
        }
    }

    public Currency? ToCurrency
    {
        get => Currencies.Find(c => c.Code == state.ToCode);
        set
        {
            if (IsBusy || value == null || value.Code == state.ToCode) return;
            state.ToCode = value.Code;
            Update();
        }
    }

    public string FromAmount
    {
        get => state.FromAmount;
        set
        {
            if (IsBusy || state.FromAmount == value) return;
            state.FromAmount = value ?? "";
            state.LastEditedFrom = true;
            Update();
        }
    }

    public string ToAmount
    {
        get => state.ToAmount;
        set
        {
            if (IsBusy || state.ToAmount == value) return;
            state.ToAmount = value ?? "";
            state.LastEditedFrom = false;
            Update();
        }
    }

    public async Task InitializeAsync()
    {
        if (initialized) return;
        initialized = true;
        try
        {
            state = stateStore.Load() ?? state;
        }
        catch (Exception)
        {
            state = new SavedState();
        }
        await LoadRatesAsync();
    }

    public async Task LoadRatesAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = "";
        Notify();
        try
        {
            state.Rates = await ratesService.GetRatesAsync(SelectedDate);
            if (FromCurrency == null) state.FromCode = Currencies[0].Code;
            if (ToCurrency == null) state.ToCode = Currencies[0].Code;
        }
        catch (Exception)
        {
            ErrorMessage = "Не удалось обновить курсы. Проверьте сервер и интернет. " +
                "Если есть сохранённые курсы, используется их дата, указанная выше.";
        }
        finally
        {
            IsBusy = false;
            Update();
        }
    }

    // Общие действия после изменения суммы, валюты или курсов.
    private void Update()
    {
        Recalculate();
        if (initialized)
        {
            try
            {
                stateStore.Save(state);
            }
            catch (Exception)
            {
                ErrorMessage = "Не удалось сохранить данные на устройстве.";
            }
        }
        Notify();
    }

    private void Recalculate()
    {
        InputError = "";
        string result = "";
        string input = state.LastEditedFrom ? FromAmount : ToAmount;

        if (CanConvert && !string.IsNullOrWhiteSpace(input))
        {
            bool valid = decimal.TryParse(input.Trim().Replace(',', '.'),
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out decimal amount);

            if (!valid || amount < 0)
                InputError = "Введите неотрицательное число, например 100,50.";
            else
            {
                try
                {
                    decimal fromRate = FromCurrency!.RublesPerUnit;
                    decimal toRate = ToCurrency!.RublesPerUnit;
                    decimal converted = state.LastEditedFrom
                        ? amount * fromRate / toRate
                        : amount * toRate / fromRate;
                    result = converted.ToString("0.########", CultureInfo.GetCultureInfo("ru-RU"));
                }
                catch (OverflowException)
                {
                    InputError = "Слишком большое число.";
                }
            }
        }

        if (state.LastEditedFrom) state.ToAmount = result;
        else state.FromAmount = result;
    }
}
