using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SistemaInventario.Api.Domain.Entities;

namespace SistemaInventario.Api.Infrastructure.Database;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Usuario> Usuarios { get; set; } = null!;
    public DbSet<Elemento> Elementos { get; set; } = null!;
    public DbSet<Revision> Revisiones { get; set; } = null!;
    public DbSet<RevisionDetalle> RevisionDetalles { get; set; } = null!;
    public DbSet<Imagen> Imagenes { get; set; } = null!;
}

// 2. El Factory que solucionará el problema de la migración
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // Busca el archivo appsettings.json en la raíz del proyecto
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        // Le indicamos que para crear la migración, DEBE usar SQL Server
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>();
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        builder.UseSqlServer(connectionString);

        return new ApplicationDbContext(builder.Options);
    }
}