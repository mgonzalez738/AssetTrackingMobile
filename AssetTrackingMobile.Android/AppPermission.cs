using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using Xamarin.Essentials;

namespace AssetTrackingMobile.Droid
{
    public class AppPermission : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions => new List<(string androidPermission, bool isRuntime)>
        {
            (Android.Manifest.Permission.AccessFineLocation, true),
            (Android.Manifest.Permission.AccessBackgroundLocation, true)
        }.ToArray();
    }
}