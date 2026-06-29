using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinVoucherTemplateEntryConfiguration : IEntityTypeConfiguration<FinVoucherTemplateEntry>
{
    public void Configure(EntityTypeBuilder<FinVoucherTemplateEntry> builder)
    {
        builder.ToTable("FIN凭证模板分录");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FTemplateId).HasColumnName("F模板ID");
        builder.Property(e => e.FSummary).HasColumnName("F摘要").HasMaxLength(500);
        builder.Property(e => e.FAccountId).HasColumnName("F科目ID");
        builder.Property(e => e.FAccountCode).HasColumnName("F科目编码").HasMaxLength(50);
        builder.Property(e => e.FAccountName).HasColumnName("F科目名称").HasMaxLength(200);
        builder.Property(e => e.FDebitAmount).HasColumnName("F借方金额").HasColumnType("DECIMAL(18,2)");
        builder.Property(e => e.FCreditAmount).HasColumnName("F贷方金额").HasColumnType("DECIMAL(18,2)");
        builder.Property(e => e.FSeq).HasColumnName("F序号");
        builder.Property(e => e.FAuxiliaryJson).HasColumnName("F辅助核算JSON");

        builder.HasIndex(e => e.FTemplateId).HasDatabaseName("IX_FIN凭证模板分录_模板ID");

        builder.HasOne(e => e.Template)
            .WithMany(e => e.Entries)
            .HasForeignKey(e => e.FTemplateId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_FIN凭证模板分录_模板ID");

        builder.HasOne<FinAccount>()
            .WithMany()
            .HasForeignKey(e => e.FAccountId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_FIN凭证模板分录_科目ID");
    }
}
