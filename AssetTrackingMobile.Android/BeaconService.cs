using AltBeaconOrg.BoundBeacon;

using AssetTrackingMobile.Interfaces;
using AssetTrackingMobile.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;

[assembly: Dependency(typeof(AssetTrackingMobile.Droid.BeaconService))]
namespace AssetTrackingMobile.Droid
{
    class BeaconService : IBeaconService
    {
        private readonly RangeNotifier rangeNotifier;
        private BeaconManager beaconManager;

        AltBeaconOrg.BoundBeacon.Region tagRegion;

        List<SharedBeacon> sharedBeacons = new List<SharedBeacon>();
        object _lock = new object();

        public BeaconService()
        {
            rangeNotifier = new RangeNotifier();
        }

        public BeaconManager BeaconManagerImpl
        {
            get
            {
                if (beaconManager == null)
                    beaconManager = InitializeBeaconManager();
                return beaconManager;
            }
        }

        public void InitializeService()
        {
            if (beaconManager == null)
                beaconManager = InitializeBeaconManager();
        }

        private BeaconManager InitializeBeaconManager()
        {
            // Enable the BeaconManager 
            BeaconManager bm = BeaconManager.GetInstanceForApplication(Xamarin.Essentials.Platform.CurrentActivity);

            var iBeaconParser = new BeaconParser();
            // EDDYSTONE TLM  x,s: 0 - 1 = feaa,m: 2 - 2 = 20,d: 3 - 3,d: 4 - 5,d: 6 - 7,d: 8 - 11,d: 12 - 15
            // EDDYSTONE UID  s: 0 - 1 = feaa,m: 2 - 2 = 00,p: 3 - 3:-41,i: 4 - 13,i: 14 - 19
            // EDDYSTONE URL  s: 0 - 1 = feaa,m: 2 - 2 = 10,p: 3 - 3:-41,i: 4 - 20v
            // IBEACON        m: 2 - 3 = 0215,i: 4 - 19,i: 20 - 21,i: 22 - 23,p: 24 - 24
            iBeaconParser.SetBeaconLayout("m:2-3=0215,i:4-19,i:20-21,i:22-23,p:24-24");
            bm.BeaconParsers.Add(iBeaconParser);

            rangeNotifier.DidRangeBeaconsInRegionComplete += RangingBeaconsInRegion;

            //tagRegion = new AltBeaconOrg.BoundBeacon.Region("Beacon 1", Identifier.Parse("8E6DBFBB-489D-418A-9560-1BA1CE6301AA"), null, null);
            tagRegion = new AltBeaconOrg.BoundBeacon.Region("EmptyBeaconId", null, null, null);

            bm.BackgroundMode = false;
            bm.Bind((IBeaconConsumer)Xamarin.Essentials.Platform.CurrentActivity);

            return bm;
        }

        void RangingBeaconsInRegion(object sender, RangeEventArgs e)
        {
            sharedBeacons = new List<SharedBeacon>();

            lock (_lock)
            {
                // Obtiene los beacons recibidos y los agrega a la lista
                foreach (Beacon beacon in e.Beacons)
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("iBeacon UUID {0} - Major: {1} - Minor: {2} - RSSI: {3}dB", beacon.Id1.ToString(), beacon.Id2.ToString(), beacon.Id3.ToString(), beacon.Rssi));
                    sharedBeacons.Add(new SharedBeacon(beacon.BluetoothName, beacon.BluetoothAddress, beacon.Id1.ToString(), beacon.Id2.ToString(), beacon.Id3.ToString(), beacon.Distance, beacon.Rssi));
                };

                // Envia los beacons recibidos al codigo compartido
                Task.Run( () => {                  
                    if (sharedBeacons.Count > 0) {
                        MessagingCenter.Send<App, List<SharedBeacon>>((App)Application.Current, "BeaconsReceived", sharedBeacons);
                    }
                });

            }

        }

        public async void Start()
        {
            // Requiere los permisos SDK 29+
            // AccessFineLocation
            // AccessBackgroundLocation
            var status = await Permissions.RequestAsync<AppPermission>();

            BeaconManagerImpl.ForegroundBetweenScanPeriod = 0;
            BeaconManagerImpl.BackgroundScanPeriod = 1100;
            BeaconManagerImpl.BackgroundBetweenScanPeriod = 0;
            BeaconManagerImpl.ForegroundScanPeriod = 1100;

            BeaconManagerImpl.AddRangeNotifier(rangeNotifier);
            try
            {
                BeaconManagerImpl.StartRangingBeaconsInRegion(tagRegion);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("StartRangingException: " + ex.Message);
            }

        }

        public void Stop()
        {
            if (beaconManager != null)
            {
                try
                {
                    BeaconManagerImpl.StopRangingBeaconsInRegion(tagRegion);
                    BeaconManagerImpl.RemoveRangeNotifier(rangeNotifier);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("StopRangingException: " + ex.Message);
                }
            }
        }
    }
}

