using System;
using System.Collections.Generic;

namespace Stmy.Network.TcpCapt.CapturingServices
{
    public class PromiscuousCapturingService : ICapturingService
    {
        List<PromiscuousSocketCapturer> capturers;

        public event EventHandler<PacketCapturedEventArgs> Captured;

        public PromiscuousCapturingService()
        {
            capturers = new List<PromiscuousSocketCapturer>();
        }

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

        public void StopCapture()
        {
            foreach (var capturer in capturers)
            {
                capturer.Dispose();
            }
        }

        public void Dispose()
        {
            StopCapture();
        }
    }
}
