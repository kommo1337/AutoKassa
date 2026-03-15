using AutoKassa.Helpers;
using AutoKassa.Models;
using AutoKassa.Services;
using System.Collections.Generic;
using System.Windows.Input;

namespace AutoKassa.ViewModels
{
    public class CategoryItemViewModel : ViewModelBase
    {
        private readonly ICategoryService _categoryService;
        private string _name;
        private string _color;
        private bool _isRenaming;
        private bool _isColorPickerOpen;
        private string _originalName;

        public static readonly List<string> PresetColors = new()
        {
            "#6366f1", "#f59e0b", "#14b8a6", "#94a3b8", "#ec4899",
            "#f97316", "#8b5cf6", "#06b6d4", "#84cc16", "#ef4444",
            "#10b981", "#3b82f6"
        };

        public CategoryItemViewModel(Category model, ICategoryService categoryService, Action<CategoryItemViewModel> onDelete)
        {
            Model = model;
            _categoryService = categoryService;
            _name = model.Name;
            _color = model.Color;

            StartRenameCommand = new RelayCommand(_ => StartRename());
            CommitRenameCommand = new RelayCommand(async _ => await CommitRenameAsync());
            CancelRenameCommand = new RelayCommand(_ => CancelRename());
            ToggleColorPickerCommand = new RelayCommand(_ => IsColorPickerOpen = !IsColorPickerOpen);
            SelectColorCommand = new RelayCommand(async p => await SelectColorAsync(p as string));
            DeleteCommand = new RelayCommand(_ => onDelete?.Invoke(this));
        }

        public Category Model { get; }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Color
        {
            get => _color;
            set => SetProperty(ref _color, value);
        }

        public bool IsRenaming
        {
            get => _isRenaming;
            set => SetProperty(ref _isRenaming, value);
        }

        public bool IsColorPickerOpen
        {
            get => _isColorPickerOpen;
            set => SetProperty(ref _isColorPickerOpen, value);
        }

        public bool IsSystem => Model.IsSystem;
        public bool IsActive => Model.IsActive;

        public ICommand StartRenameCommand { get; }
        public ICommand CommitRenameCommand { get; }
        public ICommand CancelRenameCommand { get; }
        public ICommand ToggleColorPickerCommand { get; }
        public ICommand SelectColorCommand { get; }
        public ICommand DeleteCommand { get; }

        private void StartRename()
        {
            _originalName = Name;
            IsRenaming = true;
        }

        private async Task CommitRenameAsync()
        {
            IsRenaming = false;
            var trimmed = Name?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                Name = _originalName;
                return;
            }

            Name = trimmed;
            if (trimmed != _originalName)
            {
                Model.Name = trimmed;
                await _categoryService.UpdateAsync(Model);
            }
        }

        private void CancelRename()
        {
            Name = _originalName;
            IsRenaming = false;
        }

        private async Task SelectColorAsync(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return;
            Color = hex;
            Model.Color = hex;
            IsColorPickerOpen = false;
            await _categoryService.UpdateAsync(Model);
        }
    }
}
