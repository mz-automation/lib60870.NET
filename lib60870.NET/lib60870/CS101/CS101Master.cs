/*
 *  CS101Master.cs
 *
 *  Copyright 2017 MZ Automation GmbH
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
using System.Collections.Generic;

using lib60870.linklayer;

namespace lib60870
{
	namespace CS101 {

		public class CS101MasterUnbalanced : UnbalancedMaster, IPrimaryLinkLayerCallbacks
		{
			private LinkLayer linkLayer = null;

			private PrimaryLinkLayerUnbalanced linkLayerUnbalanced = null;

			private SerialTransceiverFT12 transceiver;

			private byte[] buffer = new byte[300]; /* buffer to read data from serial line */

			private LinkLayerParameters linkLayerParameters;
			private ApplicationLayerParameters parameters = new ApplicationLayerParameters();

			private void DebugLog(string msg)
			{
				if (debugOutput) {
					Console.Write ("CS101 MASTER: ");
					Console.WriteLine (msg);
				}
			}

			public CS101MasterUnbalanced (SerialPort port)
			{
				this.linkLayerParameters = new LinkLayerParameters ();
				this.transceiver = new SerialTransceiverFT12 (port, linkLayerParameters, DebugLog);

				linkLayer = new LinkLayer (buffer, linkLayerParameters, transceiver, DebugLog);
				linkLayer.LinkLayerMode = LinkLayerMode.UNBALANCED;

				linkLayerUnbalanced = new PrimaryLinkLayerUnbalanced (linkLayer, this, DebugLog);
				linkLayer.SetPrimaryLinkLayer(linkLayerUnbalanced);
			}

			public void AddSlave(int slaveAddress)
			{
				linkLayerUnbalanced.AddSlaveConnection (slaveAddress);
			}


			public LinkLayerState GetLinkLayerState(int slaveAddress)
			{
				return linkLayerUnbalanced.GetStateOfSlave(slaveAddress);
			}


			public override void SendInterrogationCommand(CauseOfTransmission cot, int ca, byte qoi)
			{
				ASDU asdu = new ASDU (parameters, cot, false, false, (byte) parameters.OriginatorAddress, ca, false);

				asdu.AddInformationObject (new InterrogationCommand (0, qoi));

				//TODO problem -> buffer frame needs own buffer so that the message can be stored.
				BufferFrame bf = new BufferFrame (buffer, 0);

				asdu.Encode (bf, parameters);

				linkLayerUnbalanced.SendConfirmed (slaveAddress, bf);
			}

			void IPrimaryLinkLayerCallbacks.AccessDemand(int slaveAddress)
			{
				DebugLog ("Access demand slave " + slaveAddress);
				linkLayerUnbalanced.RequestClass1Data(slaveAddress);
			}
				
			void IPrimaryLinkLayerCallbacks.UserData(int slaveAddress, byte[] message, int start, int length)
			{
				DebugLog ("User data slave " + slaveAddress);

				ASDU asdu;

				try {
					asdu = new ASDU (parameters, message, start, start + length);
				}
				catch(ASDUParsingException e) {
					DebugLog ("ASDU parsing failed: " + e.Message);
					return;
				}
					
				if (asduReceivedHandler != null)
					asduReceivedHandler (asduReceivedHandlerParameter, slaveAddress, asdu);
				
			}
				
			void IPrimaryLinkLayerCallbacks.Timeout(int slaveAddress)
			{
				DebugLog ("Timeout accessing slave " + slaveAddress);
			}

			public void PollSingleSlave(int address) {
				try {
					linkLayerUnbalanced.RequestClass2Data(address);
				}
				catch (LinkLayerBusyException) {
					DebugLog ("Link layer busy");
				}
			}

			public void Run()
			{
				linkLayer.Run ();
			}
		}


		public class CS101MasterBalanced : Master
		{
			private LinkLayer linkLayer = null;

			private SerialPort port;
			private bool running = false;

			private LinkLayerParameters linkLayerParameters;
			private ApplicationLayerParameters parameters = new ApplicationLayerParameters();

			private byte[] buffer = new byte[300]; /* buffer to read data from serial line */

			private long timeoutForACK = 1000;       // for balanced mode only - timeout for ACKs in ms
			private long timeoutRepeat = 5000;       // for balanced mode only - timeout for repeating messages when no ACK received in ms

			private SerialTransceiverFT12 transceiver;

			private void DebugLog(string msg)
			{
				if (debugOutput) {
					Console.Write ("CS101 MASTER: ");
					Console.WriteLine (msg);
				}
			}

			public int Address {
				get {
					return linkLayer.LinkLayerAddress;
				}
				set {
					linkLayer.LinkLayerAddress = value;
				}
			}

			public LinkLayerState GetLinkLayerState()
			{
				return primaryLinkLayer.GetLinkLayerState ();
			}

			public long TimeoutForACK {
				get {
					return this.timeoutForACK;
				}
				set {
					timeoutForACK = value;
				}
			}

			public long TimeoutRepeat {
				get {
					return this.timeoutRepeat;
				}
				set {
					timeoutRepeat = value;
				}
			}

			private PrimaryLinkLayerBalanced primaryLinkLayer;

			public CS101MasterBalanced (SerialPort port)
			{
				this.port = port;
				this.linkLayerParameters = new LinkLayerParameters ();
				this.transceiver = new SerialTransceiverFT12 (port, linkLayerParameters, DebugLog);

				linkLayer = new LinkLayer (buffer, linkLayerParameters, transceiver, DebugLog);

				primaryLinkLayer = new PrimaryLinkLayerBalanced (linkLayer, GetUserData, DebugLog);

				linkLayer.SetPrimaryLinkLayer (primaryLinkLayer);
				linkLayer.SetSecondaryLinkLayer (new SecondaryLinkLayerBalanced (linkLayer, HandleApplicationLayer, DebugLog));
			}
				

			private Queue<BufferFrame> userDataQueue = new Queue<BufferFrame>();

			private void EnqueueUserData(ASDU asdu)
			{
				lock (userDataQueue) {

					BufferFrame frame = new BufferFrame (new byte[256], 0);

					asdu.Encode (frame, parameters);

					userDataQueue.Enqueue (frame);
				}
			}

			private BufferFrame DequeueUserData() 
			{
				lock (userDataQueue) {

					if (userDataQueue.Count > 0)
						return userDataQueue.Dequeue ();
					else
						return null;
				}
			}

			public override void SendInterrogationCommand(CauseOfTransmission cot, int ca, byte qoi) 
			{
				ASDU asdu = new ASDU (parameters, cot, false, false, (byte) parameters.OriginatorAddress, ca, false);

				asdu.AddInformationObject (new InterrogationCommand (0, qoi));

				EnqueueUserData (asdu);
			}

			private bool IsUserDataAvailable()
			{
				lock (userDataQueue) {
					if (userDataQueue.Count > 0)
						return true;
					else
						return false;
				}
			}

			private BufferFrame GetUserData()
			{
				BufferFrame asdu = null;

				if (IsUserDataAvailable())
					return DequeueUserData ();

				return asdu;
			}

			private bool HandleApplicationLayer(byte[] msg, int userDataStart, int userDataLength) 
			{

				ASDU asdu;

				try {
					asdu = new ASDU (parameters, buffer, userDataStart, userDataStart + userDataLength);
				}
				catch(ASDUParsingException e) {
					DebugLog ("ASDU parsing failed: " + e.Message);
					return false;
				}
					
				bool messageHandled = false;

				if (asduReceivedHandler != null)
					messageHandled = asduReceivedHandler (asduReceivedHandlerParameter, asdu);

				return messageHandled;
			}

			public void SendLinkLayerTestFunction() {
				linkLayer.SendTestFunction ();
			}

			public void Run() 
			{
				linkLayer.Run ();
			}

			public void Stop()
			{
				running = false;
			}

			public void ReceiveMessageLoop()
			{
				running = true;

				if (port.IsOpen == false)
					port.Open ();

				port.DiscardInBuffer ();

				while (running) {
					Run ();
				}

				port.Close ();
			}
		}
	}
}

