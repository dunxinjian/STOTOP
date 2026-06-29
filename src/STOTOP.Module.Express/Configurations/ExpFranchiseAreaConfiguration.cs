using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpFranchiseAreaConfiguration : IEntityTypeConfiguration<ExpFranchiseArea>
{
    public void Configure(EntityTypeBuilder<ExpFranchiseArea> builder)
    {
        builder.ToTable("EXP承包区");

        builder.HasKey(e => e.FCode);
        builder.Property(e => e.FCode).HasColumnName("F编号").HasMaxLength(50);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FOwnerOrgId).HasColumnName("F所属组织ID");
        builder.Property(e => e.FContractor).HasColumnName("F承包人").HasMaxLength(50);
        builder.Property(e => e.FContractStartDate).HasColumnName("F承包开始日期");
        builder.Property(e => e.FContractEndDate).HasColumnName("F承包结束日期");
        builder.Property(e => e.FCoverageDistrict).HasColumnName("F覆盖片区").HasMaxLength(500);
        builder.Property(e => e.FContractFee).HasColumnName("F承包费").HasPrecision(12, 2);
        builder.Property(e => e.FContactPhone).HasColumnName("F联系电话").HasMaxLength(20);
        builder.Property(e => e.FAddress).HasColumnName("F地址").HasMaxLength(200);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        // ===== 源系统迁移字段 =====
        builder.Property(e => e.FSourceUid).HasColumnName("F源UID").HasMaxLength(30);
        builder.Property(e => e.FContractorIdCard).HasColumnName("F负责人身份证").HasMaxLength(30);
        builder.Property(e => e.FEmergencyContact).HasColumnName("F紧急联系方式").HasMaxLength(50);
        builder.Property(e => e.FDeliveryFeeRate).HasColumnName("F派费标准").HasMaxLength(100);
        builder.Property(e => e.FSettlementCycleText).HasColumnName("F结算周期原值").HasMaxLength(20);
        builder.Property(e => e.FBankAccount).HasColumnName("F银行账号").HasMaxLength(50);
        builder.Property(e => e.FAlipayAccount).HasColumnName("F支付宝账号").HasMaxLength(50);
        builder.Property(e => e.FThreeSegmentCode).HasColumnName("F三段码").HasMaxLength(50);
        builder.Property(e => e.FSortOrder).HasColumnName("F排序");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FOrgId).HasDatabaseName("IX_EXP承包区_F组织ID");

        builder.HasOne(e => e.Organization)
            .WithMany()
            .HasForeignKey(e => e.FOrgId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
