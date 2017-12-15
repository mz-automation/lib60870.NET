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
using System.Collections.Generic;

using lib60870.linklayer;

namespace lib60870.CS101
{
	public class CS101MasterBalanced : Master
	{
		private LinkLayer linkLayer = null;

		private SerialPort port;
		private bool running = false;

		private LinkLayerParameters linkLayerParameters;
		private ApplicationLayerParameters parameters = new ApplicationLayerParameters();

		private byte[] buffer = new byte[300]; /* buffer to read data from serial line */

		private SerialTransceiverFT12 transceiver;

		private PrimaryLinkLayerBalanced primaryLinkLayer;

		private ASDUReceivedHandler asduReceivedHandler = null;
		private object asduReceivedHandlerParameter = null;

		private Queue<BufferFrame> userDataQueue = new Queue<BufferFrame>();

		private FileClient fileClient = null;

		private void DebugLog(string msg)
		{
			if (debugOutput) {
				Console.Write ("CS101 MASTER: ");
				Console.WriteLine (msg);
			}
		}

		public int OwnAddress {
			get {
				return linkLayer.OwnAddress;
			}
			set {
				linkLayer.OwnAddress = value;
			}
		}

		public LinkLayerState GetLinkLayerState()
		{
			return primaryLinkLayer.GetLinkLayerState ();
		}

		public CS101MasterBalanced (SerialPort port)
			:this(port, new LinkLayerParameters())
		{
		}

		public CS101MasterBalanced (SerialPort port, LinkLayerParameters llParameters)
		{
			this.port = port;
			this.linkLayerParameters = llParameters;
			this.transceiver = new SerialTransceiverFT12 (port, linkLayerParameters, DebugLog);

			linkLayer = new LinkLayer (buffer, linkLayerParameters, transceiver, DebugLog);

			primaryLinkLayer = new PrimaryLinkLayerBalanced (linkLayer, GetUserData, DebugLog);

			linkLayer.SetPrimaryLinkLayer (primaryLinkLayer);
			linkLayer.SetSecondaryLinkLayer (new SecondaryLinkLayerBalanced (linkLayer, 0, HandleApplicationLayer, DebugLog));

			this.fileClient = null;
		}
			
		public void SetASDUReceivedHandler(ASDUReceivedHandler handler, object parameter)
		{
			asduReceivedHandler = handler;
			asduReceivedHandlerParameter = parameter;
		}

		public void SetLinkLayerStateChangedHandler(LinkLayerStateChanged handler, object parameter)
		{
			primaryLinkLayer.SetLinkLayerStateChanged (handler, parameter);
		}

		public void SetReceivedRawMessageHandler(RawMessageHandler handler, object parameter)
		{
			linkLayer.SetReceivedRawMessageHandler (handler, parameter);
		}

		public void SetSentRawMessageHandler(RawMessageHandler handler, object parameter)
		{
			linkLayer.SetSentRawMessageHandler (handler, parameter);
		}

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

			if (fileClient != null)
				messageHandled = fileClient.HandleFileAsdu(asdu);

			if (messageHandled == false) {
				if (asduReceivedHandler != null)
					messageHandled = asduReceivedHandler (asduReceivedHandlerParameter, asdu);
			}

			return messageHandled;
		}

		public void SendLinkLayerTestFunction() {
			linkLayer.SendTestFunction ();
		}

		public void Run() 
		{
			linkLayer.Run ();

			if (fileClient != null)
				fileClient.HandleFileService ();
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


		public override void SendInterrogationCommand(CauseOfTransmission cot, int ca, byte qoi) 
		{
			ASDU asdu = new ASDU (parameters, cot, false, false, (byte) parameters.OA, ca, false);

			asdu.AddInformationObject (new InterrogationCommand (0, qoi));

			EnqueueUserData (asdu);
		}

		public override void SendCounterInterrogationCommand(CauseOfTransmission cot, int ca, byte qcc)
		{
			ASDU asdu = new ASDU (parameters, cot, false, false, (byte) parameters.OA, ca, false);

			asdu.AddInformationObject (new CounterInterrogationCommand(0, qcc));

			EnqueueUserData (asdu);
		}

		public override void SendReadCommand(int ca, int ioa)
		{
			ASDU asdu = new ASDU (parameters, CauseOfTransmission.REQUEST, false, false, (byte) parameters.OA, ca, false);

			asdu.AddInformationObject(new ReadCommand(ioa));

			EnqueueUserData (asdu);
		}

		public override void SendClockSyncCommand(int ca, CP56Time2a time)
		{
			ASDU asdu = new ASDU (parameters, CauseOfTransmission.ACTIVATION, false, false, (byte) parameters.OA, ca, false);

			asdu.AddInformationObject (new ClockSynchronizationCommand (0, time));

			EnqueueUserData (asdu);
		}

		public override void SendTestCommand(int ca)
		{
			ASDU asdu = new ASDU (parameters, CauseOfTransmission.ACTIVATION, false, false, (byte) parameters.OA, ca, false);

			asdu.AddInformationObject (new TestCommand ());

			EnqueueUserData (asdu);
		}

		public override void SendTestCommandWithCP56Time2a(int ca, ushort tsc, CP56Time2a time)
		{
			ASDU asdu = new ASDU(parameters, CauseOfTransmission.ACTIVATION, false, false, (byte)parameters.OA, ca, false);

			asdu.AddInformationObject(new TestCommandWithCP56Time2a(tsc, time));

			EnqueueUserData(asdu);
		}

		public override void SendResetProcessCommand(CauseOfTransmission cot, int ca, byte qrp)
		{
			ASDU asdu = new ASDU (parameters, CauseOfTransmission.ACTIVATION, false, false, (byte) parameters.OA, ca, false);

			asdu.AddInformationObject (new ResetProcessCommand(0, qrp));

			EnqueueUserData (asdu);
		}

		public override void SendDelayAcquisitionCommand(CauseOfTransmission cot, int ca, CP16Time2a delay)
		{
			ASDU asdu = new ASDU (parameters, CauseOfTransmission.ACTIVATION, false, false, (byte) parameters.OA, ca, false);

			asdu.AddInformationObject (new DelayAcquisitionCommand (0, delay));

			EnqueueUserData (asdu);
		}

		public override void SendControlCommand(CauseOfTransmission cot, int ca, InformationObject sc)
		{
			ASDU controlCommand = new ASDU (parameters, cot, false, false, (byte) parameters.OA, ca, false);

			controlCommand.AddInformationObject (sc);

			EnqueueUserData (controlCommand);
		}

		public override void SendASDU(ASDU asdu)
		{
			EnqueueUserData (asdu);
		}

        public override ApplicationLayerParameters GetApplicationLayerParameters()
        {
            return parameters;
        }

		public override void GetFile(int ca, int ioa, NameOfFile nof, IFileReceiver receiver)
		{
			if (fileClient == null)
				fileClient = new FileClient (this, DebugLog);

			fileClient.RequestFile (ca, ioa, nof, receiver);
		}
    }
}

