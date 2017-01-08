using System;
using System.Collections.Generic;
using PacketDotNet;
using SharpPcap.LibPcap;
using SharpPcap.WinPcap;

namespace Stmy.Network.TcpCapt.CapturingServices
{
    /// <summary>
    /// Provides packet capturing service using WinPcap library.
    /// </summary>
    /// <seealso cref="Stmy.Network.TcpCapt.ICapturingService" />
    public class WinPcapCapturingService : ICapturingService
    {
        List<WinPcapDevice> devices;
        readonly string filter;

        /// <summary>
        /// Occurs when packet is captured.
        /// </summary>
        public event EventHandler<PacketCapturedEventArgs> Captured;

        /// <summary>
        /// Initializes a new instance of the <see cref="WinPcapCapturingService"/> class without filter.
        /// </summary>
        public WinPcapCapturingService() : this("")
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WinPcapCapturingService"/> class with filter.
        /// </summary>
        /// <param name="filter">
        ///     The filter expression for WinPcap. 
        ///     See <see cref="!:https://www.winpcap.org/docs/docs_40_2/html/group__language.html">winPcap user's manual</see> for details.
        /// </param>
        /// <exception cref="System.ArgumentException"></exception>
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

        /// <summary>
        /// Starts the packet capturing.
        /// </summary>
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

        /// <summary>
        /// Stops the packet capturing.
        /// </summary>
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

        /// <summary>
        /// Stops the packet capturing.
        /// </summary>
        public void Dispose()
        {
            StopCapture();
        }
    }
}
