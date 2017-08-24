﻿using GSEmulator.Commands;
using GSEmulator.Model;
using GSEmulator.GSProtocol3;
using System.Threading;
using System;
using System.Net.Sockets;
using System.Net;
using System.Linq;
using System.Collections.Generic;
using NLog;

namespace GSEmulator
{
    class Program
    {
        private static Logger LOGGER = LogManager.GetCurrentClassLogger();

        static Server server;
        static ReaderWriterLockSlim mutex;
        static UdpClient endPoint;

        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                LOGGER.Fatal("Program started with wrong arguments: Wrong amount of args");
                Environment.Exit(-1);
                return;
            }

            IPAddress ip;
            if (!IPAddress.TryParse(args[0], out ip))
            {
                LOGGER.Fatal("Program started with wrong arguments: Ip invalid");
                Environment.Exit(-1);
                return;
            }

            ushort lPort;
            if (!ushort.TryParse(args[1], out lPort))
            {
                LOGGER.Fatal("Program started with wrong arguments: Port invalid");
                Environment.Exit(-1);
                return;
            }

            server = createDefaultPRServer();
            mutex = new ReaderWriterLockSlim();
            endPoint = new UdpClient(new IPEndPoint(ip, lPort));

            for (int i = 0; i < 5; i++)
            {
                Thread thread = new Thread(new ThreadStart(ReporterWorker));
                thread.Start();
            }

            LOGGER.Info("GSEmulator ready at {0}:{1}!", ip.ToString(), lPort);
            for (string line = Console.ReadLine(); ; line = Console.ReadLine())
            {
                LOGGER.Trace("Parsing: '{0}'", line);
                Command command = Parser.Parse(line);
                try
                {
                    mutex.EnterWriteLock();
                    command.Execute(ref server);
                }
                catch (Exception ex)
                {
                    LOGGER.Warn( ex.ToString());
                }
                finally { mutex.ExitWriteLock(); }
            }
        }

        static void ReporterWorker()
        {
            IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
            byte[] datagram;
            while ((datagram = endPoint.Receive(ref RemoteIpEndPoint)) != null)
            {
                LOGGER.Debug("Received request from {0}:{1}", RemoteIpEndPoint.Address, RemoteIpEndPoint.Port);
                LOGGER.Trace("Received request: {0}", BitConverter.ToString(datagram));

                // Its GSDiverter's job to make sure only good requests are diverted to us
                if (datagram.Length < 7) {
                    LOGGER.Warn("Received request with invalid size");
                    continue;
                }


                // A normal request: FE FD 00 XX XX XX XX AA BB CC 00
                // Where the XX is the timestamp we want
                byte[] timestamp = datagram.Skip(3).Take(4).ToArray();

                try
                {
                    mutex.EnterReadLock();
                    Encoder encoder = new Encoder(server, timestamp);
                    List<Message> messages = encoder.Encode(true, true, true);
                    mutex.ExitReadLock();

                    foreach (Message msg in messages)
                    {
                        var reply = msg.GetBytes();
                        LOGGER.Debug("Sending reply to {0}:{1}", RemoteIpEndPoint.Address, RemoteIpEndPoint.Port);
                        LOGGER.Trace("Sending reply: {0}", BitConverter.ToString(reply));
                        endPoint.Send(reply, reply.Length, RemoteIpEndPoint);
                    }
                }
                catch (Exception ex)
                {
                    LOGGER.Warn(ex.ToString());
                    mutex.ExitReadLock();
                }
            }
        }


        static Server createDefaultPRServer()
        {
            Server server = new Server();
            server.HostName = "A PR Server";
            server.GameName = "battlefield2";
            server.GameVersion = "1.5.3153-802.0";      // Well game won't get any more updates, so this will be the same forever and ever
            server.GameMode = "openplaying";            // BF2 always replies with "openplaying"
            server.OS = "win32";
            server.TKMode = "Punish";
            server.Fps = 36;
            return server;
        }

    }
}
