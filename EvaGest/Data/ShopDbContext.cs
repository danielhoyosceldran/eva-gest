using EvaGest.Models;
using Microsoft.EntityFrameworkCore;

namespace EvaGest.Data;

public class ShopDbContext : DbContext
{
    public ShopDbContext(DbContextOptions<ShopDbContext> options) : base(options) { }

    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Worker> Workers => Set<Worker>();
    public DbSet<WorkerSchedule> WorkerSchedules => Set<WorkerSchedule>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleLine> SaleLines => Set<SaleLine>();
    public DbSet<SaleBreakdown> SaleBreakdowns => Set<SaleBreakdown>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();
    public DbSet<ShopSchedule> ShopSchedule => Set<ShopSchedule>();
    public DbSet<ClosedDay> ClosedDays => Set<ClosedDay>();
    public DbSet<SettingItem> Settings => Set<SettingItem>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Enums are stored as text so the database stays readable and is not
        // broken by reordering the enum members later.
        b.Entity<Appointment>().Property(e => e.Status).HasConversion<string>();
        b.Entity<Sale>().Property(e => e.Status).HasConversion<string>();
        b.Entity<Sale>().Property(e => e.VatMode).HasConversion<string>();
        b.Entity<CashMovement>().Property(e => e.Type).HasConversion<string>();
        b.Entity<WorkerSchedule>().Property(e => e.Weekday).HasConversion<string>();
        b.Entity<ShopSchedule>().Property(e => e.Weekday).HasConversion<string>();

        // Unique indexes
        b.Entity<Client>().HasIndex(e => e.ClientKey).IsUnique();
        b.Entity<PaymentMethod>().HasIndex(e => e.Name).IsUnique();
        b.Entity<ExpenseCategory>().HasIndex(e => e.Name).IsUnique();
        b.Entity<ClosedDay>().HasIndex(e => e.Date).IsUnique();
        b.Entity<Sale>().HasIndex(e => e.AppointmentId).IsUnique();

        // Query indexes
        b.Entity<Appointment>().HasIndex(e => new { e.Date, e.Time });
        b.Entity<Sale>().HasIndex(e => e.Date);
        b.Entity<Sale>().HasIndex(e => e.Status);
        b.Entity<CashMovement>().HasIndex(e => e.Date);
        b.Entity<SaleBreakdown>().HasIndex(e => e.SaleId);

        // Deleting a client wipes their history; catalogue items are only deactivated,
        // so their references are nulled rather than cascaded.
        b.Entity<Appointment>().HasOne(e => e.Client).WithMany(c => c.Appointments)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<Sale>().HasOne(e => e.Client).WithMany(c => c.Sales)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<SaleLine>().HasOne(e => e.Sale).WithMany(v => v.Lines)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<SaleBreakdown>().HasOne(e => e.Sale).WithMany(v => v.Breakdowns)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<Appointment>().HasOne(e => e.Service).WithMany()
            .OnDelete(DeleteBehavior.SetNull);
        b.Entity<Appointment>().HasOne(e => e.Worker).WithMany(t => t.Appointments)
            .OnDelete(DeleteBehavior.SetNull);
        b.Entity<Sale>().HasOne(e => e.Appointment).WithOne(c => c.Sale)
            .OnDelete(DeleteBehavior.SetNull);
        b.Entity<CashMovement>().HasOne(e => e.Category).WithMany()
            .OnDelete(DeleteBehavior.SetNull);
        b.Entity<CashMovement>().HasOne(e => e.Worker).WithMany(t => t.CashMovements)
            .OnDelete(DeleteBehavior.SetNull);

        b.Entity<SettingItem>().HasKey(e => e.Key);

        // Registered client XOR guest client
        b.Entity<Appointment>().ToTable(t => t.HasCheckConstraint("ck_appointments_client_xor",
            "(client_id IS NOT NULL AND guest_name IS NULL) OR " +
            "(client_id IS NULL AND guest_name IS NOT NULL)"));
        b.Entity<Sale>().ToTable(t => t.HasCheckConstraint("ck_sales_client_xor",
            "(client_id IS NOT NULL AND guest_name IS NULL) OR " +
            "(client_id IS NULL AND guest_name IS NOT NULL)"));
    }
}
