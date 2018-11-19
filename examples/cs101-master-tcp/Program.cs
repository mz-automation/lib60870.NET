/*
  *  Copyright 2018 MZ Automation GmbH
  *
  *  This file is part of lib60870.NET
  *
  *  lib60870.NET is free software: you can redistribute it and/or modify
  *  it under the terms of the GNU General Public License as published by
  *  the Free Software Foundation, either version 3 of the License, or
  *  (at your option) any later version.
  *
  *  lib60870.NET is distributed in the hope that it will be useful,
  *  but WITHOUT ANY WARRANTY; without even the implied warranty of
  *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  *  GNU General Public License for more details.
  *
  *  You should have received a copy of the GNU General Public License
  *  along with lib60870.NET.  If not, see <http://www.gnu.org/licenses/>.
  *
  *  See COPYING file for the complete license text.
  */

using System;
using System.IO.Ports;

using lib60870;
using lib60870.CS101;
using lib60870.linklayer;
using System.Threading;

namespace cs101_master_tcp
{
	class MainClass
	{

		private static bool rcvdRawMessageHandler (object parameter, byte[] message, int messageSize)
		{
			Console.WriteLine ("RECV " + BitConverter.ToString (message, 0, messageSize));

			return true;
		}

		private static void linkLayerStateChanged (object parameter, int address, lib60870.linklayer.LinkLayerState newState)
		{
			Console.WriteLine ("LL state event: " + newState.ToString ());
		}

		private static bool asduReceivedHandler(object parameter, int address, ASDU asdu)
		{
			Console.WriteLine (asdu.ToString ());

			return true;
		}


		public static void Main (string[] args)
		{
			bool running = true;

			// use Ctrl-C to stop the programm
			Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs e) {
				e.Cancel = true;
				running = false;
			};

			string hostname = "127.0.0.1";
			int tcpPort = 2404;

			if (args.Length > 0)
				hostname = args [0];

			if (args.Length > 1)
				int.TryParse (args [1], out tcpPort);

			// Setup virtual serial port
			TcpClientVirtualSerialPort port = new TcpClientVirtualSerialPort(hostname, tcpPort);
			port.DebugOutput = false;
			port.Start ();

			// Setup balanced CS101 master
			LinkLayerParameters llParameters = new LinkLayerParameters();
			llParameters.AddressLength = 1;
			llParameters.UseSingleCharACK = true;

			CS101Master master = new CS101Master (port, LinkLayerMode.BALANCED, llParameters);
			master.DebugOutput = false;
			master.OwnAddress = 1;
            master.SlaveAddress = 3;
			master.SetASDUReceivedHandler (asduReceivedHandler, null);
			master.SetLinkLayerStateChangedHandler (linkLayerStateChanged, null);
			master.SetReceivedRawMessageHandler (rcvdRawMessageHandler, null);

			long lastTimestamp = SystemUtils.currentTimeMillis ();

			// This will start a separate thread!
			// alternativley you can you master.Run() inside the loop
			master.Start ();

			while (running) {

				if ((SystemUtils.currentTimeMillis() - lastTimestamp) >= 5000) {

					lastTimestamp = SystemUtils.currentTimeMillis ();

					if (master.GetLinkLayerState () == lib60870.linklayer.LinkLayerState.AVAILABLE) {
						master.SendInterrogationCommand (CauseOfTransmission.ACTIVATION, 1, 20);
					} else {
						Console.WriteLine ("Link layer: " + master.GetLinkLayerState ().ToString ());
					}
				}

				Thread.Sleep (100);
			}

			master.Stop ();

			port.Stop ();
		}
	}
}
