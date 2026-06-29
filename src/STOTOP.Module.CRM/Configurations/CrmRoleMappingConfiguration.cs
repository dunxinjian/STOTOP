using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.CRM.Entities;

namespace STOTOP.Module.CRM.Configurations;

public class CrmRoleMappingConfiguration : IEntityTypeConfiguration<CrmRoleMapping>
{
    public void Configure(EntityTypeBuilder<CrmRoleMapping> builder)
    {
        builder.ToTable("CRM角色映射");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FEmployeeId).HasColumnName("F员工ID");
        builder.Property(e => e.FRole).HasColumnName("F角色");
        builder.Property(e => e.FCreatorName).HasColumnName("F创建人").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdaterName).HasColumnName("F更新人").HasMaxLength(50);
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FOrgId).HasDatabaseName("IX_CRM角色映射_F组织ID");
        builder.HasIndex(e => e.FEmployeeId).HasDatabaseName("IX_CRM角色映射_F员工ID");
        builder.HasIndex(e => new { e.FOrgId, e.FEmployeeId }).IsUnique().HasDatabaseName("UX_CRM角色映射_组织员工");
    }
}
