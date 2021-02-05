using System.ComponentModel;
using System.Text.Json.Serialization;

namespace AssetTrackingMobile
{
    /// <summary>
    /// This class represents the message serialized and sent to IoT Hub
    /// </summary>
    class DeviceReadingMessage
    {
        public string timestamp { get; set; }
        public string latitude { get; set; }
        public string longitude { get; set; }
        public BeaconMessage[] beacons { get; set; }
       
    }

    class BeaconMessage
    {
        public string uuid { get; set; }
        public string major { get; set; }
        public string minor { get; set; }
    }
}
