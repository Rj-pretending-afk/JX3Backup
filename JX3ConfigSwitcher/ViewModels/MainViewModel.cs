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
    private readonly IBackupProfileHostApi _profileHostApi;
    private readonly List<CharacterConfig> _allCharacters = new();

    public MainViewModel(
        PortablePaths paths,
        SettingsService settingsService,
        ProfileRepository repository,
        GameScanner scanner,
        ConfigClassifier classifier,
        BackupService backupService,
        SyncService syncService,
        IBackupProfileHostApi? profileHostApi = null)
    {
        _paths = paths;
        _settingsService = settingsService;
        _repository = repository;
        _scanner = scanner;
        _classifier = classifier;
        _backupService = backupService;
        _syncService = syncService;
        _profileHostApi = profileHostApi ?? new DialogBackupProfileHostApi();
        SelectedSaveKind = SaveKind.CharacterSpecific;
        SelectedSectOption = SectCatalog.Default;
        ResetModuleChoices();
    }

    public ObservableCollection<ProfileRecord> Profiles { get; } = new();
    public ObservableCollection<SlotViewModel> Slots { get; } = new();
    public ObservableCollection<CharacterConfig> Characters { get; } = new();
    public ObservableCollection<BackupVersionRecord> RecentBackups { get; } = new();
    public ObservableCollection<OperationLogRecord> Logs { get; } = new();
    public ObservableCollection<ModuleChoiceViewModel> ModuleChoices { get; } = new();
    public ObservableCollection<SlotViewModel> CoverSourceSlots { get; } = new();
    public ObservableCollection<SlotViewModel> FavoriteSlots { get; } = new();
    public ObservableCollection<string> ScanRoots { get; } = new();
    public ObservableCollection<string> SyncConflicts { get; } = new();

    [ObservableProperty]
    private ProfileRecord? selectedProfile;

    [ObservableProperty]
    private SlotViewModel? selectedSlot;

    [ObservableProperty]
    private CharacterConfig? selectedCharacter;

    [ObservableProperty]
    private CharacterConfig? crossSourceCharacter;

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
    private SectOption? selectedSectOption;

    [ObservableProperty]
    private string noteDraft = string.Empty;

    [ObservableProperty]
    private string statusMessage = "准备就绪。";

    [ObservableProperty]
    private string riskSummary = "选择备份包和目标角色后会显示恢复风险。";

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool showCurrentProfileOnly;

    public string DataDirectory => _paths.DataDirectory;

    public double WindowWidth => _settingsService.Settings.WindowWidth;

    public double WindowHeight => _settingsService.Settings.WindowHeight;

    public IReadOnlyList<SaveKind> SaveKinds { get; } = new[] { SaveKind.CharacterSpecific };

    public IReadOnlyList<SectOption> SectOptions => SectCatalog.All;

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

    public void SaveAndDispose(double? windowWidth = null, double? windowHeight = null)
    {
        if (windowWidth is > 0)
        {
            _settingsService.Settings.WindowWidth = windowWidth.Value;
        }

        if (windowHeight is > 0)
        {
            _settingsService.Settings.WindowHeight = windowHeight.Value;
        }

        _settingsService.Save();
    }

    partial void OnSelectedProfileChanged(ProfileRecord? value)
    {
        _settingsService.Settings.CurrentProfileId = value?.Id;
        _settingsService.Save();
        ApplyCharacterFilter();
        BuildSlotGrid();
        RefreshRecentBackups();
    }

    partial void OnShowCurrentProfileOnlyChanged(bool value)
    {
        ApplyCharacterFilter();
    }

    partial void OnSelectedSlotChanged(SlotViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        SlotNameDraft = value.HasData ? value.Name : $"保存档 {value.Number:00}";
        SelectedSaveKind = SaveKind.CharacterSpecific;
        SectTagDraft = value.SectTag ?? string.Empty;
        SelectedSectOption = SectCatalog.Find(value.SectTag)
            ?? SectCatalog.FindByColor(value.SectColor)
            ?? SectCatalog.Default;
        if (!value.HasData)
        {
            ApplyCharacterSect(SelectedCharacter);
        }
    }

    partial void OnSelectedCharacterChanged(CharacterConfig? value)
    {
        if (SelectedSlot is null || !SelectedSlot.HasData)
        {
            ApplyCharacterSect(value);
        }

        RefreshRecentBackups();
        RefreshCoverSources();
        ResetModuleChoices();
    }

    partial void OnSelectedSectOptionChanged(SectOption? value)
    {
        if (value is not null)
        {
            SectTagDraft = value.Tag;
        }
    }

    partial void OnCrossSourceCharacterChanged(CharacterConfig? value)
    {
        ResetModuleChoices();
    }

    partial void OnSelectedSaveKindChanged(SaveKind value)
    {
        ResetModuleChoices();
    }

    partial void OnSelectedBackupChanged(BackupVersionRecord? value)
    {
        ResetModuleChoices();
    }

    [RelayCommand]
    private void CreateProfile()
    {
        var name = _profileHostApi.RequestCreateProfileName(Profiles.ToList());
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            var profile = _repository.CreateProfile(name);
            Profiles.Add(profile);
            SelectedProfile = profile;
            _profileHostApi.OnProfileCreated(profile);
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
        _allCharacters.Clear();
        if (string.IsNullOrWhiteSpace(GamePath))
        {
            Characters.Clear();
            StatusMessage = "未设置游戏路径。";
            return;
        }

        foreach (var character in _scanner.ScanCharacters(GamePath))
        {
            _allCharacters.Add(character);
        }

        ApplyCharacterFilter();
        CrossSourceCharacter = null;
        StatusMessage = $"扫描完成：{Characters.Count} 个角色。";
    }

    private void ApplyCharacterFilter()
    {
        var selectedKey = SelectedCharacter?.Key;
        Characters.Clear();

        IEnumerable<CharacterConfig> source = _allCharacters;
        if (ShowCurrentProfileOnly && SelectedProfile is not null)
        {
            var owned = _profileHostApi.GetOwnedCharacterKeys(SelectedProfile.Name);
            source = source.Where(character => owned.Contains(character.Key));
        }

        foreach (var character in source)
        {
            Characters.Add(character);
        }

        SelectedCharacter = Characters.FirstOrDefault(character => string.Equals(character.Key, selectedKey, StringComparison.OrdinalIgnoreCase))
            ?? Characters.FirstOrDefault();
        CrossSourceCharacter = null;
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
                SelectedSectOption?.Color,
                NoteDraft));
            StatusMessage = "备份完成。";
            BuildSlotGrid();
            RefreshRecentBackups();
            RefreshCoverSources();
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
    private async Task CopyFromCharacter()
    {
        if (SelectedProfile is null || CrossSourceCharacter is null || SelectedCharacter is null)
        {
            MessageBox.Show("请选择 profile、来源角色和目标角色。", "无法跨角色覆盖", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var modules = SelectedModules();
        if (modules.Count == 0)
        {
            MessageBox.Show("至少选择一个要覆盖的模块。", "无法跨角色覆盖", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"会先自动备份目标角色，再从来源角色覆盖所选模块。\n\n来源：{CrossSourceCharacter.DisplayName}\n目标：{SelectedCharacter.DisplayName}\n\n模块：{string.Join(", ", modules.Select(BackupService.GetModuleName))}\n\n确认继续？",
            "确认跨角色覆盖",
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
            var source = CrossSourceCharacter;
            var target = SelectedCharacter;
            await Task.Run(() => _backupService.CopyCharacterConfig(
                profile,
                source,
                target,
                modules,
                $"跨角色覆盖 {source.DisplayName} -> {target.DisplayName}"));
            StatusMessage = "跨角色覆盖完成，已自动备份目标角色。";
            RefreshRecentBackups();
            RefreshLogs();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "跨角色覆盖失败", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage = "跨角色覆盖失败。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ToggleSlotFavorite(SlotViewModel? slot)
    {
        if (SelectedProfile is null || slot is null || !slot.HasData)
        {
            return;
        }

        var next = !slot.IsFavorite;
        _repository.SetSlotFavorite(SelectedProfile.Id, slot.Number, next);
        slot.IsFavorite = next;
        RefreshCoverSources();
    }

    public async Task CoverSlotToCharacterAsync(int slotNumber, CharacterConfig target)
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var slot = Slots.FirstOrDefault(candidate => candidate.Number == slotNumber && candidate.HasData);
        if (slot is null)
        {
            MessageBox.Show("这个保存档还没有手动保存内容。", "无法覆盖", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var backup = _repository
            .GetRecentBackups(SelectedProfile.Id, 500)
            .Where(record => record.SlotNumber == slotNumber && record.Kind != SaveKind.AutoSnapshot)
            .OrderByDescending(record => record.CreatedAt)
            .FirstOrDefault();
        if (backup is null)
        {
            MessageBox.Show("没有找到这个保存档对应的备份版本。", "无法覆盖", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var manifest = _backupService.ReadManifest(backup.PackagePath);
        var modules = manifest.IncludedModules.Distinct().ToList();
        var isMatch = SlotMatchesCharacter(slot, target);
        if (modules.Count == 0)
        {
            MessageBox.Show("这个保存档没有可覆盖的模块。", "无法覆盖", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"把保存档 {slot.NumberText} 覆盖到角色：\n{target.DisplayName}\n\n{(isMatch ? "门派/心法匹配，将按保存档模块覆盖；如果保存档包含完整 dump，也会一并写回。" : "门派/心法不匹配，但仍会按保存档模块覆盖。注意：如果保存档包含宏、技能/动作按钮或完整 dump，目标角色对应内容也会被覆盖。")}\n\n覆盖前会自动备份目标角色。继续？",
            "确认拖拽覆盖",
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
            await Task.Run(() =>
            {
                _backupService.CreateAutoSnapshot(profile, target, $"拖拽覆盖 Slot {slot.NumberText} 前自动快照");
                _backupService.RestoreBackup(backup.PackagePath, target, modules);
            });
            SelectedCharacter = target;
            StatusMessage = $"已用保存档 {slot.NumberText} 覆盖 {target.CharacterName}。";
            RefreshRecentBackups();
            RefreshLogs();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "拖拽覆盖失败", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage = "拖拽覆盖失败。";
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

    public void ReloadProfilesFromHost()
    {
        LoadProfiles();
    }

    private void LoadProfiles()
    {
        Profiles.Clear();
        var hostProfileNames = _profileHostApi.GetHostProfileNames()
                     .Where(name => !string.IsNullOrWhiteSpace(name))
                     .Select(name => name.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .ToList();
        var existingProfiles = _repository.GetProfiles().ToList();
        foreach (var name in hostProfileNames
                     .Where(name => existingProfiles.All(profile => !string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase))))
        {
            _repository.CreateProfile(name);
        }

        var profiles = _repository.GetProfiles();
        if (hostProfileNames.Count > 0)
        {
            profiles = profiles
                .Where(profile => hostProfileNames.Contains(profile.Name, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        foreach (var profile in profiles)
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
        RefreshCoverSources();
    }

    private void RefreshCoverSources()
    {
        CoverSourceSlots.Clear();
        FavoriteSlots.Clear();

        var saved = Slots
            .Where(slot => slot.HasData)
            .ToList();
        foreach (var slot in saved)
        {
            slot.SetMatched(SelectedCharacter is not null && SlotMatchesCharacter(slot, SelectedCharacter));
        }

        foreach (var slot in saved
                     .OrderByDescending(slot => slot.IsMatched)
                     .ThenByDescending(slot => slot.IsFavorite)
                     .ThenBy(slot => slot.Number))
        {
            CoverSourceSlots.Add(slot);
        }

        foreach (var slot in saved.Where(slot => slot.IsFavorite).OrderBy(slot => slot.Number))
        {
            FavoriteSlots.Add(slot);
        }
    }

    private void RefreshRecentBackups()
    {
        RecentBackups.Clear();
        var characterPath = SelectedCharacter?.CharacterPath;
        foreach (var backup in _repository
                     .GetRecentBackups(SelectedProfile?.Id, 200)
                     .Where(backup => string.IsNullOrWhiteSpace(characterPath)
                         || string.Equals(Path.GetFullPath(backup.SourcePath), Path.GetFullPath(characterPath), StringComparison.OrdinalIgnoreCase))
                     .Take(80))
        {
            RecentBackups.Add(backup);
        }

        SelectedBackup = RecentBackups.FirstOrDefault();
        UpdateRiskSummary();
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

    private void ApplyCharacterSect(CharacterConfig? character)
    {
        var option = SectCatalog.Find(character?.Kungfu)
            ?? SectCatalog.Find(character?.Sect)
            ?? SectCatalog.Default;
        SelectedSectOption = option;
        SectTagDraft = option == SectCatalog.Default && string.IsNullOrWhiteSpace(character?.SectTag)
            ? string.Empty
            : character?.SectTag ?? option.Tag;
    }

    private void ResetModuleChoices()
    {
        ModuleChoices.Clear();
        var includeHighRisk = !IsCrossCharacterRestore();
        foreach (var choice in _classifier.GetDefaultChoices(includeHighRisk))
        {
            ModuleChoices.Add(new ModuleChoiceViewModel(choice));
        }

        UpdateRiskSummary();
    }

    private bool IsCrossCharacterRestore()
    {
        if (SelectedCharacter is null)
        {
            return false;
        }

        if (CrossSourceCharacter is not null && IsDifferentCharacter(CrossSourceCharacter, SelectedCharacter))
        {
            return true;
        }

        if (SelectedBackup is null)
        {
            return false;
        }

        try
        {
            var manifest = _backupService.ReadManifest(SelectedBackup.PackagePath);
            return !string.IsNullOrWhiteSpace(manifest.SourceCharacterKey)
                && !string.Equals(manifest.SourceCharacterKey, SelectedCharacter.Key, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsDifferentCharacter(CharacterConfig source, CharacterConfig target)
    {
        return !string.Equals(source.Key, target.Key, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(source.CharacterPath, target.CharacterPath, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SlotMatchesCharacter(SlotViewModel slot, CharacterConfig target)
    {
        if (string.IsNullOrWhiteSpace(slot.SectTag))
        {
            return false;
        }

        var option = SectCatalog.Find(slot.SectTag);
        if (option is null)
        {
            return false;
        }

        return string.Equals(option.Kungfu, target.Kungfu, StringComparison.OrdinalIgnoreCase)
            || string.Equals(option.Sect, target.Sect, StringComparison.OrdinalIgnoreCase);
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
