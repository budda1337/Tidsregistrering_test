using Microsoft.EntityFrameworkCore;
using Tidsregistrering.Models;

namespace Tidsregistrering.Data
{
    public class TidsregistreringContext : DbContext
    {
        public TidsregistreringContext(DbContextOptions<TidsregistreringContext> options)
            : base(options)
        {
        }

        public DbSet<Registrering> Registreringer { get; set; }
        public DbSet<MasterAfdeling> MasterAfdelinger { get; set; }
        public DbSet<Administrator> Administratorer { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Registrering entity configuration
            modelBuilder.Entity<Registrering>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Afdeling).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Brugernavn).IsRequired().HasMaxLength(50);
                entity.Property(e => e.FuldeNavn).HasMaxLength(100);
                entity.Property(e => e.OuAfdeling).HasMaxLength(100);
                entity.Property(e => e.Bemærkninger).HasMaxLength(1000);
                // NYT FELT - Sagsnummer
                entity.Property(e => e.Sagsnummer).HasMaxLength(60);
            });

            // MasterAfdeling entity configuration
            modelBuilder.Entity<MasterAfdeling>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Navn).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Aktiv).IsRequired();
                entity.Property(e => e.OprettetAf).HasMaxLength(100);
                entity.Property(e => e.OpdateretAf).HasMaxLength(100);

                // Unique constraint på navn
                entity.HasIndex(e => e.Navn).IsUnique();
            });

            // Administrator entity configuration
            modelBuilder.Entity<Administrator>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Brugernavn).IsRequired().HasMaxLength(100);
                entity.Property(e => e.FuldeNavn).HasMaxLength(100);
                entity.Property(e => e.Aktiv).IsRequired();
                entity.Property(e => e.OprettetAf).HasMaxLength(100);
                entity.Property(e => e.OpdateretAf).HasMaxLength(100);
                entity.Property(e => e.Bemærkninger).HasMaxLength(200);

                // Unique constraint på brugernavn
                entity.HasIndex(e => e.Brugernavn).IsUnique();
            });

            // Seed data - tilføj eksisterende afdelinger
            modelBuilder.Entity<MasterAfdeling>().HasData(
                new MasterAfdeling { Id = 1, Navn = "Arbejdsmarkedsafdelingen", Aktiv = true, Oprettet = DateTime.Now, OprettetAf = "SYSTEM" },
                new MasterAfdeling { Id = 2, Navn = "Børne- og Familieafdelingen", Aktiv = true, Oprettet = DateTime.Now, OprettetAf = "SYSTEM" },
                new MasterAfdeling { Id = 3, Navn = "Dagtilbudsafdelingen", Aktiv = true, Oprettet = DateTime.Now, OprettetAf = "SYSTEM" },
                new MasterAfdeling { Id = 4, Navn = "Erhvervsafdelingen og Ledelsessekretariat", Aktiv = true, Oprettet = DateTime.Now, OprettetAf = "SYSTEM" },
                new MasterAfdeling { Id = 5, Navn = "Fælles", Aktiv = true, Oprettet = DateTime.Now, OprettetAf = "SYSTEM" },
                new MasterAfdeling { Id = 6, Navn = "IT-afdelingen", Aktiv = true, Oprettet = DateTime.Now, OprettetAf = "SYSTEM" },
                new MasterAfdeling { Id = 7, Navn = "Kultur- og Fritidsafdelingen", Aktiv = true, Oprettet = DateTime.Now, OprettetAf = "SYSTEM" },
                new MasterAfdeling { Id = 8, Navn = "Skoleafdelingen", Aktiv = true, Oprettet = DateTime.Now, OprettetAf = "SYSTEM" },
                new MasterAfdeling { Id = 9, Navn = "Socialafdelingen", Aktiv = true, Oprettet = DateTime.Now, OprettetAf = "SYSTEM" },
                new MasterAfdeling { Id = 10, Navn = "Sundheds- og Ældreafdelingen", Aktiv = true, Oprettet = DateTime.Now, OprettetAf = "SYSTEM" },
                new MasterAfdeling { Id = 11, Navn = "Teknik- og Miljøafdelingen", Aktiv = true, Oprettet = DateTime.Now, OprettetAf = "SYSTEM" },
                new MasterAfdeling { Id = 12, Navn = "Økonomi- og Personaleafdelingen", Aktiv = true, Oprettet = DateTime.Now, OprettetAf = "SYSTEM" }
            );

            // Seed data - første administrator
            modelBuilder.Entity<Administrator>().HasData(
                new Administrator
                {
                    Id = 1,
                    Brugernavn = "IBK\\chrijen",
                    FuldeNavn = "System Administrator",
                    Aktiv = true,
                    Oprettet = DateTime.Now,
                    OprettetAf = "SYSTEM",
                    Bemærkninger = "Første administrator - oprettet automatisk"
                }
            );
        }
    }
}