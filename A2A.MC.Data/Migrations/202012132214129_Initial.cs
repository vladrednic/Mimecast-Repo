namespace A2A.MC.Data.Migrations {
    using System;
    using System.Data.Entity.Migrations;

    public partial class Initial : DbMigration {
        public override void Up() {
            CreateTable(
                "dbo.ExportFormat",
                c => new {
                    Code = c.Byte(nullable: false),
                    Description = c.String(maxLength: 50),
                })
                .PrimaryKey(t => t.Code);

            CreateTable(
                "dbo.Search",
                c => new {
                    Id = c.Int(nullable: false, identity: true),
                    Email = c.String(maxLength: 512),
                    Name = c.String(maxLength: 512),
                    CreatedDate = c.DateTime(),
                    BeginDate = c.DateTime(),
                    EndDate = c.DateTime(),
                    ExportFormatId = c.Byte(nullable: false),
                    SearchStatusId = c.Byte(nullable: false),
                    ItemsCount = c.Long(),
                    Priority = c.Int(),
                    Tag = c.String(),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.ExportFormat", t => t.ExportFormatId, cascadeDelete: true)
                .Index(t => t.Email, unique: true)
                .Index(t => new { t.Email, t.BeginDate, t.EndDate }, unique: true, name: "unique")
                .Index(t => t.Name)
                .Index(t => t.ExportFormatId);

            CreateTable(
                "dbo.SubSearch",
                c => new {
                    Id = c.Int(nullable: false, identity: true),
                    SearchId = c.Int(nullable: false),
                    StatusId = c.Byte(nullable: false),
                    Name = c.String(maxLength: 512),
                    Email = c.String(maxLength: 512),
                    CreatedDate = c.DateTime(),
                    BeginDate = c.DateTime(),
                    EndDate = c.DateTime(),
                    ItemsCount = c.Long(),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Search", t => t.SearchId, cascadeDelete: true)
                .Index(t => t.SearchId)
                .Index(t => t.Name);

            CreateTable(
                "dbo.SubSearchFile",
                c => new {
                    Id = c.Int(nullable: false, identity: true),
                    SubSearchId = c.Int(),
                    DiscoveredDate = c.DateTime(nullable: false),
                    DownloadPath = c.String(),
                    DownloadDate = c.DateTime(),
                    McOriginalFileName = c.String(),
                    McBatchNumber = c.Int(),
                    McCreateTime = c.DateTime(),
                    McExpiryDate = c.DateTime(),
                    McNumberOfMessages = c.Int(),
                    McFailedMessages = c.Int(),
                    McFileSizeBytesApprox = c.Long(),
                    DownloadError = c.Boolean(nullable: false),
                })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.SubSearch", t => t.SubSearchId)
                .Index(t => t.SubSearchId);

            CreateTable(
                "dbo.SearchStatus",
                c => new {
                    Id = c.Byte(nullable: false),
                    StatusCode = c.String(maxLength: 20),
                })
                .PrimaryKey(t => t.Id);

        }

        public override void Down() {
            DropForeignKey("dbo.SubSearchFile", "SubSearchId", "dbo.SubSearch");
            DropForeignKey("dbo.SubSearch", "SearchId", "dbo.Search");
            DropForeignKey("dbo.Search", "ExportFormatId", "dbo.ExportFormat");
            DropIndex("dbo.SubSearchFile", new[] { "SubSearchId" });
            DropIndex("dbo.SubSearch", new[] { "Name" });
            DropIndex("dbo.SubSearch", new[] { "SearchId" });
            DropIndex("dbo.Search", new[] { "ExportFormatId" });
            DropIndex("dbo.Search", new[] { "Name" });
            DropIndex("dbo.Search", "unique");
            DropIndex("dbo.Search", new[] { "Email" });
            DropTable("dbo.SearchStatus");
            DropTable("dbo.SubSearchFile");
            DropTable("dbo.SubSearch");
            DropTable("dbo.Search");
            DropTable("dbo.ExportFormat");
        }
    }
}
