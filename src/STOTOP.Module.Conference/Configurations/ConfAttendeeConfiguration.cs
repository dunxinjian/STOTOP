using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Conference.Entities;

namespace STOTOP.Module.Conference.Configurations;

public class ConfAttendeeConfiguration : IEntityTypeConfiguration<ConfAttendee>
{
    public void Configure(EntityTypeBuilder<ConfAttendee> builder)
    {
        builder.ToTable("CONF参会人员");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");
        builder.Property(e => e.FEventId).HasColumnName("F活动ID");
        builder.Property(e => e.FName).HasColumnName("F姓名").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FGender).HasColumnName("F性别").HasMaxLength(4);
        builder.Property(e => e.FPhone).HasColumnName("F手机号").HasMaxLength(20);
        builder.Property(e => e.FOrganization).HasColumnName("F单位").HasMaxLength(200);
        builder.Property(e => e.FTitle).HasColumnName("F职务").HasMaxLength(100);
        builder.Property(e => e.FRole).HasColumnName("F角色").HasMaxLength(50);
        builder.Property(e => e.FDietPreference).HasColumnName("F饮食偏好").HasMaxLength(50);
        builder.Property(e => e.FArrivalMode).HasColumnName("F来程方式").HasMaxLength(20);
        builder.Property(e => e.FArrivalFlightTrain).HasColumnName("F来程航班车次").HasMaxLength(50);
        builder.Property(e => e.FArrivalTime).HasColumnName("F来程到达时间");
        builder.Property(e => e.FArrivalStation).HasColumnName("F来程到达站点").HasMaxLength(100);
        builder.Property(e => e.FDepartureMode).HasColumnName("F回程方式").HasMaxLength(20);
        builder.Property(e => e.FDepartureFlightTrain).HasColumnName("F回程航班车次").HasMaxLength(50);
        builder.Property(e => e.FDepartureTime).HasColumnName("F回程出发时间");
        builder.Property(e => e.FDepartureStation).HasColumnName("F回程出发站点").HasMaxLength(100);
        builder.Property(e => e.FNeedPickup).HasColumnName("F是否需要接送").HasDefaultValue(true);
        builder.Property(e => e.FNeedAccommodation).HasColumnName("F是否需要住宿").HasDefaultValue(true);
        builder.Property(e => e.FPreferredRoomType).HasColumnName("F房型偏好").HasMaxLength(50);
        builder.Property(e => e.FRemark).HasColumnName("F备注");
        builder.Property(e => e.FStatus).HasColumnName("F状态").HasMaxLength(20).HasDefaultValue("待确认");
        builder.Property(e => e.FCheckInStatus).HasColumnName("F签到状态").HasMaxLength(20).HasDefaultValue("未签到");
        builder.Property(e => e.FCreatedTime).HasColumnName("F创建时间");
        builder.Property(e => e.FUpdatedTime).HasColumnName("F更新时间");

        builder.Property(e => e.FPrimaryGuestId).HasColumnName("F主宾客ID");
        builder.Property(e => e.FRelation).HasColumnName("F关系").HasMaxLength(20);
        builder.Property(e => e.FIsChild).HasColumnName("F是否儿童");
        builder.Property(e => e.FAge).HasColumnName("F年龄");
        builder.Property(e => e.FCamp).HasColumnName("F阵营").HasMaxLength(10);
        builder.Property(e => e.FGuestType).HasColumnName("F宾客类型").HasMaxLength(20);
        builder.Property(e => e.FCompanionCount).HasColumnName("F随行人数");
        builder.Property(e => e.FHasSeat).HasColumnName("F是否占座").HasDefaultValue(true);
        builder.Property(e => e.FMealCategory).HasColumnName("F餐标类别").HasMaxLength(20).HasDefaultValue("全餐");
        builder.Property(e => e.F确认状态).HasColumnName("F确认状态").HasMaxLength(20).HasDefaultValue("待联系");

        builder.HasOne(e => e.PrimaryGuest)
            .WithMany(e => e.Companions)
            .HasForeignKey(e => e.FPrimaryGuestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.FEventId).HasDatabaseName("IX_CONF参会人员_活动ID");
        builder.HasIndex(e => e.FName).HasDatabaseName("IX_CONF参会人员_姓名");
    }
}
