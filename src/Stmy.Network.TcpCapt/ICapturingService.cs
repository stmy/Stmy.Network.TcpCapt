using System;

namespace Stmy.Network.TcpCapt
{
    public interface ICapturingService : IDisposable
    {
        void StartCapture();
        void StopCapture();
        event EventHandler<PacketCapturedEventArgs> Captured;
    }
}
