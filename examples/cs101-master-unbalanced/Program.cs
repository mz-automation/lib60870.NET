 /*
  *  Copyright 2016, 2017 MZ Automation GmbH
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
using System.Threading;

using lib60870;
using lib60870.CS101;
using lib60870.linklayer;

namespace cs101_master_unbalanced
{
	class MainClass
	{
		private static bool asduReceivedHandler(object parameter, int slaveAddress, ASDU asdu)
		{
			Console.WriteLine (asdu.ToString ());

			if (asdu.TypeId == TypeID.M_SP_NA_1) {

				for (int i = 0; i < asdu.NumberOfElements; i++) {

					var val = (SinglePointInformation)asdu.GetElement (i);

					Console.WriteLine ("  IOA: " + val.ObjectAddress + " SP value: " + val.Value);
					Console.WriteLine ("   " + val.Quality.ToString ());
				}
			} 
			else if (asdu.TypeId == TypeID.M_ME_TE_1) {

				for (int i = 0; i < asdu.NumberOfElements; i++) {

					var msv = (MeasuredValueScaledWithCP56Time2a)asdu.GetElement (i);

					Console.WriteLine ("  IOA: " + msv.ObjectAddress + " scaled value: " + msv.ScaledValue);
					Console.WriteLine ("   " + msv.Quality.ToString ());
					Console.WriteLine ("   " + msv.Timestamp.ToString ());
				}

			} else if (asdu.TypeId == TypeID.M_ME_TF_1) {

				for (int i = 0; i < asdu.NumberOfElements; i++) {
					var mfv = (MeasuredValueShortWithCP56Time2a)asdu.GetElement (i);

					Console.WriteLine ("  IOA: " + mfv.ObjectAddress + " float value: " + mfv.Value);
					Console.WriteLine ("   " + mfv.Quality.ToString ());
					Console.WriteLine ("   " + mfv.Timestamp.ToString ());
					Console.WriteLine ("   " + mfv.Timestamp.GetDateTime ().ToString ());
				}
			} else if (asdu.TypeId == TypeID.M_SP_TB_1) {

				for (int i = 0; i < asdu.NumberOfElements; i++) {

					var val = (SinglePointWithCP56Time2a)asdu.GetElement (i);

					Console.WriteLine ("  IOA: " + val.ObjectAddress + " SP value: " + val.Value);
					Console.WriteLine ("   " + val.Quality.ToString ());
					Console.WriteLine ("   " + val.Timestamp.ToString ());
				}
			} else if (asdu.TypeId == TypeID.M_ME_NC_1) {

				for (int i = 0; i < asdu.NumberOfElements; i++) {
					var mfv = (MeasuredValueShort)asdu.GetElement (i);

					Console.WriteLine ("  IOA: " + mfv.ObjectAddress + " float value: " + mfv.Value);
					Console.WriteLine ("   " + mfv.Quality.ToString ());
				}
			} else if (asdu.TypeId == TypeID.M_ME_NB_1) {

				for (int i = 0; i < asdu.NumberOfElements; i++) {

					var msv = (MeasuredValueScaled)asdu.GetElement (i);

					Console.WriteLine ("  IOA: " + msv.ObjectAddress + " scaled value: " + msv.ScaledValue);
					Console.WriteLine ("   " + msv.Quality.ToString ());
				}

			} else {
				Console.WriteLine ("Unknown message type!");
			}

			return true;
		}

		private static void linkLayerStateChanged (object parameter, int address, lib60870.linklayer.LinkLayerState newState)
		{
			Console.WriteLine ("LL state event {0} for slave {1}", newState.ToString (), address);
		}

		public class Receiver : IFileReceiver 
		{
			public void Finished(FileErrorCode result)
			{ 
				Console.WriteLine ("File download finished - code: " + result.ToString ());
			}
				
			public void SegmentReceived(byte sectionName, int offset, int size, byte[] data)
			{
				Console.WriteLine ("File segment - sectionName: {0} offset: {1} size: {2}", sectionName, offset, size);
			}
		}

		public static void Main (string[] args)
		{
			bool running = true;

			// use Ctrl-C to stop the programm
			Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs e) {
				e.Cancel = true;
				running = false;
			};

			string portName = "/dev/ttyUSB3";

			if (args.Length > 0)
				portName = args [0];

			SerialPort port = new SerialPort ();

			port.PortName = portName;
			port.BaudRate = 9600;
			port.Parity = Parity.Even;
			port.Handshake = Handshake.None;
			port.Open ();
			port.DiscardInBuffer ();

			/* set link layer address length */
			LinkLayerParameters llParameters = new LinkLayerParameters ();
			llParameters.AddressLength = 1;

			/* unbalanced mode allows multiple slaves on a single serial line */
			CS101Master master = new CS101Master(port, LinkLayerMode.UNBALANCED, llParameters);
			master.DebugOutput = false;
			master.SetASDUReceivedHandler (asduReceivedHandler, null);

			master.SetLinkLayerStateChangedHandler (linkLayerStateChanged, null);

			master.AddSlave (1);
			//master.AddSlave (2);
			//master.AddSlave (3);

			long lastTimestamp = SystemUtils.currentTimeMillis ();

			master.SlaveAddress = 1;
			master.GetFile (1, 30000, NameOfFile.TRANSPARENT_FILE, new Receiver ());


			while (running) {


				master.Run ();

				//master.PollSlaves (); // will only return after polling is complete
				master.PollSingleSlave(1);
				//master.PollSingleSlave(2);

				if ((SystemUtils.currentTimeMillis() - lastTimestamp) >= 5000) {

					lastTimestamp = SystemUtils.currentTimeMillis ();

					//	if (master.GetStateOfSlave (1) == LinkLayerState.AVAILABLE) {
					//master.UseSlaveAddress (1);
					//master.SendInterrogationCommand (CauseOfTransmission.ACTIVATION, 1, 20);
					//	}

					//					if (master.GetStateOfSlave (2) == LinkLayerState.AVAILABLE) {
					//						master.SetSlaveAddress (2);
					//						master.SendInterrogationCommand (CauseOfTransmission.ACTIVATION, 1, 20);
					//					}
					//
					//					if (master.GetStateOfSlave (3) == LinkLayerState.AVAILABLE) {
					//						master.SetSlaveAddress (3);
					//						master.SendInterrogationCommand (CauseOfTransmission.ACTIVATION, 1, 20);
					//					}
				}

				Thread.Sleep (10);
			}

			port.Close ();
		}
	}
}
