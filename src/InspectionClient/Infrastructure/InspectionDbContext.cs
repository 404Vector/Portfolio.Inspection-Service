using System;
using Core.Enums;
using Core.Models;
using InspectionClient.Infrastructure.Entities;
using InspectionClient.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InspectionClient.Infrastructure;

public class InspectionDbContext : DbContext {

  public DbSet<RecipeEntity> Recipes => Set<RecipeEntity>();
  public DbSet<DieSpotRecipeEntity> DieSpotRecipes => Set<DieSpotRecipeEntity>();
  public DbSet<WaferInfoEntity> WaferInfos => Set<WaferInfoEntity>();
  public DbSet<InspectionResultEntity> InspectionResults => Set<InspectionResultEntity>();
  public DbSet<UserAnnotationEntity> UserAnnotations => Set<UserAnnotationEntity>();
  public DbSet<DieRenderingParametersEntity> DieRenderingParameters => Set<DieRenderingParametersEntity>();

  public InspectionDbContext(DbContextOptions<InspectionDbContext> options) : base(options) {
  }

  protected override void OnModelCreating(ModelBuilder modelBuilder) {
    // ── Recipe ──────────────────────────────────────────────────────────
    modelBuilder.Entity<RecipeEntity>(e => {
      e.ToTable("Recipe");
      e.HasKey(r => r.Id);
      e.Property(r => r.Id).ValueGeneratedOnAdd();
      e.HasIndex(r => r.Name).IsUnique();
    });

    // ── DieSpotRecipe ──────────────────────────────────────────────────
    modelBuilder.Entity<DieSpotRecipeEntity>(e => {
      e.ToTable("DieSpotRecipe");
      e.HasKey(r => r.Id);
      e.Property(r => r.Id).ValueGeneratedOnAdd();
      e.HasIndex(r => r.Name).IsUnique();
    });

    // ── DieRenderingParameters ─────────────────────────────────────────
    modelBuilder.Entity<DieRenderingParametersEntity>(e => {
      e.ToTable("DieRenderingParameters");
      e.HasKey(r => r.Id);
      e.Property(r => r.Id).ValueGeneratedOnAdd();
      e.HasIndex(r => r.Name).IsUnique();
    });

    // ── WaferInfo ──────────────────────────────────────────────────────
    modelBuilder.Entity<WaferInfoEntity>(e => {
      e.ToTable("WaferInfo");
      e.HasKey(r => r.Id);
      e.Property(r => r.Id).ValueGeneratedOnAdd();
      e.HasIndex(r => r.Name).IsUnique();
      e.HasOne(r => r.DieParameters)
       .WithMany()
       .HasForeignKey(r => r.DieParametersId)
       .OnDelete(DeleteBehavior.SetNull);
    });

    // ── InspectionResult ───────────────────────────────────────────────
    modelBuilder.Entity<InspectionResultEntity>(e => {
      e.ToTable("InspectionResult");
      e.HasKey(r => r.ResultId);
    });

    // ── UserAnnotation ─────────────────────────────────────────────────
    modelBuilder.Entity<UserAnnotationEntity>(e => {
      e.ToTable("UserAnnotation");
      e.HasKey(r => r.Id);
      e.Property(r => r.Id).ValueGeneratedOnAdd();
      e.HasIndex(r => new { r.EntityId, r.EntityKind })
       .HasDatabaseName("idx_annotation_entity");
    });

    SeedData(modelBuilder);
  }

  // ── Seed Data ──────────────────────────────────────────────────────────

  private void SeedData(ModelBuilder modelBuilder) {
    var now = DateTimeOffset.UtcNow.ToString("O");

    // DieRenderingParameters
    var dieParamsJson = Serialize(new {
      canvasWidth         = 10_000,
      canvasHeight        = 10_000,
      backgroundGray      = 70,
      showAlignmentMarks  = true,
      showRuler           = true,
      showTextureBands    = true,
      showCalibrationMark = true,
      padRowCount         = 1,
      padColumnCount      = 6,
    });
    modelBuilder.Entity<DieRenderingParametersEntity>().HasData(
      new DieRenderingParametersEntity { Id = 1, Name = "SEED-DEFAULT", Json = dieParamsJson });

    // WaferInfo
    var dummy = WaferInfo.CreateDummy();
    var waferJson = Serialize(dummy);
    modelBuilder.Entity<WaferInfoEntity>().HasData(
      new WaferInfoEntity {
        Id = 1, Name = dummy.WaferId,
        WaferType = dummy.WaferType.ToString(),
        CreatedAt = dummy.CreatedAt.ToString("O"),
        Json = waferJson,
      });

    // Recipe
    var recipe = new InspectionRecipe.Models.WaferSurfaceInspectionRecipe(
      RecipeName: "SEED-RECIPE", Description: "Seed dummy recipe",
      Fov: new FovSize(1413.0, 1035.0));
    modelBuilder.Entity<RecipeEntity>().HasData(
      new RecipeEntity { Id = 1, Name = recipe.RecipeName, CreatedAt = now, Json = Serialize(recipe) });

    // InspectionResult
    var seedResultId = "00000000-0000-0000-0000-000000000001";
    var startedAt = DateTimeOffset.UtcNow;
    var result = new WaferSurfaceInspectionResult(
      RecipeName: "SEED-RECIPE", WaferId: dummy.WaferId,
      Status: InspectionStatus.Pass,
      StartedAt: startedAt, CompletedAt: startedAt.AddSeconds(1),
      FrameResults: Array.Empty<FrameInspectionResult>());
    modelBuilder.Entity<InspectionResultEntity>().HasData(
      new InspectionResultEntity {
        ResultId = seedResultId, RecipeName = result.RecipeName,
        WaferId = result.WaferId, Status = result.Status.ToString(),
        StartedAt = result.StartedAt.ToString("O"),
        CompletedAt = result.CompletedAt.ToString("O"),
        Json = Serialize(result),
      });

    // UserAnnotations
    modelBuilder.Entity<UserAnnotationEntity>().HasData(
      new UserAnnotationEntity {
        Id = 1, EntityId = dummy.WaferId, EntityKind = EntityKind.WaferInfo.ToString(),
        Operator = "seed", Comment = "Seed dummy annotation", Tags = "seed,dummy", CreatedAt = now,
      },
      new UserAnnotationEntity {
        Id = 2, EntityId = "SEED-RECIPE", EntityKind = EntityKind.Recipe.ToString(),
        Operator = "seed", Comment = "Seed dummy annotation", Tags = "seed,dummy", CreatedAt = now,
      },
      new UserAnnotationEntity {
        Id = 3, EntityId = seedResultId, EntityKind = EntityKind.InspectionResult.ToString(),
        Operator = "seed", Comment = "Seed dummy annotation", Tags = "seed,dummy", CreatedAt = now,
      });
  }

  private static string Serialize<T>(T value) =>
      System.Text.Json.JsonSerializer.Serialize(value, RepositoryJsonOptions.Default);
}
