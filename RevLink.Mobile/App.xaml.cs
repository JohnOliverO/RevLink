namespace RevLink.Mobile;
public partial class App:Application {
 public static ApiClient Api {get;}=new();
 public App(){InitializeComponent();UserAppTheme=AppTheme.Dark;}
 protected override Window CreateWindow(IActivationState? activationState){var p=new Views.LoginPage();return new Window(p);}
 public static void OpenMain()=>Current!.Windows[0].Page=new MainShell();
 public static async Task Logout(){await Api.ClearSession();Current!.Windows[0].Page=new Views.LoginPage();}
}
