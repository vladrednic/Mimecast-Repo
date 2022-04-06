namespace A2A.MC.Data.Migrations {
    using System;
    using System.Data.Entity.Migrations;

    public partial class JournalExport : DbMigration {
        public override void Up() {
            AddColumn("dbo.Search", "JournalExtraction", c => c.Boolean(nullable: false));
        }

        public override void Down() {
            DropColumn("dbo.Search", "JournalExtraction");
        }
    }
}
