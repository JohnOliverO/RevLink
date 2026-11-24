using Microsoft.Maui.Storage;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
namespace RevLink.Mobile;
public class ApiClient {
 // Android emulator reaches host via 10.0.2.2; physical phone: replace with PC's LAN IP.
 public const string DefaultBaseUrl="http://10.0.2.2:5080";
 readonly HttpClient http=new(){Timeout=TimeSpan.FromSeconds(25)};
 public string BaseUrl=>Preferences.Default.Get("api_url",DefaultBaseUrl).TrimEnd('/');
 public string? Token {get;private set;} public DriverDto? Me {get;set;}
 public void SetBaseUrl(string url){if(!Uri.TryCreate(url.Trim(),UriKind.Absolute,out var uri)||uri.Scheme is not ("http" or "https"))throw new Exception("Informe uma URL HTTP ou HTTPS válida.");Preferences.Default.Set("api_url",url.Trim().TrimEnd('/'));}
 public string ImageUrl(string path)=>BaseUrl+path;
 public async Task<bool> Restore(){Token=await SecureStorage.Default.GetAsync("revlink_token");if(string.IsNullOrWhiteSpace(Token))return false;try{Me=await Get<DriverDto>("/api/users/me");return true;}catch(ApiException e) when(e.Status==401){await ClearSession();return false;}}
 public async Task SignIn(string email,string password,string? tag=null){var result=await Send<AuthDto>(HttpMethod.Post,tag==null?"/api/auth/login":"/api/auth/register",tag==null?(object)new{email,password}:new{email,password,driverTag=tag},false);Token=result.Token;Me=result.User;await SecureStorage.Default.SetAsync("revlink_token",Token);}
 public Task ClearSession(){Token=null;Me=null;SecureStorage.Default.Remove("revlink_token");return Task.CompletedTask;}
 public Task<T> Get<T>(string path)=>Send<T>(HttpMethod.Get,path);
 public Task Write(HttpMethod method,string path,object? body=null)=>Send<object>(method,path,body);
 async Task<T> Send<T>(HttpMethod method,string path,object? body=null,bool auth=true){using var req=new HttpRequestMessage(method,BaseUrl+path);if(auth&&Token!=null)req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",Token);if(body!=null)req.Content=JsonContent.Create(body);using var res=await http.SendAsync(req);if(!res.IsSuccessStatusCode){var msg=$"Erro HTTP {(int)res.StatusCode}.";try{using var doc=JsonDocument.Parse(await res.Content.ReadAsStringAsync());if(doc.RootElement.TryGetProperty("message",out var m))msg=m.GetString()??msg;}catch{}throw new ApiException((int)res.StatusCode,msg);}if(res.StatusCode==HttpStatusCode.NoContent||typeof(T)==typeof(object))return default!;return (await res.Content.ReadFromJsonAsync<T>())!;}
}
public class ApiException(int status,string message):Exception(message){public int Status=>status;}
public record AuthDto(string Token,DriverDto User);
public record DriverDto(int Id,string Email,string DriverTag,string? DrivatarUrl);
public record CarDto(int Id,int UserId,string OwnerTag,string Nickname,string Brand,string Model,int Year,string Engine,string Style,string PhotoUrl);
public record PostDto(int Id,int UserId,string DriverTag,string? DrivatarUrl,int? CarId,string? CarNickname,string Caption,DateTime CreatedAt,string PhotoUrl,int LikeCount,bool Liked);
public record EventDto(int Id,int HostId,string HostTag,string Title,string Description,string Location,DateTime StartAt,DateTime EndAt,int? MaxParticipants,int ParticipantCount,bool Joined,bool IsHost);
public record MemberDto(int UserId,string DriverTag);
