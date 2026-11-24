using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using RevLink.Api;

var builder=WebApplication.CreateBuilder(args);
var dbPath=Path.GetFullPath(builder.Configuration["DatabasePath"] ?? "revlink.db");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
// A local random key survives restarts; set Jwt__Key for any deployed service.
var secret=builder.Configuration["Jwt:Key"];
if(string.IsNullOrWhiteSpace(secret)){
 var keyPath=dbPath+".key";
 if(!File.Exists(keyPath)) File.WriteAllText(keyPath,Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
 secret=File.ReadAllText(keyPath).Trim();
}
if(Encoding.UTF8.GetByteCount(secret)<32) throw new InvalidOperationException("Jwt:Key precisa ter pelo menos 32 bytes.");
var key=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
builder.Services.AddDbContext<RevDb>(o=>o.UseSqlite(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder{DataSource=dbPath,ForeignKeys=true}.ToString()));
builder.Services.AddScoped<PasswordHasher<Driver>>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o=>{
 o.MapInboundClaims=false; o.TokenValidationParameters=new(){ValidateIssuer=true,ValidIssuer="RevLink",ValidateAudience=true,ValidAudience="RevLink.Mobile",ValidateLifetime=true,ValidateIssuerSigningKey=true,IssuerSigningKey=key,ClockSkew=TimeSpan.FromSeconds(30)};
});
builder.Services.AddAuthorization(); builder.Services.AddOpenApi();
builder.WebHost.ConfigureKestrel(o=>o.Limits.MaxRequestBodySize=8*1024*1024);
var app=builder.Build();
using(var scope=app.Services.CreateScope()) { var db=scope.ServiceProvider.GetRequiredService<RevDb>(); await db.Database.EnsureCreatedAsync(); }
app.UseAuthentication(); app.UseAuthorization(); app.MapOpenApi();
app.MapGet("/health",()=>new {status="ok",app="RevLink"});
var api=app.MapGroup("/api").RequireAuthorization();
static int Uid(ClaimsPrincipal p)=>int.Parse(p.FindFirstValue("sub")!);
static bool TextOk(string? s,int max)=>!string.IsNullOrWhiteSpace(s)&&s.Trim().Length<=max;
static bool PhotoOk(byte[]? b)=>b is {Length:>3 and <= 2_000_000} && ((b[0]==0xff&&b[1]==0xd8&&b[2]==0xff)||(b.Length>=8&&b.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})));
static IResult Bad(string s)=>Results.BadRequest(new{message=s});
static IResult Missing()=>Results.NotFound(new{message="Registro não encontrado."});
static IResult Denied()=>Results.Json(new{message="Você não tem permissão."},statusCode:403);
static string Email(string? s)=>(s??"").Trim().ToLowerInvariant();
static bool ValidEmail(string s)=>s.Length<=254 && System.Net.Mail.MailAddress.TryCreate(s,out var a)&&a.Address==s;
static bool ValidPassword(string? p)=>p is {Length:>=6 and <=128};
static string? Avatar(Driver d)=>d.Drivatar==null?null:$"/api/images/users/{d.Id}?v={d.PhotoVersion}";
static object Profile(Driver d)=>new{id=d.Id,d.Email,d.DriverTag,drivatarUrl=Avatar(d)};
static object CarDto(Car c)=>new{c.Id,c.UserId,ownerTag=c.User.DriverTag,c.Nickname,c.Brand,c.Model,c.Year,c.Engine,c.Style,photoUrl=$"/api/images/cars/{c.Id}?v={c.PhotoVersion}"};
static object PostDto(Post p,int uid)=>new{p.Id,p.UserId,driverTag=p.User.DriverTag,drivatarUrl=Avatar(p.User),p.CarId,carNickname=p.Car?.Nickname,p.Caption,createdAt=DateTime.SpecifyKind(p.CreatedAt,DateTimeKind.Utc),photoUrl=$"/api/images/posts/{p.Id}",likeCount=p.Likes.Count,liked=p.Likes.Any(x=>x.UserId==uid)};
static object EventDto(Meet e,int uid)=>new{e.Id,e.HostId,hostTag=e.Host.DriverTag,e.Title,e.Description,e.Location,startAt=DateTime.SpecifyKind(e.StartAt,DateTimeKind.Utc),endAt=DateTime.SpecifyKind(e.EndAt,DateTimeKind.Utc),e.MaxParticipants,participantCount=e.Members.Count,joined=e.Members.Any(m=>m.UserId==uid),isHost=e.HostId==uid};
string Token(Driver d)=>new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("RevLink","RevLink.Mobile",[new Claim("sub",d.Id.ToString())],expires:DateTime.UtcNow.AddDays(7),signingCredentials:new SigningCredentials(key,SecurityAlgorithms.HmacSha256)));
api.MapPost("/auth/register",async Task<IResult>(RegisterRequest r,RevDb db,PasswordHasher<Driver> hasher)=>{
 var email=Email(r.Email); if(!ValidEmail(email)||!ValidPassword(r.Password)||!TextOk(r.DriverTag,40))return Bad("Informe e-mail válido, DriverTag e senha de 6 a 128 caracteres.");
 if(await db.Users.AnyAsync(x=>x.Email==email))return Results.Conflict(new{message="E-mail já cadastrado."});
 var d=new Driver{Email=email,DriverTag=r.DriverTag.Trim()};d.PasswordHash=hasher.HashPassword(d,r.Password);db.Users.Add(d);
 try{await db.SaveChangesAsync();}catch(DbUpdateException){return Results.Conflict(new{message="Não foi possível cadastrar; verifique o e-mail."});}
 return Results.Ok(new{token=Token(d),user=Profile(d)});
}).AllowAnonymous();
api.MapPost("/auth/login",async Task<IResult>(LoginRequest r,RevDb db,PasswordHasher<Driver> hasher)=>{
 var email=Email(r.Email);
 var d=await db.Users.FirstOrDefaultAsync(x=>x.Email==email);
 if(d==null||!ValidPassword(r.Password)||hasher.VerifyHashedPassword(d,d.PasswordHash,r.Password)==PasswordVerificationResult.Failed)return Results.Json(new{message="E-mail ou senha incorretos."},statusCode:401);
 return Results.Ok(new{token=Token(d),user=Profile(d)});
}).AllowAnonymous();
api.MapGet("/users/me",async Task<IResult>(ClaimsPrincipal p,RevDb db)=>{var d=await db.Users.FindAsync(Uid(p));return d==null?Missing():Results.Ok(Profile(d));});
api.MapPut("/users/me",async Task<IResult>(ProfileRequest r,ClaimsPrincipal p,RevDb db)=>{
 var d=await db.Users.FindAsync(Uid(p));if(d==null)return Missing();if(!TextOk(r.DriverTag,40)||r.Photo!=null&&!PhotoOk(r.Photo))return Bad("Nome ou foto inválida.");d.DriverTag=r.DriverTag.Trim();if(r.Photo!=null){d.Drivatar=r.Photo;d.PhotoVersion=DateTime.UtcNow.Ticks;}await db.SaveChangesAsync();return Results.Ok(Profile(d));
});
api.MapPut("/users/me/password",async Task<IResult>(PasswordRequest r,ClaimsPrincipal p,RevDb db,PasswordHasher<Driver> h)=>{
 var d=await db.Users.FindAsync(Uid(p));if(d==null)return Missing();if(!ValidPassword(r.CurrentPassword)||h.VerifyHashedPassword(d,d.PasswordHash,r.CurrentPassword)==PasswordVerificationResult.Failed)return Bad("Senha atual incorreta.");if(!ValidPassword(r.NewPassword))return Bad("A nova senha deve ter 6 a 128 caracteres.");d.PasswordHash=h.HashPassword(d,r.NewPassword);await db.SaveChangesAsync();return Results.NoContent();
});
static bool ValidCar(CarRequest r)=>TextOk(r.Nickname,50)&&TextOk(r.Brand,50)&&TextOk(r.Model,80)&&TextOk(r.Engine,80)&&r.Year>=1886&&r.Year<=DateTime.UtcNow.Year+2&&new[]{"Stock","Street","JDM","Stance","Track","Classic","Off-road","Custom"}.Contains(r.Style);
static void SetCar(Car c,CarRequest r){c.Nickname=r.Nickname.Trim();c.Brand=r.Brand.Trim();c.Model=r.Model.Trim();c.Year=r.Year;c.Engine=r.Engine.Trim();c.Style=r.Style;if(r.Photo!=null){c.Photo=r.Photo;c.PhotoVersion=DateTime.UtcNow.Ticks;}}
api.MapGet("/cars",async (RevDb db,string? q)=>{var query=db.Cars.Include(c=>c.User).AsQueryable();if(!string.IsNullOrWhiteSpace(q)){var t=q.Trim();query=query.Where(c=>c.Brand.Contains(t)||c.Model.Contains(t)||c.Nickname.Contains(t));}return (await query.OrderByDescending(c=>c.Id).ToListAsync()).Select(CarDto);});
api.MapGet("/cars/mine",async (ClaimsPrincipal p,RevDb db)=>{
 var uid=Uid(p);
 return (await db.Cars.Include(c=>c.User).Where(c=>c.UserId==uid).OrderByDescending(c=>c.Id).ToListAsync()).Select(CarDto);
});
api.MapGet("/cars/{id:int}",async Task<IResult>(int id,RevDb db)=>{var c=await db.Cars.Include(c=>c.User).FirstOrDefaultAsync(c=>c.Id==id);return c==null?Missing():Results.Ok(CarDto(c));});
api.MapPost("/cars",async Task<IResult>(CarRequest r,ClaimsPrincipal p,RevDb db)=>{if(!ValidCar(r)||!PhotoOk(r.Photo))return Bad("Preencha os campos e selecione uma foto válida.");var c=new Car{UserId=Uid(p)};SetCar(c,r);db.Cars.Add(c);await db.SaveChangesAsync();return Results.Ok(new{c.Id});});
api.MapPut("/cars/{id:int}",async Task<IResult>(int id,CarRequest r,ClaimsPrincipal p,RevDb db)=>{var c=await db.Cars.FindAsync(id);if(c==null)return Missing();if(c.UserId!=Uid(p))return Denied();if(!ValidCar(r)||r.Photo!=null&&!PhotoOk(r.Photo))return Bad("Dados inválidos.");SetCar(c,r);await db.SaveChangesAsync();return Results.NoContent();});
api.MapDelete("/cars/{id:int}",async Task<IResult>(int id,ClaimsPrincipal p,RevDb db)=>{var c=await db.Cars.FindAsync(id);if(c==null)return Missing();if(c.UserId!=Uid(p))return Denied();db.Cars.Remove(c);await db.SaveChangesAsync();return Results.NoContent();});
api.MapGet("/posts",async (ClaimsPrincipal p,RevDb db)=>(await db.Posts.Include(x=>x.User).Include(x=>x.Car).Include(x=>x.Likes).OrderByDescending(x=>x.CreatedAt).ToListAsync()).Select(x=>PostDto(x,Uid(p))));
api.MapPost("/posts",async Task<IResult>(PostRequest r,ClaimsPrincipal p,RevDb db)=>{var uid=Uid(p);if(!PhotoOk(r.Photo)||(r.Caption?.Length??0)>1000)return Bad("Selecione uma foto válida e use até 1000 caracteres.");if(r.CarId.HasValue&&!await db.Cars.AnyAsync(x=>x.Id==r.CarId&&x.UserId==uid))return Bad("Associe apenas veículos da sua garagem.");var post=new Post{UserId=uid,CarId=r.CarId,Caption=r.Caption?.Trim()??"",Photo=r.Photo!};db.Posts.Add(post);await db.SaveChangesAsync();return Results.Ok(new{post.Id});});
api.MapDelete("/posts/{id:int}",async Task<IResult>(int id,ClaimsPrincipal p,RevDb db)=>{var post=await db.Posts.FindAsync(id);if(post==null)return Missing();if(post.UserId!=Uid(p))return Denied();db.Posts.Remove(post);await db.SaveChangesAsync();return Results.NoContent();});
api.MapPut("/posts/{id:int}/like",async Task<IResult>(int id,ClaimsPrincipal p,RevDb db)=>{if(!await db.Posts.AnyAsync(x=>x.Id==id))return Missing();await db.Database.ExecuteSqlInterpolatedAsync($"INSERT OR IGNORE INTO PostLikes (PostId, UserId) VALUES ({id}, {Uid(p)})");return Results.NoContent();});
api.MapDelete("/posts/{id:int}/like",async (int id,ClaimsPrincipal p,RevDb db)=>{var uid=Uid(p);await db.PostLikes.Where(x=>x.PostId==id&&x.UserId==uid).ExecuteDeleteAsync();return Results.NoContent();});
static bool ValidEvent(EventRequest r)=>TextOk(r.Title,100)&&TextOk(r.Description,2000)&&TextOk(r.Location,250)&&r.EndAt>r.StartAt&&(r.MaxParticipants==null||r.MaxParticipants is >=1 and <=100000);
static void SetEvent(Meet e,EventRequest r){e.Title=r.Title.Trim();e.Description=r.Description.Trim();e.Location=r.Location.Trim();e.StartAt=r.StartAt.UtcDateTime;e.EndAt=r.EndAt.UtcDateTime;e.MaxParticipants=r.MaxParticipants;}
api.MapGet("/events",async (ClaimsPrincipal p,RevDb db)=>(await db.Events.Include(x=>x.Host).Include(x=>x.Members).OrderBy(x=>x.StartAt).ToListAsync()).Select(x=>EventDto(x,Uid(p))));
api.MapGet("/events/{id:int}",async Task<IResult>(int id,ClaimsPrincipal p,RevDb db)=>{var e=await db.Events.Include(x=>x.Host).Include(x=>x.Members).FirstOrDefaultAsync(x=>x.Id==id);return e==null?Missing():Results.Ok(EventDto(e,Uid(p)));});
api.MapPost("/events",async Task<IResult>(EventRequest r,ClaimsPrincipal p,RevDb db)=>{if(!ValidEvent(r)||r.EndAt<=DateTimeOffset.UtcNow)return Bad("Dados ou datas inválidos.");var uid=Uid(p);var e=new Meet{HostId=uid};SetEvent(e,r);e.Members.Add(new EventMember{UserId=uid});db.Events.Add(e);await db.SaveChangesAsync();return Results.Ok(new{e.Id});});
api.MapPut("/events/{id:int}",async Task<IResult>(int id,EventRequest r,ClaimsPrincipal p,RevDb db)=>{await using var tx=await db.Database.BeginTransactionAsync();var e=await db.Events.Include(x=>x.Members).FirstOrDefaultAsync(x=>x.Id==id);if(e==null)return Missing();if(e.HostId!=Uid(p))return Denied();if(!ValidEvent(r)||(r.MaxParticipants.HasValue&&r.MaxParticipants<e.Members.Count))return Bad("Dados inválidos ou limite inferior às inscrições atuais.");SetEvent(e,r);await db.SaveChangesAsync();await tx.CommitAsync();return Results.NoContent();});
api.MapDelete("/events/{id:int}",async Task<IResult>(int id,ClaimsPrincipal p,RevDb db)=>{var e=await db.Events.FindAsync(id);if(e==null)return Missing();if(e.HostId!=Uid(p))return Denied();db.Events.Remove(e);await db.SaveChangesAsync();return Results.NoContent();});
api.MapPost("/events/{id:int}/join",async Task<IResult>(int id,ClaimsPrincipal p,RevDb db)=>{await using var tx=await db.Database.BeginTransactionAsync();var e=await db.Events.Include(x=>x.Members).FirstOrDefaultAsync(x=>x.Id==id);if(e==null)return Missing();var uid=Uid(p);if(e.Members.Any(x=>x.UserId==uid))return Results.NoContent();if(e.EndAt<=DateTime.UtcNow)return Bad("Evento encerrado.");if(e.MaxParticipants.HasValue&&e.Members.Count>=e.MaxParticipants)return Bad("Evento lotado.");e.Members.Add(new EventMember{UserId=uid});await db.SaveChangesAsync();await tx.CommitAsync();return Results.NoContent();});
api.MapDelete("/events/{id:int}/leave",async Task<IResult>(int id,ClaimsPrincipal p,RevDb db)=>{var e=await db.Events.FindAsync(id);if(e==null)return Missing();var uid=Uid(p);if(e.HostId==uid)return Bad("O anfitrião não pode sair do próprio evento.");await db.EventMembers.Where(x=>x.EventId==id&&x.UserId==uid).ExecuteDeleteAsync();return Results.NoContent();});
api.MapGet("/events/{id:int}/members",async (int id,RevDb db)=>await db.EventMembers.Where(x=>x.EventId==id).Select(x=>new{x.UserId,x.User.DriverTag}).ToListAsync());
api.MapDelete("/events/{id:int}/members/{userId:int}",async Task<IResult>(int id,int userId,ClaimsPrincipal p,RevDb db)=>{var e=await db.Events.FindAsync(id);if(e==null)return Missing();if(e.HostId!=Uid(p))return Denied();if(userId==e.HostId)return Bad("Não é possível remover o anfitrião.");await db.EventMembers.Where(x=>x.EventId==id&&x.UserId==userId).ExecuteDeleteAsync();return Results.NoContent();});
// Public read-only media: images in public feed/garage/profile, without private account data.
static IResult Image(byte[]? bytes)=>bytes==null?Results.NotFound():Results.File(bytes,bytes[0]==0xff?"image/jpeg":"image/png");
api.MapGet("/images/users/{id:int}",async (int id,RevDb db)=>Image(await db.Users.Where(x=>x.Id==id).Select(x=>x.Drivatar).FirstOrDefaultAsync())).AllowAnonymous();
api.MapGet("/images/cars/{id:int}",async (int id,RevDb db)=>Image(await db.Cars.Where(x=>x.Id==id).Select(x=>x.Photo).FirstOrDefaultAsync())).AllowAnonymous();
api.MapGet("/images/posts/{id:int}",async (int id,RevDb db)=>Image(await db.Posts.Where(x=>x.Id==id).Select(x=>x.Photo).FirstOrDefaultAsync())).AllowAnonymous();
app.Run();
