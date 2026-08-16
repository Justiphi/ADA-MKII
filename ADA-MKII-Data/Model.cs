using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Sqlite;
using System.Collections.Generic;
using System;
using System.IO;

namespace ADA_MKII_Data
{
    public class Model
    {
        public class DataContext : DbContext
        {
            public DbSet<APIKey> APIKeys { get; set; }
            public DbSet<Setting> Settings { get; set; }

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseSqlite($"Data Source={Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\\ADA-MKII\\ADA.db;Version=3;");
            }
        }

        public class APIKey {
            public int APIKeyId { get; set; }
            public string? API { get; set; }
            public string? Key { get; set; }
        }

        public class Setting {
            public int SettingId { get; set; }
            public string? SettingName { get; set; }
            public string? SettingValue { get; set; }
        }

    }
}