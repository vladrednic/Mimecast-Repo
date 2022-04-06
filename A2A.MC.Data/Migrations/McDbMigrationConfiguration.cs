namespace A2A.MC.Data.Migrations {
    using A2A.MC.Kernel.Entities;
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Migrations;
    using System.Linq;

    internal sealed class McDbMigrationConfiguration : DbMigrationsConfiguration<A2A.MC.Data.McDbContext> {
        public McDbMigrationConfiguration() {
            AutomaticMigrationsEnabled = false;
        }

        protected override void Seed(A2A.MC.Data.McDbContext context) {
            //populate ExportFormat
            var t = typeof(ExportFormatCode);
            var values = Enum.GetValues(t);

            foreach (var value in values) {
                var name = Enum.GetName(t, value);
                context.ExportFormat.AddOrUpdate(
                    sourceType => sourceType.Code,
                    new ExportFormat() {
                        Code = (ExportFormatCode)value,
                        Description = name
                    });
            }

            //populate SearchStatus
            t = typeof(SearchStatusEnum);
            values = Enum.GetValues(t);

            foreach (var value in values) {
                var name = Enum.GetName(t, value);
                context.SearchStatus.AddOrUpdate(
                    sourceType => sourceType.Id,
                    new SearchStatus() {
                        Id = (SearchStatusEnum)value,
                        StatusCode = name
                    });
            }
        }
    }
}
