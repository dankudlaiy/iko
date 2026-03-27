using iko_host.Models;
using Microsoft.EntityFrameworkCore;

namespace iko_host.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<ConnectedAccount> ConnectedAccounts => Set<ConnectedAccount>();
    public DbSet<IkoPlaylist> IkoPlaylists => Set<IkoPlaylist>();
    public DbSet<IkoPlaylistTrack> IkoPlaylistTracks => Set<IkoPlaylistTrack>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<ConnectedAccount>(entity =>
        {
            entity.HasKey(ca => ca.Id);
            entity.HasOne(ca => ca.User)
                .WithMany(u => u.ConnectedAccounts)
                .HasForeignKey(ca => ca.UserId);
            entity.HasIndex(ca => new { ca.UserId, ca.Platform }).IsUnique();
        });

        modelBuilder.Entity<IkoPlaylist>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId);
        });

        modelBuilder.Entity<IkoPlaylistTrack>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.HasOne(t => t.Playlist)
                .WithMany(p => p.Tracks)
                .HasForeignKey(t => t.PlaylistId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(t => new { t.PlaylistId, t.Platform, t.PlatformTrackId }).IsUnique();
        });
    }
}
