using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Conference.Entities;

namespace STOTOP.Module.Conference.Configurations;

public class ConfEventConfiguration : IEntityTypeConfiguration<ConfEvent>
{
    public void Configure(EntityTypeBuilder<ConfEvent> builder)
    {
        builder.ToTable("CONF活动");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FName).HasColumnName("F名称").HasMaxLength(200).IsRequired();
        builder.Property(e => e.FDescription).HasColumnName("F描述");
        builder.Property(e => e.FStartDate).HasColumnName("F开始日期").HasColumnType("date");
        builder.Property(e => e.FEndDate).HasColumnName("F结束日期").HasColumnType("date");
        builder.Property(e => e.FLocation).HasColumnName("F地点").HasMaxLength(200);
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasMaxLength(20).HasDefaultValue("筹备中");
        builder.Property(e => e.FManager).HasColumnName("F负责人").HasMaxLength(50);
        builder.Property(e => e.FManagerPhone).HasColumnName("F负责人电话").HasMaxLength(20);
        builder.Property(e => e.FBudget).HasColumnName("F预算").HasColumnType("decimal(18,2)");
        builder.Property(e => e.FRemark).HasColumnName("F备注");
        builder.Property(e => e.FCreator).HasColumnName("F创建人").HasMaxLength(50);
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.Property(e => e.FType).HasColumnName("F活动类型").HasMaxLength(20).HasDefaultValue("conference");
        builder.Property(e => e.FGroomName).HasColumnName("F新郎姓名").HasMaxLength(50);
        builder.Property(e => e.FBrideName).HasColumnName("F新娘姓名").HasMaxLength(50);

        builder.HasIndex(e => e.FName).HasDatabaseName("IX_CONF活动_名称");
        builder.HasIndex(e => e.FStatus).HasDatabaseName("IX_CONF活动_状态");

        // 一对多关系
        builder.HasMany(e => e.Attendees)
            .WithOne(e => e.Event)
            .HasForeignKey(e => e.FEventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Schedules)
            .WithOne(e => e.Event)
            .HasForeignKey(e => e.FEventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Vehicles)
            .WithOne(e => e.Event)
            .HasForeignKey(e => e.FEventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.PickupTasks)
            .WithOne(e => e.Event)
            .HasForeignKey(e => e.FEventId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Hotels)
            .WithOne(e => e.Event)
            .HasForeignKey(e => e.FEventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.MealPlans)
            .WithOne(e => e.Event)
            .HasForeignKey(e => e.FEventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Incomes)
            .WithOne(e => e.Event)
            .HasForeignKey(e => e.FEventId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Materials)
            .WithOne(e => e.Event)
            .HasForeignKey(e => e.FEventId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.VehicleSchedules)
            .WithOne(e => e.Event)
            .HasForeignKey(e => e.FEventId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Gifts)
            .WithOne(e => e.Event)
            .HasForeignKey(e => e.FEventId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.CeremonyItems)
            .WithOne(e => e.Event)
            .HasForeignKey(e => e.FEventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
