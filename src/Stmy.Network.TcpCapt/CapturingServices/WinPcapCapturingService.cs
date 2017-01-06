using System;
using System.Collections.Generic;
using PacketDotNet;
using SharpPcap.LibPcap;
using SharpPcap.WinPcap;

namespace Stmy.Network.TcpCapt.CapturingServices
{
    public class WinPcapCapturingService : ICapturingService
    {
        List<WinPcapDevice> devices;
        readonly string filter;

        public event EventHandler<PacketCapturedEventArgs> Captured;

        public WinPcapCapturingService() : this("")
        {
        }

        public WinPcapCapturingService(string filter)
        {
            string error;
            if (!PcapDevice.CheckFilter(filter, out error))
            {
                throw new ArgumentException($"Invalid filter string: {error}");
            }

            this.filter = filter;
        }

        private void OnPacketArrival(object sender, SharpPcap.CaptureEventArgs e)
        {
            Captured?.Invoke(this, new PacketCapturedEventArgs(Packet.ParsePacket(e.Packet.LinkLayerType, e.Packet.Data)));
        }

        public void StartCapture()
        {
            devices = new List<WinPcapDevice>(WinPcapDeviceList.New());
            foreach (var device in devices)
            {
                device.Filter = filter;
                device.OnPacketArrival += OnPacketArrival;
                device.StartCapture();
            }
        }

        public void StopCapture()
        {
            foreach (var device in devices)
            {
                try
                {
                    device.StopCapture();
                }
                catch { }
                device.OnPacketArrival -= OnPacketArrival;
            }
        }

        public void Dispose()
        {
            StopCapture();
        }
    }
}
