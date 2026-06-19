using System;
using System.Collections.Generic;
using JX3ConfigSwitcher.Models;
using JX3ConfigSwitcher.Views;

namespace JX3ConfigSwitcher.Services;

public interface IBackupProfileHostApi
{
    string? RequestCreateProfileName(IReadOnlyList<ProfileRecord> existingProfiles);

    void OnProfileCreated(ProfileRecord profile);

    IReadOnlySet<string> GetOwnedCharacterKeys(string profileName);

    IReadOnlyList<string> GetHostProfileNames();
}

public sealed class DialogBackupProfileHostApi : IBackupProfileHostApi
{
    public string? RequestCreateProfileName(IReadOnlyList<ProfileRecord> existingProfiles)
    {
        return InputDialog.Ask("新建 Profile", "输入用户名称，例如：我、她、朋友A", "");
    }

    public void OnProfileCreated(ProfileRecord profile)
    {
    }

    public IReadOnlySet<string> GetOwnedCharacterKeys(string profileName)
    {
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> GetHostProfileNames()
    {
        return Array.Empty<string>();
    }
}
