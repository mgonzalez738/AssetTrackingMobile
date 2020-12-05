using AssetTrackingMobile.Interfaces;
using AssetTrackingMobile.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace AssetTrackingMobile
{
    // Learn more about making custom code visible in the Xamarin.Forms previewer
    // by visiting https://aka.ms/xamarinforms-previewer
    [DesignTimeVisible(false)]
    public partial class MainPage : ContentPage
    {
        
        
        public bool IsStartedRanging { get; set; }
        private ObservableCollection<SharedBeacon> receivedBeacons = new ObservableCollection<SharedBeacon>();
        public ObservableCollection<SharedBeacon> ReceivedBeacons { get { return receivedBeacons; } }
        private CancellationTokenSource _sendTaskCancellationTokenSource = new CancellationTokenSource();
        private readonly IoTHubService _iotHubService = new IoTHubService();

        private DateTime timestamp = DateTime.Now;
        public DateTime Timestamp
        {
            get { return timestamp; }
            set
            {
                timestamp = value;
                OnPropertyChanged();
            }
        }

        private Single latitude;
        public Single Latitude
        {
            get { return latitude; }
            set
            {
                latitude = value;
                OnPropertyChanged();
            }
        }

        private Single longitude;
        public Single Longitude
        {
            get { return longitude; }
            set
            {
                longitude = value;
                OnPropertyChanged();
            }
        }

        public MainPage()
        {
            InitializeComponent();
            IsStartedRanging = false;

            ReceivedBeaconsView.ItemsSource = receivedBeacons;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            startStopButton.IsEnabled = false;

            bool initialized = await _iotHubService.Initialize();
            if (!initialized)
            {
                await DisplayAlert("Sample not configured", "Unable to initialize IoT Hub Client. Please check IoTHubService.cs file and that you have access to Internet.", "ok");
                return;
            }
            startStopButton.IsEnabled = true;

            /*
            Task.Run(async () =>
            {
                var status = await CrossPermissions.Current.CheckPermissionStatusAsync(Permission.Location);

                if (status != PermissionStatus.Granted)
                    status = await Util.Permissions.CheckPermissions(Permission.Location);
            });*/

            MessagingCenter.Subscribe<App>(this, "CleanBeacons", (sender) =>
            {
                List<SharedBeacon> rb = new List<SharedBeacon>(receivedBeacons);

                updateBeaconCurrentDateTime();
                deleteOldBeacons();

            });

            MessagingCenter.Subscribe<App, List<SharedBeacon>>(this, "BeaconsReceived", (sender, arg) =>
            {
                if (arg != null && arg is List<SharedBeacon>)
                {
                    System.Diagnostics.Debug.WriteLine("Received: " + ((List<SharedBeacon>)arg).Count);
                    List<SharedBeacon> temp = arg;
                    List<SharedBeacon> rb = new List<SharedBeacon>(receivedBeacons);

                    if (arg != null && arg.Count > 0)
                    {

                        DateTime now = DateTime.Now;

                        foreach (SharedBeacon shared in rb)
                            shared.CurrentDateTime = now;

                        foreach (SharedBeacon sharedBeacon in arg)
                        {

                            // Is the beacon already in list?
                            var ret = rb.Where(o => o.BluetoothAddress == sharedBeacon.BluetoothAddress).FirstOrDefault();
                            if (ret != null) // Is present
                            {
                                var index = rb.IndexOf(ret);
                                rb[index].Update(now, sharedBeacon.Distance, sharedBeacon.Rssi); // Update last received date time
                            }
                            else
                            {
                                rb.Insert(0, sharedBeacon);
                            }
                        }

                        rb.OrderByDescending(o => o.Rssi);

                        receivedBeacons.Clear();
                        foreach (SharedBeacon item in rb)
                            receivedBeacons.Add(item);
                    }

                }
            });

        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            MessagingCenter.Unsubscribe<App, List<SharedBeacon>>(this, "BeaconsReceived");
            MessagingCenter.Unsubscribe<App>(this, "CleanBeacons");

        }

        private async Task TimerCallback()
        {
            updateBeaconCurrentDateTime();
            deleteOldBeacons();

            string message = await GetNextMessageAsync();

            try
            {
                IoTClientStatistics statistics = await _iotHubService.SendMessage(message);
                Device.BeginInvokeOnMainThread(() =>
                {
                    this.BindingContext = null;
                    this.BindingContext = statistics;
                });
            }
            catch (InvalidAsynchronousStateException e)
            {
                Device.BeginInvokeOnMainThread(async () => await DisplayAlert("Exception", e?.Message, "ok"));
            }
            
        }

        
        private void updateBeaconCurrentDateTime()
        {
            // Update current received date time
            foreach (SharedBeacon shared in receivedBeacons)
                shared.CurrentDateTime = DateTime.Now;
        }

        private void deleteOldBeacons()
        {
            if (receivedBeacons != null)
            {
                List<SharedBeacon> rb = receivedBeacons.ToList();

                int count = rb.Count;

                // Delete old beacons
                for (int ii = count - 1; ii >= 0; ii--)
                {
                    try
                    {
                        if (rb[ii].ForceDelete | rb[ii].CanDelete)
                            rb.RemoveAt(ii);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex.Message);
                    }
                }

                rb.OrderByDescending(o => o.Rssi);

                receivedBeacons.Clear();
                foreach (SharedBeacon item in rb)
                    receivedBeacons.Add(item);
            }
        }

        void OnStartStopClicked(object sender, EventArgs e)
        {
            IBeaconService beaconService = DependencyService.Get<IBeaconService>();

            if (!IsStartedRanging)
            {
                _sendTaskCancellationTokenSource = new CancellationTokenSource();

                Device.StartTimer(TimeSpan.FromSeconds(10), () =>
                {
                    if (_sendTaskCancellationTokenSource != null &&
                        _sendTaskCancellationTokenSource.IsCancellationRequested)
                        return false;

                    Task.Factory.StartNew(async () => await TimerCallback());
                    return true;
                });

                beaconService.Start();
                this.startStopButton.Text = "Detener";
            }
            else
            {
                _sendTaskCancellationTokenSource.Cancel();
                beaconService.Stop();
                receivedBeacons.Clear();
                this.startStopButton.Text = "Iniciar";
            }

            IsStartedRanging = !IsStartedRanging;

        }

        private async Task<string> GetNextMessageAsync()
        {
            int i;

            List<BeaconMessage> beacons = new List<BeaconMessage>();
            for(i=0; i<receivedBeacons.Count; i++)
            {
                BeaconMessage beacon = new BeaconMessage();
                beacon.uuid = receivedBeacons[i].Id1;
                beacon.major = receivedBeacons[i].Id2;
                beacon.minor = receivedBeacons[i].Id3;
                beacons.Add(beacon);
            }

            var location = await GetCoordinatesAsync();

            Timestamp = DateTime.Now;
            Latitude = (Single)location.Latitude;
            Longitude = (Single)location.Longitude;

            DeviceReadingMessage msg = new DeviceReadingMessage()
            {
                DateTime = Timestamp.ToString(),
                Latitude = location.Latitude.ToString("F6"),
                Longitude = location.Longitude.ToString("F6"),
                Beacons = beacons.ToArray()
            };

            

            return JsonSerializer.Serialize(msg);
        }

        private async Task<Location> GetCoordinatesAsync()
        {
            try
            {
                var location = await Geolocation.GetLastKnownLocationAsync();

                if (location != null)
                {
                    Console.WriteLine($"Latitude: {location.Latitude}, Longitude: {location.Longitude}, Altitude: {location.Altitude}");
                }

                return location;
            }
            catch (FeatureNotSupportedException fnsEx)
            {
                // Handle not supported on device exception
                Console.WriteLine("FeatureNotSupportedException");
                return null;
            }
            catch (FeatureNotEnabledException fneEx)
            {
                // Handle not enabled on device exception
                Console.WriteLine("FeatureNotEnabledException");
                return null;
            }
            catch (PermissionException pEx)
            {
                // Handle permission exception
                Console.WriteLine("PermissionException");
                return null;
            }
            catch (Exception ex)
            {
                // Unable to get location
                Console.WriteLine("OtherException");
                return null;
            }
        }
    }
}
