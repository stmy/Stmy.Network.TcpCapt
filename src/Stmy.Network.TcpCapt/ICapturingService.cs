using System;

namespace Stmy.Network.TcpCapt
{
    /// <summary>
    /// Represents packet capturing service.
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    public interface ICapturingService : IDisposable
    {
        /// <summary>
        /// Starts the packet capturing.
        /// </summary>
        void StartCapture();

        /// <summary>
        /// Stops the packet capturing.
        /// </summary>
        void StopCapture();

        /// <summary>
        /// Occurs when packet is captured.
        /// </summary>
        event EventHandler<PacketCapturedEventArgs> Captured;
    }
}
