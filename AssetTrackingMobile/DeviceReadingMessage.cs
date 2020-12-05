using System.ComponentModel;
using System.Text.Json.Serialization;

namespace AssetTrackingMobile
{
    /// <summary>
    /// This class represents the message serialized and sent to IoT Hub
    /// </summary>
    class DeviceReadingMessage
    {
        public string DateTime { get; set; }
        public string Latitude { get; set; }
        public string Longitude { get; set; }
        public BeaconMessage[] Beacons { get; set; }
       
    }

    class BeaconMessage
    {
        public string uuid { get; set; }
        public string major { get; set; }
        public string minor { get; set; }
    }
}
