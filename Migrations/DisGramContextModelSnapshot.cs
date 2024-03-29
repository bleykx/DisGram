﻿// <auto-generated />
using System;
using DisGram;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace DisGram.Migrations
{
    [DbContext(typeof(DisGramContext))]
    partial class DisGramContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "6.0.5");

            modelBuilder.Entity("DisGram.Models.Category", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("ButtonLabel")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("ButtonReply")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<bool>("Enabled")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Categories");
                });

            modelBuilder.Entity("DisGram.Models.Customer", b =>
                {
                    b.Property<ulong>("Id")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Customers");
                });

            modelBuilder.Entity("DisGram.Models.StaffMember", b =>
                {
                    b.Property<ulong>("Id")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("Available")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("StaffMembers");
                });

            modelBuilder.Entity("DisGram.Models.Statistic", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int?>("CategoryId")
                        .HasColumnType("INTEGER");

                    b.Property<ulong?>("CustomerId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<ulong?>("StaffMemberId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Value")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("CategoryId");

                    b.HasIndex("CustomerId");

                    b.HasIndex("StaffMemberId");

                    b.ToTable("Statistics");
                });

            modelBuilder.Entity("DisGram.Models.Ticket", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int?>("CategoryId")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime?>("CreatedAt")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<ulong?>("CustomerId")
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("DiscordChannelId")
                        .HasColumnType("INTEGER");

                    b.Property<ulong?>("StaffMemberId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("State")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("CategoryId");

                    b.HasIndex("CustomerId");

                    b.HasIndex("StaffMemberId");

                    b.ToTable("Tickets");
                });

            modelBuilder.Entity("DisGram.Models.Statistic", b =>
                {
                    b.HasOne("DisGram.Models.Category", "Category")
                        .WithMany()
                        .HasForeignKey("CategoryId");

                    b.HasOne("DisGram.Models.Customer", "Customer")
                        .WithMany()
                        .HasForeignKey("CustomerId");

                    b.HasOne("DisGram.Models.StaffMember", "StaffMember")
                        .WithMany()
                        .HasForeignKey("StaffMemberId");

                    b.Navigation("Category");

                    b.Navigation("Customer");

                    b.Navigation("StaffMember");
                });

            modelBuilder.Entity("DisGram.Models.Ticket", b =>
                {
                    b.HasOne("DisGram.Models.Category", "Category")
                        .WithMany()
                        .HasForeignKey("CategoryId");

                    b.HasOne("DisGram.Models.Customer", "Customer")
                        .WithMany()
                        .HasForeignKey("CustomerId");

                    b.HasOne("DisGram.Models.StaffMember", "StaffMember")
                        .WithMany()
                        .HasForeignKey("StaffMemberId");

                    b.Navigation("Category");

                    b.Navigation("Customer");

                    b.Navigation("StaffMember");
                });
#pragma warning restore 612, 618
        }
    }
}
