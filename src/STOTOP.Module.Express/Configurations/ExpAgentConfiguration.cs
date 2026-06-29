using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpAgentConfiguration : IEntityTypeConfiguration<ExpAgent>
{
    public void Configure(EntityTypeBuilder<ExpAgent> builder)
    {
        builder.ToTable("EXP业务代理");

        builder.HasKey(e => e.FCode);
        builder.Property(e => e.FCode).HasColumnName("F编号").HasMaxLength(50);
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(100).IsRequired();
        builder.Property(e => e.FAgentLevel).HasColumnName("F代理级别").HasDefaultValue(1);
        builder.Property(e => e.FAgentRegion).HasColumnName("F代理区域").HasMaxLength(200);
        builder.Property(e => e.FContactName).HasColumnName("F联系人").HasMaxLength(50);
        builder.Property(e => e.FContactPhone).HasColumnName("F联系电话").HasMaxLength(20);
        builder.Property(e => e.FAddress).HasColumnName("F地址").HasMaxLength(200);
        builder.Property(e => e.FCooperationStartDate).HasColumnName("F合作开始日期");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");
    }
}
