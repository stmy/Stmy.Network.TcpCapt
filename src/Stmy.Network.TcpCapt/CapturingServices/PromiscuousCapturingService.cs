using System;
using System.Collections.Generic;

namespace Stmy.Network.TcpCapt.CapturingServices
{
    /// <summary>
    /// Provides packet capturing service using promiscuous mode.
    /// </summary>
    /// <seealso cref="Stmy.Network.TcpCapt.ICapturingService" />
    public class PromiscuousCapturingService : ICapturingService
    {
        List<PromiscuousSocketCapturer> capturers;

        /// <summary>
        /// Occurs when packet is captured.
        /// </summary>
        public event EventHandler<PacketCapturedEventArgs> Captured;

        /// <summary>
        /// Initializes a new instance of the <see cref="PromiscuousCapturingService"/> class.
        /// </summary>
        public PromiscuousCapturingService()
        {
            capturers = new List<PromiscuousSocketCapturer>();
        }

        /// <summary>
        /// Starts the packet capturing.
        /// </summary>
        public void StartCapture()
        {
            foreach (var localAddress in Util.GetLocalAddresses())
            {
                // Begin packet sniffing using promiscuous mode
                var capturer = new PromiscuousSocketCapturer(localAddress);

                capturer.Captured += OnCaptured;
                capturers.Add(capturer);
            }
        }

        private void OnCaptured(object sender, PacketCapturedEventArgs e)
        {
            Captured?.Invoke(this, new PacketCapturedEventArgs(e.Packet));
        }

        /// <summary>
        /// Stops the packet capturing.
        /// </summary>
        public void StopCapture()
        {
            foreach (var capturer in capturers)
            {
                capturer.Dispose();
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
