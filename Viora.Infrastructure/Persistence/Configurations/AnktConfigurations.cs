using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viora.Domain.Entities;

namespace Viora.Infrastructure.Persistence.Configurations;

internal sealed class AnktIdentityConfiguration : IEntityTypeConfiguration<AnktIdentity>
{
    public void Configure(EntityTypeBuilder<AnktIdentity> builder)
    {
        builder.ToTable("AnktIdentities");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MiniAppId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Environment).HasMaxLength(16).IsRequired();
        builder.Property(x => x.AnktUserId).HasMaxLength(255).IsRequired();
        builder.Property(x => x.ProfileJson).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(x => new { x.MiniAppId, x.Environment, x.AnktUserId }).IsUnique();
        builder.HasIndex(x => x.AccountId).IsUnique();
        builder.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AnktGrantConfiguration : IEntityTypeConfiguration<AnktGrant>
{
    public void Configure(EntityTypeBuilder<AnktGrant> builder)
    {
        builder.ToTable("AnktGrants");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.Identity).WithMany().HasForeignKey(x => x.IdentityId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.ExpiresAt);
    }
}
