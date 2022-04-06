using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Migrations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.MC.Data {
    public class McDbContextInitializer<TContext, TMigration> : MigrateDatabaseToLatestVersion<TContext, TMigration>
        where TContext : McDbContext
        where TMigration : DbMigrationsConfiguration<TContext>, new() {

        public override void InitializeDatabase(TContext context) {
            try {
                if (context.Database.Exists()) {
                    var migrator = new DbMigrator(new TMigration(), context);
                    migrator.Update();
                }
                else {
                    var maker = new CreateDatabaseIfNotExists<TContext>();
                    try {
                        maker.InitializeDatabase(context);
                    }
                    catch (Exception ex) {
                        DbFactory.Info(ex.Message);
                        if (ex.InnerException != null) {
                            DbFactory.Info(ex.InnerException.Message);
                        }
                        throw;
                    }

                    DbMigrator migratorInitial = new DbMigrator(new TMigration(), context);
                    migratorInitial.Update();

                }
            }
            catch { }
        }
    }
}
