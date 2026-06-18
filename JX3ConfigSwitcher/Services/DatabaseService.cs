using System;
using Microsoft.Data.Sqlite;

namespace JX3ConfigSwitcher.Services;

public sealed class DatabaseService
{
    private readonly PortablePaths _paths;

    public DatabaseService(PortablePaths paths)
    {
        _paths = paths;
    }

    public SqliteConnection OpenConnection()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _paths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    public void Initialize()
    {
        using var connection = OpenConnection();
        Execute(connection, @"
            CREATE TABLE IF NOT EXISTS profiles (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS save_slots (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                profile_id INTEGER NOT NULL,
                slot_number INTEGER NOT NULL CHECK(slot_number BETWEEN 1 AND 99),
                name TEXT NOT NULL,
                kind TEXT NOT NULL,
                character_key TEXT NULL,
                sect_tag TEXT NULL,
                updated_at TEXT NOT NULL,
                UNIQUE(profile_id, slot_number),
                FOREIGN KEY(profile_id) REFERENCES profiles(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS backup_versions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                profile_id INTEGER NOT NULL,
                slot_number INTEGER NOT NULL,
                kind TEXT NOT NULL,
                package_path TEXT NOT NULL,
                source_path TEXT NOT NULL,
                note TEXT NOT NULL,
                module_summary TEXT NOT NULL,
                contains_dump INTEGER NOT NULL,
                created_at TEXT NOT NULL,
                FOREIGN KEY(profile_id) REFERENCES profiles(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS operation_logs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                level TEXT NOT NULL,
                message TEXT NOT NULL,
                created_at TEXT NOT NULL
            );
            ");

        Execute(connection, @"
            INSERT INTO profiles(name, created_at)
            SELECT '默认用户', $now
            WHERE NOT EXISTS (SELECT 1 FROM profiles);
            ", ("$now", DateTime.UtcNow.ToString("O")));
    }

    private static void Execute(SqliteConnection connection, string sql, params (string Name, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }

        command.ExecuteNonQuery();
    }
}
