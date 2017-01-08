using System;
using PacketDotNet;

namespace Stmy.Network.TcpCapt
{
    /// <summary>
    /// Represents arguments for the <see cref="ICapturingService.Captured"/> event.
    /// </summary>
    /// <seealso cref="System.EventArgs" />
    public class PacketCapturedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the packet captured.
        /// </summary>
        public Packet Packet { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PacketCapturedEventArgs"/> class.
        /// </summary>
        /// <param name="packet">The packet captured.</param>
        public PacketCapturedEventArgs(Packet packet)
        {
            Packet = packet;
        }
    }
}
