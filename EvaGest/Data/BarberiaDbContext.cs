using EvaGest.Models;
using Microsoft.EntityFrameworkCore;

namespace EvaGest.Data;

public class BarberiaDbContext : DbContext
{
    public BarberiaDbContext(DbContextOptions<BarberiaDbContext> opcions) : base(opcions) { }

    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Treballadora> Treballadores => Set<Treballadora>();
    public DbSet<HorariTreballadora> HorarisTreballadora => Set<HorariTreballadora>();
    public DbSet<Servei> Serveis => Set<Servei>();
    public DbSet<Producte> Productes => Set<Producte>();
    public DbSet<MetodePagament> MetodesPagament => Set<MetodePagament>();
    public DbSet<Cita> Cites => Set<Cita>();
    public DbSet<Venda> Vendes => Set<Venda>();
    public DbSet<VendaLinia> VendaLinies => Set<VendaLinia>();
    public DbSet<VendaDesglossament> VendaDesglossaments => Set<VendaDesglossament>();
    public DbSet<MovimentCaixa> MovimentsCaixa => Set<MovimentCaixa>();
    public DbSet<HorariBarberia> HorariBarberia => Set<HorariBarberia>();
    public DbSet<DiaTancat> DiesTancats => Set<DiaTancat>();
    public DbSet<ConfiguracioItem> Configuracio => Set<ConfiguracioItem>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Enums are stored as text so the database stays readable and is not
        // broken by reordering the enum members later.
        b.Entity<Cita>().Property(e => e.Estat).HasConversion<string>();
        b.Entity<Venda>().Property(e => e.Estat).HasConversion<string>();
        b.Entity<Venda>().Property(e => e.IvaMode).HasConversion<string>();
        b.Entity<MovimentCaixa>().Property(e => e.Tipus).HasConversion<string>();
        b.Entity<HorariTreballadora>().Property(e => e.DiaSetmana).HasConversion<string>();
        b.Entity<HorariBarberia>().Property(e => e.DiaSetmana).HasConversion<string>();

        // Unique indexes
        b.Entity<Client>().HasIndex(e => e.ClientKey).IsUnique();
        b.Entity<MetodePagament>().HasIndex(e => e.Nom).IsUnique();
        b.Entity<DiaTancat>().HasIndex(e => e.Data).IsUnique();
        b.Entity<Venda>().HasIndex(e => e.CitaId).IsUnique();

        // Query indexes
        b.Entity<Cita>().HasIndex(e => new { e.Data, e.Hora });
        b.Entity<Venda>().HasIndex(e => e.Data);
        b.Entity<Venda>().HasIndex(e => e.Estat);
        b.Entity<MovimentCaixa>().HasIndex(e => e.Data);
        b.Entity<VendaDesglossament>().HasIndex(e => e.VendaId);

        // Deleting a client wipes their history; catalogue items are only deactivated,
        // so their references are nulled rather than cascaded.
        b.Entity<Cita>().HasOne(e => e.Client).WithMany(c => c.Cites)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<Venda>().HasOne(e => e.Client).WithMany(c => c.Vendes)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<VendaLinia>().HasOne(e => e.Venda).WithMany(v => v.Linies)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<VendaDesglossament>().HasOne(e => e.Venda).WithMany(v => v.Desglossaments)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<Cita>().HasOne(e => e.Servei).WithMany()
            .OnDelete(DeleteBehavior.SetNull);
        b.Entity<Cita>().HasOne(e => e.Treballadora).WithMany(t => t.Cites)
            .OnDelete(DeleteBehavior.SetNull);
        b.Entity<Venda>().HasOne(e => e.Cita).WithOne(c => c.Venda)
            .OnDelete(DeleteBehavior.SetNull);

        b.Entity<ConfiguracioItem>().HasKey(e => e.Clau);

        // Registered client XOR guest client
        b.Entity<Cita>().ToTable(t => t.HasCheckConstraint("ck_cites_client_xor",
            "(client_id IS NOT NULL AND nom_convidat IS NULL) OR " +
            "(client_id IS NULL AND nom_convidat IS NOT NULL)"));
        b.Entity<Venda>().ToTable(t => t.HasCheckConstraint("ck_vendes_client_xor",
            "(client_id IS NOT NULL AND nom_convidat IS NULL) OR " +
            "(client_id IS NULL AND nom_convidat IS NOT NULL)"));
    }
}
