using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Configurations;

public class ExpClientRebateTierConfiguration : IEntityTypeConfiguration<ExpClientRebateTier>
{
    public void Configure(EntityTypeBuilder<ExpClientRebateTier> builder)
    {
        builder.ToTable("EXP客户返利阶梯");

        builder.Property(e => e.FID).HasColumnName("FID");
        builder.Property(e => e.FRebateId).HasColumnName("F返利ID");
        builder.Property(e => e.FMinTickets).HasColumnName("F最低票数");
        builder.Property(e => e.FMaxTickets).HasColumnName("F最高票数");
        builder.Property(e => e.FRebatePerTicket).HasColumnName("F每票返利").HasColumnType("decimal(10,4)");
        builder.Property(e => e.FOrgId).HasColumnName("F组织ID");

        builder.HasIndex(e => new { e.FRebateId, e.FMinTickets })
            .HasDatabaseName("IX_EXP客户返利阶梯_返利最低票数");
    }
}
