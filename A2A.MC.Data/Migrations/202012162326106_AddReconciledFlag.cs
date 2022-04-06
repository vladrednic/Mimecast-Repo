namespace A2A.MC.Data.Migrations {
    using System;
    using System.Data.Entity.Migrations;

    public partial class AddReconciledFlag : DbMigration {
        public override void Up() {
            AddColumn("dbo.SubSearch", "Reconciled", c => c.DateTime());
        }

        public override void Down() {
            DropColumn("dbo.SubSearch", "Reconciled");
        }
    }
}
