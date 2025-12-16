using System.Windows;
using AutoRegressionVM.Models;
using Microsoft.Win32;

namespace AutoRegressionVM.Views
{
    public partial class AddVMDialog : Window
    {
        public VMInfo Result { get; private set; }

        public AddVMDialog()
        {
            InitializeComponent();
        }

        private void BrowseVmx_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "VMware Virtual Machine (*.vmx)|*.vmx",
                Title = "VMX 파일 선택"
            };

            if (dialog.ShowDialog() == true)
            {
                txtVmxPath.Text = dialog.FileName;

                if (string.IsNullOrWhiteSpace(txtVMName.Text))
                {
                    txtVMName.Text = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
                }
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtVMName.Text))
            {
                MessageBox.Show("VM 이름을 입력하세요.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtVmxPath.Text))
            {
                MessageBox.Show("VMX 파일 경로를 선택하세요.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Result = new VMInfo
            {
                Name = txtVMName.Text.Trim(),
                VmxPath = txtVmxPath.Text.Trim(),
                GuestUsername = txtUsername.Text.Trim(),
                GuestPassword = txtPassword.Password
            };

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
