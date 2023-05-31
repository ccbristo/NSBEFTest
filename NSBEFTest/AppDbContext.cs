using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NSBEFTest;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions options)
        : base(options)
    {
        
    }

    public DbSet<MyData> Data { get; set; }
}

public class MyData
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public MyDataStatus Status { get; set; }
}

public enum MyDataStatus
{
    Pending,
    Complete
}