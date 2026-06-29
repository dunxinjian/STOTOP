using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Points.Entities;

namespace STOTOP.Module.Points.Configurations;

public class PmRedeemRecordConfiguration : IEntityTypeConfiguration<PmRedeemRecord>
{
    public void Configure(EntityTypeBuilder<PmRedeemRecord> builder)
    {
        builder.ToTable("PM兑换记录");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FUserId).HasColumnName("F用户ID");
        builder.Property(e => e.FItemId).HasColumnName("F商品ID");
        builder.Property(e => e.FDeductedPoints).HasColumnName("F扣除积分");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(0);
        builder.Property(e => e.FIssuerId).HasColumnName("F发放人ID");
        builder.Property(e => e.FIssueTime).HasColumnName("F发放时间");
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(200);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FUserId).HasDatabaseName("IX_PM兑换记录_用户ID");
        builder.HasIndex(e => e.FStatus).HasDatabaseName("IX_PM兑换记录_状态");
    }
}
