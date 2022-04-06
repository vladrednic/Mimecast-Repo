using A2A.MC.Data.Migrations;
using A2A.MC.Kernel.Entities;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.MC.Data {
    public class McDbContext : DbContext {
        public McDbContext() : base(DbFactory.SqlDatabaseName) {
            Database.SetInitializer(new McDbContextInitializer<McDbContext, McDbMigrationConfiguration>());
        }
        public McDbContext(string connectionString) : base(connectionString) {
            Database.SetInitializer(new McDbContextInitializer<McDbContext, McDbMigrationConfiguration>());
        }

        public DbSet<SearchStatus> SearchStatus { get; set; }
        public DbSet<ExportFormat> ExportFormat { get; set; }
        public DbSet<Search> Search { get; set; }
        public DbSet<SubSearch> SubSearch { get; set; }

        public DbSet<SubSearchFile> SubSearchFile { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder) {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();
        }
    }
}
