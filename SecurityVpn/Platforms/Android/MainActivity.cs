using Android.App;
using Android.Content;
using Android.Net;
using Android.OS;

namespace VPN.Platforms.Android;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    ConfigurationChanges = global::Android.Content.PM.ConfigChanges.ScreenSize
                         | global::Android.Content.PM.ConfigChanges.Orientation
                         | global::Android.Content.PM.ConfigChanges.UiMode
                         | global::Android.Content.PM.ConfigChanges.ScreenLayout
                         | global::Android.Content.PM.ConfigChanges.SmallestScreenSize
                         | global::Android.Content.PM.ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    const int VpnRequestCode = 100;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Регистрируем колбэк для запроса разрешения
        VpnPermissionHelper.RequestPermission = (intent) =>
        {
            StartActivityForResult(intent, VpnRequestCode);
        };
    }

    protected override async void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        if (requestCode == VpnRequestCode && resultCode == Result.Ok)
        {
            // Пользователь разрешил — запускаем
            var config = VpnPermissionHelper.PendingConfigJson;
            if (!string.IsNullOrEmpty(config))
                await VpnServiceImplementation.LaunchServiceAsync(config);
        }
    }
}