using CommunityToolkit.Mvvm.ComponentModel;

namespace LamisPlusModulesInstaller.GUI.Wpf
{
    public partial class ModuleViewModel : ObservableObject
    {
        [ObservableProperty] private string name = string.Empty;
        [ObservableProperty] private string localVersion = string.Empty;
        [ObservableProperty] private string installedVersion = string.Empty;
        [ObservableProperty] private string status = string.Empty;
        [ObservableProperty] private string localPath = string.Empty;
        [ObservableProperty] private bool isSelected;
    }
}
