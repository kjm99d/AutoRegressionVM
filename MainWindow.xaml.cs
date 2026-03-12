using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AutoRegressionVM
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 로그 자동 스크롤
            var vm = DataContext as ViewModels.MainViewModel;
            if (vm != null)
            {
                vm.Logs.CollectionChanged += (s, args) =>
                {
                    if (lstLogs.Items.Count > 0)
                    {
                        lstLogs.ScrollIntoView(lstLogs.Items[lstLogs.Items.Count - 1]);
                    }
                };
            }
        }
    }
}
