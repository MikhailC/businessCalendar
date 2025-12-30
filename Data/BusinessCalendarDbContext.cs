using BusinessCalendarAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace BusinessCalendarAPI.Data;

public sealed class BusinessCalendarDbContext : DbContext
{
    public BusinessCalendarDbContext(DbContextOptions<BusinessCalendarDbContext> options) : base(options)
    {
    }

    public DbSet<CalendarDayEntity> CalendarDays => Set<CalendarDayEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var calendarDay = modelBuilder.Entity<CalendarDayEntity>();

        calendarDay.HasKey(x => x.Id);

        calendarDay.HasIndex(x => new { x.Calendar, x.Date })
            .IsUnique();

        calendarDay.Property(x => x.Calendar)
            .HasMaxLength(32)
            .IsRequired();

        calendarDay.Property(x => x.Date)
            .IsRequired();

        calendarDay.Property(x => x.Year)
            .IsRequired();

        calendarDay.Property(x => x.DayType)
            .HasMaxLength(64)
            .IsRequired();
    }
}


