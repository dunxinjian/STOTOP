using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Quality.Entities;

namespace STOTOP.Module.Quality.Configurations;

public class QlExceptionLogConfiguration : IEntityTypeConfiguration<QlExceptionLog>
{
    public void Configure(EntityTypeBuilder<QlExceptionLog> builder)
    {
        builder.ToTable("QL异常日志");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FExceptionId).HasColumnName("F异常单ID");
        builder.Property(e => e.FOperatorId).HasColumnName("F操作人ID");
        builder.Property(e => e.FAction).HasColumnName("F操作").HasMaxLength(50).IsRequired();
        builder.Property(e => e.FRemark).HasColumnName("F备注").HasMaxLength(500);
        builder.Property(e => e.FFromStatus).HasColumnName("F原状态");
        builder.Property(e => e.FToStatus).HasColumnName("F新状态");
        builder.Property(e => e.FCreateTime).HasColumnName("F创建时间").HasDefaultValueSql("GETDATE()");

        builder.HasIndex(e => e.FExceptionId).HasDatabaseName("IX_QL异常日志_异常单ID");
    }
}
