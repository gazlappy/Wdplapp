using Android.App;
using Android.Runtime;

namespace Wdpl2;

[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownerShip) : base(handle, ownerShip) { }
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
