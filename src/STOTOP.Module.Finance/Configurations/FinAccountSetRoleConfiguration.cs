using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Finance.Entities;

namespace STOTOP.Module.Finance.Configurations;

public class FinAccountSetRoleConfiguration : IEntityTypeConfiguration<FinAccountSetRole>
{
    public void Configure(EntityTypeBuilder<FinAccountSetRole> builder)
    {
        builder.ToTable("FIN账套角色");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(100);
        builder.Property(e => e.FCode).HasColumnName("F编码").HasMaxLength(50);
        builder.Property(e => e.FDescription).HasColumnName("F说明").HasMaxLength(500);
        builder.Property(e => e.FIsSystem).HasColumnName("F系统预置");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FCode).IsUnique().HasDatabaseName("UK_FIN账套角色_编码");
    }
}
