using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Dormitory.Entities;

namespace STOTOP.Module.Dormitory.Configurations;

public class DorExpenseConfiguration : IEntityTypeConfiguration<DorExpense>
{
    public void Configure(EntityTypeBuilder<DorExpense> builder)
    {
        builder.ToTable("DOR费用记录");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FRoomId).HasColumnName("F房间ID").IsRequired();
        builder.Property(e => e.FExpenseType).HasColumnName("F费用类型").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FAmount).HasColumnName("F金额").HasPrecision(18, 2).IsRequired();
        builder.Property(e => e.FMonth).HasColumnName("F月份").HasMaxLength(10).IsRequired();
        builder.Property(e => e.FShareMethod).HasColumnName("F分摊方式").HasMaxLength(50);
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasDefaultValue(1);
        builder.Property(e => e.FCreatorId).HasColumnName("F创建人ID");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.HasIndex(e => e.FRoomId).HasDatabaseName("IX_DOR费用记录_房间ID");
        builder.HasIndex(e => e.FMonth).HasDatabaseName("IX_DOR费用记录_月份");

        builder.HasOne(e => e.Room)
            .WithMany()
            .HasForeignKey(e => e.FRoomId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
