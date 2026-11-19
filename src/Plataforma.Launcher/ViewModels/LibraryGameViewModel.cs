using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Plataforma.Launcher.ViewModels;

public partial class LibraryGameViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Slug { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Genre { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Status { get; set; } = "Pronto para jogar";

    [ObservableProperty]
    public partial string LastPlayed { get; set; } = "Primeira vez";

    [ObservableProperty]
    public partial string PlayTime { get; set; } = "0 horas";

    [ObservableProperty]
    public partial string InstalledVersion { get; set; } = "Não instalado";

    [ObservableProperty]
    public partial bool IsInstalled { get; set; }

    [ObservableProperty]
    public partial bool IsWindowsBuildAvailable { get; set; }

    [ObservableProperty]
    public partial bool IsWebGlBuildAvailable { get; set; }

    [ObservableProperty]
    public partial bool IsExecutableSelectionRequired { get; set; }

    [ObservableProperty]
    public partial string? SelectedExecutablePath { get; set; }

    public ObservableCollection<string> ExecutableCandidates { get; } = [];

    public bool CanLaunchInstalledBuild =>
        IsInstalled && !string.IsNullOrWhiteSpace(SelectedExecutablePath);

    partial void OnIsInstalledChanged(bool value)
    {
        OnPropertyChanged(nameof(CanLaunchInstalledBuild));
    }

    partial void OnSelectedExecutablePathChanged(string? value)
    {
        OnPropertyChanged(nameof(CanLaunchInstalledBuild));
    }

    public static LibraryGameViewModel CreateTemplate()
    {
        return new LibraryGameViewModel
        {
            Title = "Dash & Crash",
            Slug = "dash-and-crash",
            Genre = "Ação / Corrida",
            Description = "Modelo visual para a página de lançamento. Este template será usado para apresentar detalhes, builds, conquistas e ações do jogo.",
            Status = "Disponível",
            LastPlayed = "Nunca iniciado",
            PlayTime = "0 horas",
            InstalledVersion = "1.0.0"
        };
    }
}
