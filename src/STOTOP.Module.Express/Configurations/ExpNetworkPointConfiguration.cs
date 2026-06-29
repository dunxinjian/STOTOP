using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpNetworkPointConfiguration : IEntityTypeConfiguration<ExpNetworkPoint>
{
    public void Configure(EntityTypeBuilder<ExpNetworkPoint> builder)
    {
        builder.ToTable("EXP快递网点");

        builder.HasKey(e => e.FCode);
        builder.Property(e => e.FCode).HasColumnName("F编号").HasMaxLength(50);
        builder.Property(e => e.FShortName).HasColumnName("F网点简称").HasMaxLength(50);
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FOwnerOrgId).HasColumnName("F所属组织ID");
        builder.Property(e => e.FPointLevel).HasColumnName("F网点级别").HasDefaultValue(1);
        builder.Property(e => e.FIsPrimaryPoint).HasColumnName("F是否一级网点").HasDefaultValue(1);
        builder.Property(e => e.FCoverageArea).HasColumnName("F覆盖区域").HasMaxLength(500);
        builder.Property(e => e.FDailyCapacity).HasColumnName("F日处理能力");
        builder.Property(e => e.FStorageArea).HasColumnName("F仓储面积").HasPrecision(10, 2);
        builder.Property(e => e.FBusinessHours).HasColumnName("F营业时间").HasMaxLength(100);
        builder.Property(e => e.FAddress).HasColumnName("F地址").HasMaxLength(200);
        builder.Property(e => e.FManager).HasColumnName("F负责人").HasMaxLength(50);
        builder.Property(e => e.FContactPhone).HasColumnName("F联系电话").HasMaxLength(20);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        // ===== BU网点扩展属性 =====
        builder.Property(e => e.FFullName).HasColumnName("F网点全称").HasMaxLength(200);
        builder.Property(e => e.FEntityCompany).HasColumnName("F实体公司").HasMaxLength(200);
        builder.Property(e => e.FExpressBrand).HasColumnName("F快递品牌").HasMaxLength(50);
        builder.Property(e => e.FPickupEmployeeCode).HasColumnName("F揽收员编码").HasMaxLength(50);
        builder.Property(e => e.FParentPointCode).HasColumnName("F上级网点编号").HasMaxLength(50);
        builder.Property(e => e.FSortOrder).HasColumnName("F排序");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FOrgId).HasDatabaseName("IX_EXP快递网点_F组织ID");

        builder.HasOne(e => e.Organization)
            .WithMany()
            .HasForeignKey(e => e.FOrgId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
