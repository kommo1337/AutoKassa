using AutoKassa.ViewModels;
using System;
using System.Windows;

namespace AutoKassa.Views
{
    /// <summary>
    /// Логика взаимодействия для YandexClientIdDialogView.xaml
    /// </summary>
    public partial class YandexClientIdDialogView : Window
    {
        private readonly YandexClientIdDialogViewModel _viewModel;

        public YandexClientIdDialogView(YandexClientIdDialogViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;

            _viewModel.OnClose = (result) =>
            {
                DialogResult = result;
                Close();
            };

            Closed += OnClosed;
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            _viewModel.OnClose = null;
            Closed -= OnClosed;
            _viewModel.Dispose();
        }
    }
}
