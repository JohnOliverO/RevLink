using Microsoft.EntityFrameworkCore;
namespace RevLink.Api;
public class Driver { public int Id {get;set;} public string Email {get;set;}=""; public string PasswordHash {get;set;}=""; public string DriverTag {get;set;}=""; public byte[]? Drivatar {get;set;} public long PhotoVersion {get;set;} }
public class Car { public int Id {get;set;} public int UserId {get;set;} public Driver User {get;set;}=null!; public string Nickname {get;set;}=""; public string Brand {get;set;}=""; public string Model {get;set;}=""; public int Year {get;set;} public string Engine {get;set;}=""; public string Style {get;set;}=""; public byte[] Photo {get;set;}=[]; public long PhotoVersion {get;set;} }
public class Post { public int Id {get;set;} public int UserId {get;set;} public Driver User {get;set;}=null!; public int? CarId {get;set;} public Car? Car {get;set;} public string Caption {get;set;}=""; public byte[] Photo {get;set;}=[]; public DateTime CreatedAt {get;set;}=DateTime.UtcNow; public List<PostLike> Likes {get;set;}=[]; }
public class PostLike { public int PostId {get;set;} public int UserId {get;set;} }
public class Meet { public int Id {get;set;} public int HostId {get;set;} public Driver Host {get;set;}=null!; public string Title {get;set;}=""; public string Description {get;set;}=""; public string Location {get;set;}=""; public DateTime StartAt {get;set;} public DateTime EndAt {get;set;} public int? MaxParticipants {get;set;} public List<EventMember> Members {get;set;}=[]; }
public class EventMember { public int EventId {get;set;} public int UserId {get;set;} public Driver User {get;set;}=null!; public DateTime JoinedAt {get;set;}=DateTime.UtcNow; }
public class RevDb(DbContextOptions<RevDb> options):DbContext(options) {
 public DbSet<Driver> Users=>Set<Driver>(); public DbSet<Car> Cars=>Set<Car>(); public DbSet<Post> Posts=>Set<Post>(); public DbSet<PostLike> PostLikes=>Set<PostLike>(); public DbSet<Meet> Events=>Set<Meet>(); public DbSet<EventMember> EventMembers=>Set<EventMember>();
 protected override void OnModelCreating(ModelBuilder b){
 b.Entity<Driver>().HasIndex(x=>x.Email).IsUnique();
 b.Entity<PostLike>().HasKey(x=>new{x.PostId,x.UserId});
 b.Entity<EventMember>().HasKey(x=>new{x.EventId,x.UserId});
 b.Entity<Post>().HasOne(x=>x.Car).WithMany().HasForeignKey(x=>x.CarId).OnDelete(DeleteBehavior.SetNull);
 b.Entity<Post>().HasMany(x=>x.Likes).WithOne().HasForeignKey(x=>x.PostId).OnDelete(DeleteBehavior.Cascade);
 b.Entity<PostLike>().HasOne<Driver>().WithMany().HasForeignKey(x=>x.UserId);
 b.Entity<Meet>().HasMany(x=>x.Members).WithOne().HasForeignKey(x=>x.EventId).OnDelete(DeleteBehavior.Cascade);
 }
}
public record RegisterRequest(string Email,string Password,string DriverTag);
public record LoginRequest(string Email,string Password);
public record ProfileRequest(string DriverTag,byte[]? Photo);
public record PasswordRequest(string CurrentPassword,string NewPassword);
public record CarRequest(string Nickname,string Brand,string Model,int Year,string Engine,string Style,byte[]? Photo);
public record PostRequest(string? Caption,int? CarId,byte[]? Photo);
public record EventRequest(string Title,string Description,string Location,DateTimeOffset StartAt,DateTimeOffset EndAt,int? MaxParticipants);
