using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.System.Configurations;

public class SysUserPositionConfiguration : IEntityTypeConfiguration<SysUserPosition>
{
    public void Configure(EntityTypeBuilder<SysUserPosition> builder)
    {
        builder.ToTable("SYS用户岗位");
        
        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FUserId).HasColumnName("F用户ID");
        builder.Property(e => e.FPositionId).HasColumnName("F岗位ID");
        builder.Property(e => e.FIsPrimary).HasColumnName("F是否主岗").HasDefaultValue(0);
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");
        builder.Property(e => e.SysUserFID).HasColumnName("SysUserFID");

        builder.HasIndex(e => new { e.FUserId, e.FPositionId }).IsUnique().HasDatabaseName("UQ_SYS用户岗位");

        builder.HasOne(e => e.User)
            .WithMany(u => u.UserPositions)
            .HasForeignKey(e => e.FUserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_用户岗位_用户");

        builder.HasOne(e => e.Position)
            .WithMany(p => p.UserPositions)
            .HasForeignKey(e => e.FPositionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_用户岗位_岗位");
    }
}
