using CommunityToolkit.Mvvm.ComponentModel;

namespace Plataforma.Launcher.ViewModels;

public partial class StoreItemViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Price { get; set; } = "R$ 0,00";
}

public partial class StoreGameViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Slug { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Genre { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Price { get; set; } = "R$ 0,00";

    [ObservableProperty]
    public partial string Discount { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CoverInitial { get; set; } = "G";

    [ObservableProperty]
    public partial bool IsPurchased { get; set; }

    [ObservableProperty]
    public partial bool IsSandboxManaged { get; set; }

    public bool CanPurchase => !IsPurchased && !IsSandboxManaged;

    partial void OnIsPurchasedChanged(bool value)
    {
        OnPropertyChanged(nameof(CanPurchase));
    }

    partial void OnIsSandboxManagedChanged(bool value)
    {
        OnPropertyChanged(nameof(CanPurchase));
    }

    public ObservableStoreItems Items { get; } = new();
}

public sealed class ObservableStoreItems : System.Collections.ObjectModel.ObservableCollection<StoreItemViewModel>;
