using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace LamisPlusModulesInstaller.GUI.Wpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            LogsTextBox.TextChanged += (s, e) =>
            {
                LogsTextBox.ScrollToEnd();
            };

            // Subscribe to module installing event to auto-scroll
            _viewModel.ModuleInstalling += OnModuleInstalling;
        }

        private void OnModuleInstalling(ModuleViewModel module)
        {
            // Scroll to the installing module in the DataGrid
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                if (module != null)
                {
                    ModulesDataGrid.ScrollIntoView(module);
                    ModulesDataGrid.SelectedItem = module;
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        //event handler for check box if module is selected
        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox checkBox)
                checkBox.GetBindingExpression(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty)?.UpdateSource();
        }

        private void Button_Click()
        {

        }
    }
}