using CommunityToolkit.Mvvm.ComponentModel;
using JX3ConfigSwitcher.Models;

namespace JX3ConfigSwitcher.ViewModels;

public sealed partial class SlotViewModel : ObservableObject
{
    public SlotViewModel(int number)
    {
        Number = number;
        Name = $"保存档 {number:00}";
    }

    public int Number { get; }

    [ObservableProperty]
    private string name;

    [ObservableProperty]
    private SaveKind kind = SaveKind.CharacterSpecific;

    [ObservableProperty]
    private string? characterKey;

    [ObservableProperty]
    private string? sectTag;

    [ObservableProperty]
    private string sectColor = "#4CC9F0";

    [ObservableProperty]
    private bool hasData;

    [ObservableProperty]
    private bool isFavorite;

    [ObservableProperty]
    private bool isMatched;

    public string NumberText => Number.ToString("00");
    public string KindText => Kind is SaveKind.AutoSnapshot ? "自动快照" : "手动保存";
    public string FavoriteText => IsFavorite ? "★" : "☆";
    public string MatchText => IsMatched ? "匹配" : string.Empty;

    public void Apply(SaveSlotRecord record)
    {
        Name = record.Name;
        Kind = record.Kind;
        CharacterKey = record.CharacterKey;
        SectTag = record.SectTag;
        SectColor = string.IsNullOrWhiteSpace(record.SectColor) ? "#4CC9F0" : record.SectColor;
        IsFavorite = record.IsFavorite;
        HasData = true;
        OnPropertyChanged(nameof(KindText));
        OnPropertyChanged(nameof(FavoriteText));
    }

    public void SetMatched(bool value)
    {
        IsMatched = value;
        OnPropertyChanged(nameof(MatchText));
    }

    partial void OnIsFavoriteChanged(bool value)
    {
        OnPropertyChanged(nameof(FavoriteText));
    }
}

public sealed partial class ModuleChoiceViewModel : ObservableObject
{
    public ModuleChoiceViewModel(ModuleChoice choice)
    {
        Module = choice.Module;
        Name = choice.Name;
        Description = choice.Description;
        IsSelected = choice.IsSelected;
        IsHighRisk = choice.IsHighRisk;
    }

    public ConfigModule Module { get; }
    public string Name { get; }
    public string Description { get; }
    public bool IsHighRisk { get; }

    [ObservableProperty]
    private bool isSelected;
}
