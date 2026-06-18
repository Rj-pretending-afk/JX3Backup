using JX3ConfigSwitcher.Models;
using JX3ConfigSwitcher.Services;

namespace JX3ConfigSwitcher.Tests;

public sealed class CoreServiceTests
{
    [Fact]
    public void Repository_Enforces_99_Slot_Limit()
    {
        using var temp = new TempWorkspace();
        var repository = CreateRepository(temp.Root);
        var profile = repository.GetProfiles().Single();

        var slot = repository.UpsertSlot(profile.Id, 99, "最后一个保存档", SaveKind.Universal, null, null);

        Assert.Equal(99, slot.SlotNumber);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            repository.UpsertSlot(profile.Id, 100, "超出范围", SaveKind.Universal, null, null));
    }

    [Fact]
    public void Universal_Default_Modules_Exclude_High_Risk_Items()
    {
        var classifier = new ConfigClassifier();

        var choices = classifier.GetDefaultChoices(SaveKind.Universal);

        Assert.True(choices.Single(choice => choice.Module == ConfigModule.UiLayout).IsSelected);
        Assert.True(choices.Single(choice => choice.Module == ConfigModule.KeyBindings).IsSelected);
        Assert.False(choices.Single(choice => choice.Module == ConfigModule.Macros).IsSelected);
        Assert.False(choices.Single(choice => choice.Module == ConfigModule.ActionButtons).IsSelected);
        Assert.False(choices.Single(choice => choice.Module == ConfigModule.FullDump).IsSelected);
    }

    [Fact]
    public void Scanner_Reads_Account_Server_Character_And_Dump_List()
    {
        using var temp = new TempWorkspace();
        var characterPath = Path.Combine(temp.Root, "userdata", "accountA", "serverA", "roleA");
        Directory.CreateDirectory(Path.Combine(characterPath, "userpreferences"));
        File.WriteAllText(Path.Combine(characterPath, "userpreferences", "settings.dump"), "dump");

        var scanner = new GameScanner();
        var characters = scanner.ScanCharacters(temp.Root);

        var character = Assert.Single(characters);
        Assert.Equal("accountA", character.Account);
        Assert.Equal("serverA", character.Server);
        Assert.Equal("roleA", character.CharacterName);
        Assert.Single(character.DumpFiles);
    }

    [Fact]
    public void Scanner_Prioritizes_Jx3_Root_And_Auto_Locates_UserData()
    {
        using var temp = new TempWorkspace();
        var jx3Root = Path.Combine(temp.Root, "JX3");
        var characterPath = Path.Combine(jx3Root, "bin", "zhcn_hd", "userdata", "accountA", "serverA", "roleA");
        Directory.CreateDirectory(characterPath);
        File.WriteAllText(Path.Combine(characterPath, "custom.dat"), "safe ui");

        var scanner = new GameScanner(new[] { jx3Root });

        var roots = scanner.FindGameRoots(null);
        var characters = scanner.ScanCharacters(roots.Single());

        Assert.Equal(jx3Root, roots.Single());
        Assert.Equal("roleA", Assert.Single(characters).CharacterName);
    }

    [Fact]
    public void Scanner_Reads_Real_Jx3_Four_Level_Character_Folders()
    {
        using var temp = new TempWorkspace();
        var characterPath = Path.Combine(temp.Root, "userdata", "accountA", "电信区", "斗转星移", "roleA");
        Directory.CreateDirectory(Path.Combine(characterPath, "userpreferences"));
        File.WriteAllText(Path.Combine(characterPath, "custom.dat"), "ui");
        File.WriteAllText(Path.Combine(characterPath, "hotkey.data"), "keys");
        File.WriteAllText(Path.Combine(characterPath, "userpreferences", "settings.dump"), "dump");

        var scanner = new GameScanner();
        var characters = scanner.ScanCharacters(temp.Root);

        var character = Assert.Single(characters);
        Assert.Equal("accountA", character.Account);
        Assert.Equal("电信区 / 斗转星移", character.Server);
        Assert.Equal("roleA", character.CharacterName);
        Assert.Single(character.DumpFiles);
    }

    [Fact]
    public void Scanner_Allows_Direct_UserData_Path_When_User_Selects_It()
    {
        using var temp = new TempWorkspace();
        var userData = Path.Combine(temp.Root, "userdata");
        var characterPath = Path.Combine(userData, "accountA", "serverA", "roleA");
        Directory.CreateDirectory(characterPath);
        File.WriteAllText(Path.Combine(characterPath, "custom.dat"), "safe ui");

        var scanner = new GameScanner();

        var characters = scanner.ScanCharacters(userData);

        Assert.Equal("roleA", Assert.Single(characters).CharacterName);
    }

    [Fact]
    public void Backup_And_Restore_With_Safe_Modules_Does_Not_Copy_Dump_Or_Macro()
    {
        using var temp = new TempWorkspace();
        var repository = CreateRepository(temp.Root);
        var profile = repository.GetProfiles().Single();
        var classifier = new ConfigClassifier();
        var backupService = new BackupService(
            new PortablePaths(temp.Root),
            repository,
            classifier,
            new GameProcessGuard(() => false));

        var sourcePath = Path.Combine(temp.Root, "source");
        var targetPath = Path.Combine(temp.Root, "target");
        Directory.CreateDirectory(Path.Combine(sourcePath, "userpreferences"));
        Directory.CreateDirectory(targetPath);
        File.WriteAllText(Path.Combine(sourcePath, "ui_layout.ini"), "safe ui");
        File.WriteAllText(Path.Combine(sourcePath, "macro.txt"), "macro");
        File.WriteAllText(Path.Combine(sourcePath, "userpreferences", "role.dump"), "dump");

        var source = new CharacterConfig("acc", "server", "sourceRole", sourcePath, Array.Empty<string>());
        var target = new CharacterConfig("acc", "server", "targetRole", targetPath, Array.Empty<string>());

        var backup = backupService.CreateBackup(
            profile,
            1,
            "通用档",
            SaveKind.Universal,
            source,
            new[] { ConfigModule.UiLayout, ConfigModule.KeyBindings, ConfigModule.DisplayChatAddon },
            null,
            "safe backup");

        var manifest = backupService.ReadManifest(backup.PackagePath);
        Assert.False(manifest.ContainsDump);
        Assert.DoesNotContain(manifest.Files, file => file.Module == ConfigModule.Macros);
        Assert.DoesNotContain(manifest.Files, file => file.Module == ConfigModule.FullDump);

        backupService.RestoreBackup(backup.PackagePath, target, new[] { ConfigModule.UiLayout, ConfigModule.KeyBindings, ConfigModule.DisplayChatAddon });

        Assert.True(File.Exists(Path.Combine(targetPath, "ui_layout.ini")));
        Assert.False(File.Exists(Path.Combine(targetPath, "macro.txt")));
        Assert.False(File.Exists(Path.Combine(targetPath, "userpreferences", "role.dump")));
    }

    private static ProfileRepository CreateRepository(string root)
    {
        var paths = new PortablePaths(root);
        paths.EnsureCreated();
        var database = new DatabaseService(paths);
        database.Initialize();
        return new ProfileRepository(database);
    }

    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), "JX3ConfigSwitcherTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
