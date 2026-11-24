using Microsoft.Maui.Media;
using Microsoft.Maui.Controls.Shapes;
using Android.Graphics;
using Color = Microsoft.Maui.Graphics.Color;
using Image = Microsoft.Maui.Controls.Image;
namespace RevLink.Mobile.Views;
public static class Ui {
 public static Color Accent=>Get("Accent");public static Color Surface=>Get("Surface");public static Color Muted=>Get("Muted");public static Color Danger=>Get("Danger");public static Color White=>Get("White");public static Color Get(string key)=>(Color)Application.Current!.Resources[key];
 public static Label Text(string text,int size=15,Color? color=null,bool bold=false)=>new(){Text=text,FontSize=size,TextColor=color??White,FontAttributes=bold?FontAttributes.Bold:FontAttributes.None,LineBreakMode=LineBreakMode.WordWrap};
 public static VerticalStackLayout Stack(params View[] views){var s=new VerticalStackLayout{Spacing=10};foreach(var v in views)s.Children.Add(v);return s;}
 public static Border Card(params View[] views)=>new(){BackgroundColor=Surface,Stroke=Get("Outline"),StrokeThickness=1,StrokeShape=new RoundRectangle{CornerRadius=14},Padding=12,Content=Stack(views)};
 public static Image Picture(string? path,double height=220)=>new(){Source=string.IsNullOrEmpty(path)?null:ImageSource.FromUri(new Uri(App.Api.ImageUrl(path))),HeightRequest=height,Aspect=Aspect.AspectFill,BackgroundColor=Get("Outline")};
 public static Button Button(string text,Func<Task> action,Color? bg=null,Color? fg=null){var b=new Button{Text=text,BackgroundColor=bg??Accent,TextColor=fg??Get("Black")};b.Clicked+=async (_,_)=>{b.IsEnabled=false;try{await action();}catch(Exception ex){var p=Shell.Current?.CurrentPage??Application.Current!.Windows[0].Page!;await p.DisplayAlertAsync("RevLink",ex.Message,"OK");}finally{b.IsEnabled=true;}};return b;}
 public static Entry Entry(string placeholder,string? value=null,Keyboard? keyboard=null)=>new(){Placeholder=placeholder,Text=value,Keyboard=keyboard??Keyboard.Default};
 public static Editor Editor(string placeholder,string? value=null)=>new(){Placeholder=placeholder,Text=value,HeightRequest=100,AutoSize=EditorAutoSizeOption.TextChanges};
 public static string Value(Entry e)=>e.Text?.Trim()??"";
 public static string Value(Editor e)=>e.Text?.Trim()??"";
 public static async Task<byte[]?> PickPhoto(){var file=await MediaPicker.Default.PickPhotoAsync();if(file==null)return null;
  using var opts=new BitmapFactory.Options{InJustDecodeBounds=true};using(var input=await file.OpenReadAsync())BitmapFactory.DecodeStream(input,null,opts);if(opts.OutWidth<=0||opts.OutHeight<=0)throw new Exception("Imagem inválida.");
  int sample=1;while(Math.Max(opts.OutWidth,opts.OutHeight)/sample>1600)sample*=2;
  using var decode=new BitmapFactory.Options{InSampleSize=sample};using var stream=await file.OpenReadAsync();using var bitmap=BitmapFactory.DecodeStream(stream,null,decode)??throw new Exception("Não foi possível abrir a imagem.");
  using var output=new MemoryStream();bitmap.Compress(Bitmap.CompressFormat.Jpeg!,80,output);if(output.Length>2_000_000)throw new Exception("Imagem muito grande.");return output.ToArray();
 }
 public static ImageSource Preview(byte[] data)=>ImageSource.FromStream(()=>new MemoryStream(data));
}
public abstract class RevPage:ContentPage {
 protected readonly VerticalStackLayout Body=new(){Padding=new Thickness(16,12,16,24),Spacing=12};protected ApiClient Api=>App.Api;
 bool loading;
 protected RevPage(string title){Title=title;Content=new ScrollView{Content=Body};}
 protected override async void OnAppearing(){base.OnAppearing();await Reload();}
 protected virtual Task Load()=>Task.CompletedTask;
 protected async Task Reload(){if(loading)return;loading=true;try{await Load();}catch(ApiException ex) when(ex.Status==401){await DisplayAlertAsync("Sessão","Entre novamente.","OK");await App.Logout();}catch(Exception ex){Body.Children.Clear();Body.Children.Add(Ui.Text(ex.Message,15,Ui.Danger));Body.Children.Add(Ui.Button("Tentar novamente",Reload));}finally{loading=false;}}
 protected void Reset(string heading,string? subtitle=null){Body.Children.Clear();Body.Children.Add(Ui.Text(heading,29,Ui.White,true));if(subtitle!=null)Body.Children.Add(Ui.Text(subtitle,13,Ui.Muted));}
 protected void Add(params View[] views){foreach(var v in views)Body.Children.Add(v);}
 protected Task<bool> Confirm(string text)=>DisplayAlertAsync("Confirmar",text,"Sim","Cancelar");
 protected async Task Open(Page page)=>await Navigation.PushAsync(page);
}
public class FormPage:ContentPage {
 protected readonly VerticalStackLayout Body=new(){Spacing=12,Padding=16};protected ApiClient Api=>App.Api;
 public FormPage(string title){Title=title;Content=new ScrollView{Content=Body};Body.Children.Add(Ui.Text(title,26,Ui.White,true));}
 protected void Add(params View[] views){foreach(var v in views)Body.Children.Add(v);}
 protected async Task Finish(){await Navigation.PopAsync();}
 protected (Image preview,Button button,Func<byte[]?> data) PhotoInput(string? old=null){byte[]? data=null;var pic=Ui.Picture(old,190);var b=Ui.Button("Selecionar foto",async()=>{var picked=await Ui.PickPhoto();if(picked==null)return;data=picked;pic.Source=Ui.Preview(data);},Ui.Surface,Ui.Accent);return(pic,b,()=>data);}
}
