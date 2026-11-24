namespace RevLink.Mobile.Views;
public partial class LoginPage:ContentPage {
 bool register,checkedSession;
 public LoginPage(){InitializeComponent();}
 protected override async void OnAppearing(){base.OnAppearing();if(checkedSession)return;checkedSession=true;Submit.IsEnabled=false;try{if(await App.Api.Restore())App.OpenMain();}catch(Exception ex){Status.Text="Servidor indisponível: "+ex.Message;}finally{Submit.IsEnabled=true;}}
 void ToggleClicked(object? sender,EventArgs e){register=!register;TagBox.IsVisible=ConfirmBox.IsVisible=register;Heading.Text=register?"Crie seu DriverTag":"Entre na comunidade";Submit.Text=register?"CADASTRAR":"ENTRAR";Toggle.Text=register?"Já tenho uma conta":"Criar conta";Status.Text="";}
 async void SubmitClicked(object? sender,EventArgs e){Submit.IsEnabled=false;Status.Text="";try{if(register&&PasswordBox.Text!=ConfirmBox.Text)throw new Exception("As senhas não coincidem.");await App.Api.SignIn(EmailBox.Text??"",PasswordBox.Text??"",register?TagBox.Text??"":null);App.OpenMain();}catch(Exception ex){Status.Text=ex.Message;}finally{Submit.IsEnabled=true;}}
 async void ConfigureClicked(object? sender,EventArgs e){var url=await DisplayPromptAsync("Servidor RevLink","No emulador: 10.0.2.2. No celular: IP do computador.",initialValue:App.Api.BaseUrl);if(url==null)return;try{App.Api.SetBaseUrl(url);await App.Api.ClearSession();Status.Text="Endereço atualizado.";}catch(Exception ex){Status.Text=ex.Message;}}
}
