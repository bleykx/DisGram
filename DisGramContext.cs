using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using DisGram.Models;

namespace DisGram
{
    public class DisGramContext : DbContext
    {
        public DbSet<Customer>? Customers { get; set; }
        public DbSet<Ticket>? Tickets { get; set; }
        public DbSet<Category>? Categories { get; set; }
        public DbSet<StaffMember>? StaffMembers { get; set; }
        public DbSet<Statistic>? Statistics { get; set; }
        private string DbPath;

        public DisGramContext(DbContextOptions<DisGramContext> options)
        : base(options)
        {
            var folder = Environment.SpecialFolder.LocalApplicationData;
            var path = Environment.GetFolderPath(folder);
            if (!Directory.Exists(Environment.GetFolderPath(folder) + "\\Disgram"))
                Directory.CreateDirectory(Environment.GetFolderPath(folder) + "\\Disgram");

            DbPath = System.IO.Path.Join(path + "\\Disgram\\Disgram.db");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var CustomerEntity = modelBuilder.Entity<Customer>();
            CustomerEntity.HasKey(e => e.Id);
            CustomerEntity.Property(e => e.Name).IsRequired();

            var TicketEntity = modelBuilder.Entity<Ticket>();
            TicketEntity.HasKey(e => e.Id);
            TicketEntity.Property(p => p.State).IsRequired();
            TicketEntity.Property(p => p.CreatedAt).IsRequired();
            TicketEntity.HasOne(o => o.Customer);
            TicketEntity.HasOne(o => o.StaffMember);
            TicketEntity.HasOne(o => o.Category);

            var CategoryEntity = modelBuilder.Entity<Category>();
            CategoryEntity.HasKey(e => e.Id);
            CategoryEntity.Property(e => e.Name).IsRequired();
            CategoryEntity.Property(e => e.ButtonLabel).IsRequired();
            CategoryEntity.Property(e => e.ButtonReply).IsRequired();

            var StaffMemberEntity = modelBuilder.Entity<StaffMember>();
            StaffMemberEntity.HasKey(e => e.Id);
            StaffMemberEntity.Property(e => e.Name).IsRequired();
            StaffMemberEntity.Property(e => e.Available).IsRequired();

            var StatisticEntity = modelBuilder.Entity<Statistic>();
            StatisticEntity.HasKey(e => e.Id);
            StatisticEntity.Property(e => e.Name).IsRequired();
            StatisticEntity.Property(e => e.Value).IsRequired();

            base.OnModelCreating(modelBuilder);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseSqlite($"Data Source={DbPath}");
        }

    }
}
