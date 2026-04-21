/*
 * CategoryToggleViewModel.cs - один чип-категория в UI
 *
 * IsChecked = все правила категории присутствуют в целевом списке.
 * Клик пользователя добавляет (или убирает) правила категории целиком.
 */

using CommunityToolkit.Mvvm.ComponentModel;
using VlessVPN.Models;

namespace VlessVPN.ViewModels;

public sealed class CategoryToggleViewModel : ObservableObject
{
    public GeoCategory Category { get; }
    public bool IsBypass { get; }

    /// <summary>Не применять пользовательский клик — используется при обновлении IsChecked из текстового списка.</summary>
    public bool SuppressToggle { get; set; }

    /// <summary>Вызывается при ручном клике пользователя: параметр — новое состояние.</summary>
    public Action<CategoryToggleViewModel, bool>? OnUserToggled { get; set; }

    public string Display => $"{Category.Emoji} {Category.DisplayName}";

    public string Tooltip =>
        $"{Category.DisplayName}\n" +
        string.Join('\n', Category.Rules);

    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value) return;
            _isChecked = value;
            OnPropertyChanged();
            if (!SuppressToggle)
                OnUserToggled?.Invoke(this, value);
        }
    }

    public CategoryToggleViewModel(GeoCategory category, bool isBypass)
    {
        Category = category;
        IsBypass = isBypass;
    }

    public void SetCheckedSilently(bool value)
    {
        if (_isChecked == value) return;
        SuppressToggle = true;
        try
        {
            _isChecked = value;
            OnPropertyChanged(nameof(IsChecked));
        }
        finally { SuppressToggle = false; }
    }
}
