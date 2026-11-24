namespace RevLink.Mobile.Views;
public class AccountPage:RevPage {
 public AccountPage():base("Conta"){}
 protected override async Task Load(){var me=await Api.Get<DriverDto>("/api/users/me");Api.Me=me;Reset("DRIVER PROFILE","REVLINK DRIVER");var avatar=Ui.Picture(me.DrivatarUrl,140);avatar.WidthRequest=140;avatar.HorizontalOptions=LayoutOptions.Center;Add(avatar,Ui.Text(me.DriverTag,26,Ui.Accent,true),Ui.Text(me.Email,14,Ui.Muted),Ui.Button("Editar perfil",()=>Open(new ProfileForm(me))),Ui.Button("Alterar senha",()=>Open(new PasswordForm()),Ui.Surface,Ui.White),Ui.Button("SAIR",async()=>{if(await Confirm("Encerrar sessão?"))await App.Logout();},Ui.Surface,Ui.Danger));}
}
public class ProfileForm:FormPage {
 public ProfileForm(DriverDto me):base("Editar perfil"){var p=PhotoInput(me.DrivatarUrl);var tag=Ui.Entry("DriverTag",me.DriverTag);Add(p.preview,p.button,tag,Ui.Button("SALVAR PERFIL",async()=>{await Api.Write(HttpMethod.Put,"/api/users/me",new{driverTag=Ui.Value(tag),photo=p.data()});await Finish();}));}
}
public class PasswordForm:FormPage {
 public PasswordForm():base("Alterar senha"){var old=Ui.Entry("Senha atual");old.IsPassword=true;var next=Ui.Entry("Nova senha");next.IsPassword=true;var confirm=Ui.Entry("Confirmar nova senha");confirm.IsPassword=true;Add(old,next,confirm,Ui.Button("ALTERAR SENHA",async()=>{if(next.Text!=confirm.Text)throw new Exception("Senhas diferentes.");await Api.Write(HttpMethod.Put,"/api/users/me/password",new{currentPassword=old.Text??"",newPassword=next.Text??""});await DisplayAlertAsync("RevLink","Senha alterada.","OK");await Finish();}));}
}
public class CreatePage:RevPage {
 public CreatePage():base("Criar"){}
 protected override Task Load(){Reset("CRIAR","SHOW YOUR BUILD.");Add(Ui.Card(Ui.Text("01 / FEED",13,Ui.Accent,true),Ui.Text("Nova publicação",22,Ui.White,true),Ui.Text("Compartilhe seu projeto ou um carro que encontrou.",14,Ui.Muted),Ui.Button("PUBLICAR",()=>Open(new PostForm()))),Ui.Card(Ui.Text("02 / GARAGEM",13,Ui.Accent,true),Ui.Text("Novo veículo",22,Ui.White,true),Ui.Button("CADASTRAR",()=>Open(new CarForm()))),Ui.Card(Ui.Text("03 / EVENTOS",13,Ui.Accent,true),Ui.Text("Novo encontro",22,Ui.White,true),Ui.Button("ORGANIZAR",()=>Open(new EventForm()))));return Task.CompletedTask;}
}
