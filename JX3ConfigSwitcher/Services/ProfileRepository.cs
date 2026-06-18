using System;
using System.Collections.Generic;
using JX3ConfigSwitcher.Models;
using Microsoft.Data.Sqlite;

namespace JX3ConfigSwitcher.Services;

public sealed class ProfileRepository
{
    private readonly DatabaseService _database;

    public ProfileRepository(DatabaseService database)
    {
        _database = database;
    }

    public IReadOnlyList<ProfileRecord> GetProfiles()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, created_at FROM profiles ORDER BY created_at, id;";
        using var reader = command.ExecuteReader();
        var result = new List<ProfileRecord>();
        while (reader.Read())
        {
            result.Add(new ProfileRecord(
                reader.GetInt64(0),
                reader.GetString(1),
                DateTime.Parse(reader.GetString(2)).ToLocalTime()));
        }

        return result;
    }

    public ProfileRecord CreateProfile(string name)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO profiles(name, created_at) VALUES($name, $created); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$name", name.Trim());
        command.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O"));
        var id = (long)(command.ExecuteScalar() ?? 0L);
        return new ProfileRecord(id, name.Trim(), DateTime.Now);
    }

    public IReadOnlyList<SaveSlotRecord> GetSlots(long profileId)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, profile_id, slot_number, name, kind, character_key, sect_tag, updated_at
            FROM save_slots
            WHERE profile_id = $profile
            ORDER BY slot_number;
            ";
        command.Parameters.AddWithValue("$profile", profileId);
        using var reader = command.ExecuteReader();
        var result = new List<SaveSlotRecord>();
        while (reader.Read())
        {
            result.Add(ReadSlot(reader));
        }

        return result;
    }

    public SaveSlotRecord UpsertSlot(
        long profileId,
        int slotNumber,
        string name,
        SaveKind kind,
        string? characterKey,
        string? sectTag)
    {
        if (slotNumber is < 1 or > 99)
        {
            throw new ArgumentOutOfRangeException(nameof(slotNumber), "保存档编号必须在 01-99。");
        }

        var now = DateTime.UtcNow;
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO save_slots(profile_id, slot_number, name, kind, character_key, sect_tag, updated_at)
            VALUES($profile, $slot, $name, $kind, $character, $sect, $updated)
            ON CONFLICT(profile_id, slot_number) DO UPDATE SET
                name = excluded.name,
                kind = excluded.kind,
                character_key = excluded.character_key,
                sect_tag = excluded.sect_tag,
                updated_at = excluded.updated_at
            RETURNING id, profile_id, slot_number, name, kind, character_key, sect_tag, updated_at;
            ";
        command.Parameters.AddWithValue("$profile", profileId);
        command.Parameters.AddWithValue("$slot", slotNumber);
        command.Parameters.AddWithValue("$name", name.Trim());
        command.Parameters.AddWithValue("$kind", kind.ToString());
        command.Parameters.AddWithValue("$character", (object?)characterKey ?? DBNull.Value);
        command.Parameters.AddWithValue("$sect", (object?)sectTag ?? DBNull.Value);
        command.Parameters.AddWithValue("$updated", now.ToString("O"));
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException("保存档写入失败。");
        }

        return ReadSlot(reader);
    }

    public void AddBackupVersion(BackupVersionRecord record)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO backup_versions(profile_id, slot_number, kind, package_path, source_path, note, module_summary, contains_dump, created_at)
            VALUES($profile, $slot, $kind, $package, $source, $note, $modules, $containsDump, $created);
            ";
        command.Parameters.AddWithValue("$profile", record.ProfileId);
        command.Parameters.AddWithValue("$slot", record.SlotNumber);
        command.Parameters.AddWithValue("$kind", record.Kind.ToString());
        command.Parameters.AddWithValue("$package", record.PackagePath);
        command.Parameters.AddWithValue("$source", record.SourcePath);
        command.Parameters.AddWithValue("$note", record.Note);
        command.Parameters.AddWithValue("$modules", record.ModuleSummary);
        command.Parameters.AddWithValue("$containsDump", record.ContainsDump ? 1 : 0);
        command.Parameters.AddWithValue("$created", record.CreatedAt.ToUniversalTime().ToString("O"));
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<BackupVersionRecord> GetRecentBackups(long? profileId = null, int limit = 50)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = profileId is null
            ? "SELECT id, profile_id, slot_number, kind, package_path, source_path, note, module_summary, contains_dump, created_at FROM backup_versions ORDER BY datetime(created_at) DESC LIMIT $limit;"
            : "SELECT id, profile_id, slot_number, kind, package_path, source_path, note, module_summary, contains_dump, created_at FROM backup_versions WHERE profile_id = $profile ORDER BY datetime(created_at) DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", limit);
        if (profileId is not null)
        {
            command.Parameters.AddWithValue("$profile", profileId.Value);
        }

        using var reader = command.ExecuteReader();
        var result = new List<BackupVersionRecord>();
        while (reader.Read())
        {
            result.Add(new BackupVersionRecord(
                reader.GetInt64(0),
                reader.GetInt64(1),
                reader.GetInt32(2),
                Enum.Parse<SaveKind>(reader.GetString(3)),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetInt32(8) == 1,
                DateTime.Parse(reader.GetString(9)).ToLocalTime()));
        }

        return result;
    }

    public void AddLog(string level, string message)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO operation_logs(level, message, created_at) VALUES($level, $message, $created);";
        command.Parameters.AddWithValue("$level", level);
        command.Parameters.AddWithValue("$message", message);
        command.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<OperationLogRecord> GetLogs(int limit = 100)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, level, message, created_at FROM operation_logs ORDER BY datetime(created_at) DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", limit);
        using var reader = command.ExecuteReader();
        var result = new List<OperationLogRecord>();
        while (reader.Read())
        {
            result.Add(new OperationLogRecord(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                DateTime.Parse(reader.GetString(3)).ToLocalTime()));
        }

        return result;
    }

    private static SaveSlotRecord ReadSlot(SqliteDataReader reader)
    {
        return new SaveSlotRecord(
            reader.GetInt64(0),
            reader.GetInt64(1),
            reader.GetInt32(2),
            reader.GetString(3),
            Enum.Parse<SaveKind>(reader.GetString(4)),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            DateTime.Parse(reader.GetString(7)).ToLocalTime());
    }
}
