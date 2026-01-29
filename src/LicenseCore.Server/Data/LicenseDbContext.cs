using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace LicenseCore.Server.Data;

public sealed class LicenseDbContext : DbContext
{
    public LicenseDbContext(DbContextOptions<LicenseDbContext> options) : base(options) { }

    public DbSet<License> Licenses => Set<License>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        var e = modelBuilder.Entity<License>();
        e.ToTable("Licenses");

        e.HasKey(x => x.Key);
        e.Property(x => x.Key).IsRequired().HasMaxLength(128);

        // HYBRID UPGRADE: Beide Public Keys + ML-KEM Private Key
        e.Property(x => x.EcdsaPublicKey).IsRequired();
        e.Property(x => x.MlKemPublicKey).IsRequired();
        e.Property(x => x.MlKemPrivateKey).IsRequired(); // NEU (für Decapsulation)

        e.Property(x => x.Hash).IsRequired();
        e.Property(x => x.Nonce).IsRequired(false);
        e.Property(x => x.NonceExpiresAtUtc).IsRequired(false);

        e.Property(x => x.RowVersion).IsConcurrencyToken();
    }
}

public sealed class License
{
    [Key]
    [MaxLength(128)]
    public required string Key { get; init; }

    // Classic crypto
    public required byte[] EcdsaPublicKey { get; set; }

    // Post-Quantum crypto
    public required byte[] MlKemPublicKey { get; set; }
    public required byte[] MlKemPrivateKey { get; set; } // SECURITY: Dev only, migrate to HSM in prod!

    public required byte[] Hash { get; set; }
    
    // Challenge-Response
    public byte[]? Nonce { get; set; }
    public DateTimeOffset? NonceExpiresAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = Guid.NewGuid().ToByteArray();
}
