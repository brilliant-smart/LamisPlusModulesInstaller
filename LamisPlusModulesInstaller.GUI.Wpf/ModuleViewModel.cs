using CommunityToolkit.Mvvm.ComponentModel;

namespace LamisPlusModulesInstaller.GUI.Wpf
{
    public partial class ModuleViewModel : ObservableObject
    {
        [ObservableProperty] private string name;
        [ObservableProperty] private string localVersion;
        [ObservableProperty] private string installedVersion;
        [ObservableProperty] private string status;
        [ObservableProperty] private string localPath;
        [ObservableProperty] private bool isSelected;
    }
}
