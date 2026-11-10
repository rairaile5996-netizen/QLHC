using DocumentManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace DocumentManagement.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents { get; set; } = null!;
    public DbSet<ContractAppendix> ContractAppendices { get; set; } = null!;
    public DbSet<AdvancePayment> AdvancePayments { get; set; } = null!;
    public DbSet<Payment> Payments { get; set; } = null!;
    public DbSet<InitialDocumentChecklistItem> InitialDocumentChecklistItems { get; set; } = null!;
    public DbSet<GuaranteeLetter> GuaranteeLetters { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PasswordHash).IsRequired();
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ContractNumber).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ProjectName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ItemName).HasMaxLength(200);
            entity.Property(e => e.ConstructionName).HasMaxLength(200);
            entity.Property(e => e.EmployerName).HasMaxLength(200);
            entity.Property(e => e.InvestorName).HasMaxLength(200);
            entity.Property(e => e.ContractorName).HasMaxLength(200);
            entity.Property(e => e.EmployerCode).HasMaxLength(50);
            entity.Property(e => e.ContractorCode).HasMaxLength(50);
            entity.Property(e => e.SignDate).IsRequired();
            entity.Property(e => e.ContractValue).HasPrecision(18, 2);
            entity.Property(e => e.SettlementValue).HasPrecision(18, 2);
            entity.Property(e => e.RemainingPaymentValue).HasPrecision(18, 2);
            entity.Property(e => e.WarrantyValue).HasPrecision(18, 2);
            entity.Property(e => e.PaidPercentage).HasPrecision(5, 2);
            entity.Property(e => e.PaymentStatus).HasMaxLength(50);
            entity.Property(e => e.IsCompleted).HasDefaultValue(false);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.Location).HasMaxLength(500);
            entity.Property(e => e.PlannedAdvanceValue).HasPrecision(18, 2);
            entity.Property(e => e.FilePath).HasMaxLength(500);
            entity.Property(e => e.CreatedDate).IsRequired();
            entity.Ignore(e => e.TotalValue);
            entity.Ignore(e => e.TotalAppendixValue);
            entity.Ignore(e => e.TotalAdvance);
            entity.Ignore(e => e.TotalPayment);
            entity.Ignore(e => e.RemainingAmount);
            entity.HasMany(e => e.Appendices).WithOne().HasForeignKey(a => a.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.Advances).WithOne().HasForeignKey(a => a.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.Payments).WithOne().HasForeignKey(p => p.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.InitialDocumentChecklist).WithOne().HasForeignKey(i => i.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.GuaranteeLetters).WithOne(g => g.Document).HasForeignKey(g => g.DocumentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContractAppendix>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AppendixNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.SignDate).IsRequired();
            entity.Property(e => e.Value).HasPrecision(18, 2);
            entity.Property(e => e.PlannedAdvanceValue).HasPrecision(18, 2);
        });

        modelBuilder.Entity<AdvancePayment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.PaymentDate).IsRequired();
            entity.Property(e => e.Value).HasPrecision(18, 2);
            entity.Ignore(e => e.Percentage);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.PaymentDate).IsRequired();
            entity.Property(e => e.Value).HasPrecision(18, 2);
            entity.Ignore(e => e.Percentage);
        });

        modelBuilder.Entity<InitialDocumentChecklistItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.FilePath).HasMaxLength(1000);
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Ignore(e => e.IsDone);
            entity.Ignore(e => e.HasFile);
        });

        modelBuilder.Entity<GuaranteeLetter>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.LifecycleStatus).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.LetterNumber).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IssuingBank).IsRequired().HasMaxLength(200);
            entity.Property(e => e.RelatedAppendixLabel).HasMaxLength(200);
            entity.Property(e => e.GuaranteeValue).HasPrecision(18, 2);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.PdfFilePath).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.CreatedDate).IsRequired();

            entity.HasOne(e => e.ParentGuarantee)
                .WithMany(e => e.Amendments)
                .HasForeignKey(e => e.ParentGuaranteeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.RelatedAppendix)
                .WithMany()
                .HasForeignKey(e => e.RelatedAppendixId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.ParentGuaranteeId);
            entity.HasIndex(e => new { e.DocumentId, e.RelatedAppendixId });
            entity.HasIndex(e => new { e.DocumentId, e.LifecycleStatus, e.ExpiryDate });

            entity.Ignore(e => e.DaysRemaining);
            entity.Ignore(e => e.AlertSeverity);
            entity.Ignore(e => e.IsActionable);
            entity.Ignore(e => e.RelationDisplayLabel);
            entity.Ignore(e => e.TypeDisplay);
            entity.Ignore(e => e.LifecycleDisplay);
            entity.Ignore(e => e.DisplayStatus);
        });
    }
}
