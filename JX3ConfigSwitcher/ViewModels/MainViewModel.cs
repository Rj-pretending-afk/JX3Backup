using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JX3ConfigSwitcher.Models;
using JX3ConfigSwitcher.Services;
using JX3ConfigSwitcher.Views;
using MessageBox = System.Windows.MessageBox;

namespace JX3ConfigSwitcher.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly PortablePaths _paths;
    private readonly SettingsService _settingsService;
    private readonly ProfileRepository _repository;
    private readonly GameScanner _scanner;
    private readonly ConfigClassifier _classifier;
    private readonly BackupService _backupService;
    private readonly SyncService _syncService;

    public MainViewModel(
        PortablePaths paths,
        SettingsService settingsService,
        ProfileRepository repository,
        GameScanner scanner,
        ConfigClassifier classifier,
        BackupService backupService,
        SyncService syncService)
    {
        _paths = paths;
        _settingsService = settingsService;
        _repository = repository;
        _scanner = scanner;
        _classifier = classifier;
        _backupService = backupService;
        _syncService = syncService;
        SelectedSaveKind = SaveKind.Universal;
        ResetModuleChoices();
    }

    public ObservableCollection<ProfileRecord> Profiles { get; } = new();
    public ObservableCollection<SlotViewModel> Slots { get; } = new();
    public ObservableCollection<CharacterConfig> Characters { get; } = new();
    public ObservableCollection<BackupVersionRecord> RecentBackups { get; } = new();
    public ObservableCollection<OperationLogRecord> Logs { get; } = new();
    public ObservableCollection<ModuleChoiceViewModel> ModuleChoices { get; } = new();
    public ObservableCollection<string> ScanRoots { get; } = new();
    public ObservableCollection<string> SyncConflicts { get; } = new();

    [ObservableProperty]
    private ProfileRecord? selectedProfile;

    [ObservableProperty]
    private SlotViewModel? selectedSlot;

    [ObservableProperty]
    private CharacterConfig? selectedCharacter;

    [ObservableProperty]
    private BackupVersionRecord? selectedBackup;

    [ObservableProperty]
    private SaveKind selectedSaveKind;

    [ObservableProperty]
    private string gamePath = string.Empty;

    [ObservableProperty]
    private string syncFolder = string.Empty;

    [ObservableProperty]
    private string slotNameDraft = string.Empty;

    [ObservableProperty]
    private string sectTagDraft = string.Empty;

    [ObservableProperty]
    private string noteDraft = string.Empty;

    [ObservableProperty]
    private string statusMessage = "准备就绪。";

    [ObservableProperty]
    private string riskSummary = "选择备份包和目标角色后会显示恢复风险。";

    [ObservableProperty]
    private bool isBusy;

    public string DataDirectory => _paths.DataDirectory;

    public IReadOnlyList<SaveKind> SaveKinds { get; } = new[] { SaveKind.Universal, SaveKind.CharacterSpecific };

    public void Initialize()
    {
        GamePath = _settingsService.Settings.GamePath ?? string.Empty;
        SyncFolder = _settingsService.Settings.SyncFolder ?? string.Empty;
        LoadProfiles();
        BuildSlotGrid();
        RefreshRecentBackups();
        RefreshLogs();
        ScanGamePaths();
    }

    partial void OnSelectedProfileChanged(ProfileRecord? value)
    {
        _settingsService.Settings.CurrentProfileId = value?.Id;
        _settingsService.Save();
        BuildSlotGrid();
        RefreshRecentBackups();
    }

    partial void OnSelectedSlotChanged(SlotViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        SlotNameDraft = value.HasData ? value.Name : $"保存档 {value.Number:00}";
        SelectedSaveKind = value.HasData ? value.Kind : SaveKind.Universal;
        SectTagDraft = value.SectTag ?? string.Empty;
    }

    partial void OnSelectedSaveKindChanged(SaveKind value)
    {
        ResetModuleChoices();
    }

    partial void OnSelectedBackupChanged(BackupVersionRecord? value)
    {
        UpdateRiskSummary();
    }

    [RelayCommand]
    private void CreateProfile()
    {
        var name = InputDialog.Ask("新建 Profile", "输入用户名称，例如：我、她、朋友A", "");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            var profile = _repository.CreateProfile(name);
            Profiles.Add(profile);
            SelectedProfile = profile;
            StatusMessage = $"已创建 profile：{profile.Name}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "创建失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void ChooseGamePath()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择剑网3根目录或 userdata 目录",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(GamePath) ? GamePath : string.Empty
        };
        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        GamePath = dialog.SelectedPath;
        _settingsService.Settings.GamePath = GamePath;
        _settingsService.Save();
        ScanCharacters();
    }

    [RelayCommand]
    private void ChooseSyncFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择 OneDrive/Google Drive 本地同步文件夹",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(SyncFolder) ? SyncFolder : string.Empty
        };
        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        SyncFolder = dialog.SelectedPath;
        _settingsService.Settings.SyncFolder = SyncFolder;
        _settingsService.Save();
        RefreshSyncConflicts();
    }

    [RelayCommand]
    private void ScanGamePaths()
    {
        ScanRoots.Clear();
        foreach (var root in _scanner.FindGameRoots(GamePath))
        {
            ScanRoots.Add(root);
        }

        if (string.IsNullOrWhiteSpace(GamePath) && ScanRoots.FirstOrDefault() is { } firstRoot)
        {
            GamePath = firstRoot;
            _settingsService.Settings.GamePath = firstRoot;
            _settingsService.Save();
        }

        ScanCharacters();
    }

    [RelayCommand]
    private void ScanCharacters()
    {
        Characters.Clear();
        if (string.IsNullOrWhiteSpace(GamePath))
        {
            StatusMessage = "未设置游戏路径。";
            return;
        }

        foreach (var character in _scanner.ScanCharacters(GamePath))
        {
            Characters.Add(character);
        }

        SelectedCharacter = Characters.FirstOrDefault();
        StatusMessage = $"扫描完成：{Characters.Count} 个角色。";
    }

    [RelayCommand]
    private async Task CreateBackup()
    {
        if (!EnsureCanBackup())
        {
            return;
        }

        IsBusy = true;
        try
        {
            var modules = SelectedModules();
            var profile = SelectedProfile!;
            var slot = SelectedSlot!;
            var character = SelectedCharacter!;
            await Task.Run(() => _backupService.CreateBackup(
                profile,
                slot.Number,
                string.IsNullOrWhiteSpace(SlotNameDraft) ? $"保存档 {slot.Number:00}" : SlotNameDraft,
                SelectedSaveKind,
                character,
                modules,
                string.IsNullOrWhiteSpace(SectTagDraft) ? null : SectTagDraft,
                NoteDraft));
            StatusMessage = "备份完成。";
            BuildSlotGrid();
            RefreshRecentBackups();
            RefreshLogs();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "备份失败", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage = "备份失败。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RestoreBackup()
    {
        if (SelectedProfile is null || SelectedBackup is null || SelectedCharacter is null)
        {
            MessageBox.Show("请选择 profile、备份版本和目标角色。", "无法恢复", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var modules = SelectedModules();
        if (modules.Count == 0)
        {
            MessageBox.Show("至少选择一个要恢复的模块。", "无法恢复", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        UpdateRiskSummary();
        var confirm = MessageBox.Show(
            $"恢复前会自动创建目标角色快照。\n\n{RiskSummary}\n\n确认恢复到：{SelectedCharacter.DisplayName}？",
            "确认恢复",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var profile = SelectedProfile;
            var target = SelectedCharacter;
            var backup = SelectedBackup;
            await Task.Run(() =>
            {
                _backupService.CreateAutoSnapshot(profile, target, $"恢复 {Path.GetFileName(backup.PackagePath)} 前自动快照");
                _backupService.RestoreBackup(backup.PackagePath, target, modules);
            });
            StatusMessage = "恢复完成，已先创建自动快照。";
            RefreshRecentBackups();
            RefreshLogs();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "恢复失败", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage = "恢复失败。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SyncSelectedBackup()
    {
        if (SelectedBackup is null)
        {
            MessageBox.Show("请选择一个备份版本。", "无法同步", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var destination = _syncService.SyncBackupToFolder(SelectedBackup, SyncFolder);
            StatusMessage = $"已同步：{destination}";
            RefreshSyncConflicts();
            RefreshLogs();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "同步失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = _paths.DataDirectory,
            UseShellExecute = true
        });
    }

    private void LoadProfiles()
    {
        Profiles.Clear();
        foreach (var profile in _repository.GetProfiles())
        {
            Profiles.Add(profile);
        }

        SelectedProfile = Profiles.FirstOrDefault(profile => profile.Id == _settingsService.Settings.CurrentProfileId)
            ?? Profiles.FirstOrDefault();
    }

    private void BuildSlotGrid()
    {
        Slots.Clear();
        var slots = SelectedProfile is null
            ? Array.Empty<SaveSlotRecord>()
            : _repository.GetSlots(SelectedProfile.Id).ToArray();

        for (var index = 1; index <= 99; index++)
        {
            var viewModel = new SlotViewModel(index);
            var record = slots.FirstOrDefault(slot => slot.SlotNumber == index);
            if (record is not null)
            {
                viewModel.Apply(record);
            }

            Slots.Add(viewModel);
        }

        SelectedSlot = Slots.FirstOrDefault(slot => slot.HasData) ?? Slots.FirstOrDefault();
    }

    private void RefreshRecentBackups()
    {
        RecentBackups.Clear();
        foreach (var backup in _repository.GetRecentBackups(SelectedProfile?.Id, 80))
        {
            RecentBackups.Add(backup);
        }

        SelectedBackup ??= RecentBackups.FirstOrDefault();
    }

    private void RefreshLogs()
    {
        Logs.Clear();
        foreach (var log in _repository.GetLogs())
        {
            Logs.Add(log);
        }
    }

    private void RefreshSyncConflicts()
    {
        SyncConflicts.Clear();
        foreach (var conflict in _syncService.FindPotentialConflicts(SyncFolder))
        {
            SyncConflicts.Add(conflict);
        }
    }

    private void ResetModuleChoices()
    {
        ModuleChoices.Clear();
        foreach (var choice in _classifier.GetDefaultChoices(SelectedSaveKind))
        {
            ModuleChoices.Add(new ModuleChoiceViewModel(choice));
        }

        UpdateRiskSummary();
    }

    private void UpdateRiskSummary()
    {
        if (SelectedBackup is null)
        {
            RiskSummary = "选择备份包后会显示恢复风险。";
            return;
        }

        try
        {
            RiskSummary = _backupService.GetRiskSummary(SelectedBackup.PackagePath, SelectedModules());
        }
        catch (Exception ex)
        {
            RiskSummary = $"风险摘要读取失败：{ex.Message}";
        }
    }

    private bool EnsureCanBackup()
    {
        if (SelectedProfile is null || SelectedSlot is null || SelectedCharacter is null)
        {
            MessageBox.Show("请选择 profile、保存档 slot 和来源角色。", "无法备份", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (SelectedModules().Count == 0)
        {
            MessageBox.Show("至少选择一个要备份的模块。", "无法备份", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        return true;
    }

    private IReadOnlyList<ConfigModule> SelectedModules()
    {
        return ModuleChoices
            .Where(choice => choice.IsSelected)
            .Select(choice => choice.Module)
            .Distinct()
            .ToList();
    }
}
