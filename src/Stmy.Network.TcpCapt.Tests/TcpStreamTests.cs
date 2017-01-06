using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PacketDotNet;
using PacketDotNet.Utils;

namespace Stmy.Network.TcpCapt.Tests
{
    [TestClass]
    public class TcpStreamTests
    {
        static TcpPacket[] CreateSequentialPacket(string[] messages, uint initialSequenceNumber = 0)
        {
            List<TcpPacket> result = new List<TcpPacket>();

            uint seq = initialSequenceNumber;
            foreach (var message in messages)
            {
                var tcpPacket = CreatePacket(message);
                tcpPacket.SequenceNumber = seq;
                seq = unchecked(seq + (uint)tcpPacket.PayloadData.Length);
                tcpPacket.UpdateTCPChecksum();

                result.Add(tcpPacket);
            }

            return result.ToArray();
        }

        static TcpPacket CreatePacket(string message)
        {
            var packet = EthernetPacket.RandomPacket();
            var ipPacket = IpPacket.RandomPacket(IpVersion.IPv4);
            ipPacket.ParentPacket = packet;
            packet.PayloadPacket = ipPacket;
            var tcpPacket = TcpPacket.RandomPacket();
            tcpPacket.ParentPacket = ipPacket;
            ipPacket.PayloadPacket = tcpPacket;

            tcpPacket.PayloadData = Encoding.UTF8.GetBytes(message);
            tcpPacket.UpdateTCPChecksum();

            return tcpPacket;
        }

        [TestMethod]
        public void TestRegularSequence()
        {
            var testStrings = new[] { "hello", "capturing", "world" };
            var packets = CreateSequentialPacket(testStrings);

            var stream = new TcpStream();
            stream.Process(packets[0]);
            stream.Process(packets[1]);
            stream.Process(packets[2]);

            var reader = new StreamReader(stream);
            var result = reader.ReadToEnd();

            Assert.AreEqual(string.Join("", testStrings), result);
        }

        [TestMethod]
        public void TestFragmentedSequence()
        {
            var testStrings = new[] { "hello", "capturing", "world" };
            var packets = CreateSequentialPacket(testStrings);

            var stream = new TcpStream();
            stream.Process(packets[0]);
            stream.Process(packets[2]);
            stream.Process(packets[1]);

            var reader = new StreamReader(stream);
            var result = reader.ReadToEnd();

            Assert.AreEqual(string.Join("", testStrings), result);
        }

        [TestMethod]
        public void TestRetransmittedSequence()
        {
            var testStrings = new[] { "hello", "capturing", "world" };
            var packets = CreateSequentialPacket(testStrings);

            var stream = new TcpStream();
            stream.Process(packets[0]);
            stream.Process(packets[0]);
            stream.Process(packets[1]);
            stream.Process(packets[1]);
            stream.Process(packets[2]);
            stream.Process(packets[2]);

            var reader = new StreamReader(stream);
            var result = reader.ReadToEnd();

            Assert.AreEqual(string.Join("", testStrings), result);
        }

        [TestMethod]
        public void TestRetransmittedSequence2()
        {
            var testStrings = new[] { "hello", "capturing", "world" };
            var packets = CreateSequentialPacket(testStrings);

            var shortened = CreatePacket("cap");
            shortened.SequenceNumber = packets[1].SequenceNumber;
            shortened.UpdateTCPChecksum();

            var stream = new TcpStream();
            stream.Process(packets[0]);
            stream.Process(shortened);
            stream.Process(packets[1]);
            stream.Process(packets[2]);

            var reader = new StreamReader(stream);
            var result = reader.ReadToEnd();

            Assert.AreEqual(string.Join("", testStrings), result);
        }

        [TestMethod]
        public void TestTimeout()
        {
            var testStrings = new[] { "hello", "capturing", "world" };
            var packets = CreateSequentialPacket(testStrings);

            var stream = new TcpStream();
            stream.Process(packets[0]);
            stream.Process(packets[2]);
            Thread.Sleep(3000);
            stream.Process(packets[1]); // Should be ignored

            var reader = new StreamReader(stream);
            var result = reader.ReadToEnd();

            Assert.AreEqual(
                testStrings[0] + testStrings[2],
                result);
        }

        [TestMethod]
        public void TestFragmentedSequenceWithEdgeSN()
        {
            var testStrings = new[] { "hello", "capturing", "world" };
            var packets = CreateSequentialPacket(testStrings, uint.MaxValue - 5);

            var stream = new TcpStream();
            stream.Process(packets[0]);
            stream.Process(packets[2]);
            stream.Process(packets[1]);

            var reader = new StreamReader(stream);
            var result = reader.ReadToEnd();

            Assert.AreEqual(string.Join("", testStrings), result);
        }
    }
}
