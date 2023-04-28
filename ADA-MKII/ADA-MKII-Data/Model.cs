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

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseSqlite($"Data Source={Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\\ADA-MKII\\ADA.db;Version=3;");
            }
        }
    }
}