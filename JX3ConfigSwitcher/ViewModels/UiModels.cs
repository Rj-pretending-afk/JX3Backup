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
    private SaveKind kind = SaveKind.Universal;

    [ObservableProperty]
    private string? characterKey;

    [ObservableProperty]
    private string? sectTag;

    [ObservableProperty]
    private bool hasData;

    public string NumberText => Number.ToString("00");
    public string KindText => Kind switch
    {
        SaveKind.Universal => "通用",
        SaveKind.CharacterSpecific => "角色",
        SaveKind.AutoSnapshot => "快照",
        _ => Kind.ToString()
    };

    public void Apply(SaveSlotRecord record)
    {
        Name = record.Name;
        Kind = record.Kind;
        CharacterKey = record.CharacterKey;
        SectTag = record.SectTag;
        HasData = true;
        OnPropertyChanged(nameof(KindText));
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
