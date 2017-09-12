/*
 *  SecondaryLinkLayer.cs
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

namespace lib60870.linklayer
{

	internal interface ISecondaryApplicationLayer
	{
		bool IsClass1DataAvailable();
		BufferFrame GetClass1Data();
		BufferFrame GetCLass2Data();
		bool HandleReceivedData (byte[] msg, bool isBroadcast, int userDataStart, int userDataLength);
		void ResetCUReceived(bool onlyFCB);
	}

	internal abstract class SecondaryLinkLayer {
		public abstract void HandleMessage(FunctionCodePrimary fcp, bool isBroadcast, bool fcb, bool fcv, byte[] msg, int userDataStart, int userDataLength);
		public abstract void RunStateMachine();
	}

	internal class SecondaryLinkLayerUnbalanced : SecondaryLinkLayer
	{
		private bool expectedFcb = true; // expected value of next frame count bit (FCB)
		private Action<string> DebugLog;
		private LinkLayer linkLayer;
		private ISecondaryApplicationLayer applicationLayer;
	//	private Func<byte[], int, int, bool> HandleApplicationLayer;


		public SecondaryLinkLayerUnbalanced(LinkLayer linkLayer, ISecondaryApplicationLayer applicationLayer, Action<string> debugLog)
		{
			this.linkLayer = linkLayer;
			this.DebugLog = debugLog;
			this.applicationLayer = applicationLayer;
		}

		private bool CheckFCB(bool fcb) 
		{
			if (fcb != expectedFcb) {
				Console.WriteLine ("ERROR: Frame count bit (FCB) invalid!");
				//TODO change link status
				return false;
			} else {
				expectedFcb = !expectedFcb;
				return true;
			}
		}

		public override void HandleMessage(FunctionCodePrimary fcp, bool isBroadcast, bool fcb, bool fcv, byte[] msg, int userDataStart, int userDataLength)
		{
			// check frame count bit if fcv == true
			if (fcv) {
				if (CheckFCB (fcb) == false)
					return;
			}

				
			switch (fcp) {

			case FunctionCodePrimary.REQUEST_LINK_STATUS:
				DebugLog ("SLL - REQUEST LINK STATUS");
				{
					bool accessDemand = applicationLayer.IsClass1DataAvailable ();

					linkLayer.SendFixedFrameSecondary (FunctionCodeSecondary.STATUS_OF_LINK_OR_ACCESS_DEMAND, linkLayer.LinkLayerAddress, accessDemand, false);
				}
				break;

			case FunctionCodePrimary.RESET_REMOTE_LINK:
				DebugLog("SLL - RESET REMOTE LINK");
				{
					expectedFcb = true;
					linkLayer.SendFixedFrameSecondary (FunctionCodeSecondary.ACK, linkLayer.LinkLayerAddress, false, false);
					//TODO can answer with single char
					applicationLayer.ResetCUReceived(false);
				}

				break;

			case FunctionCodePrimary.RESET_FCB:
				DebugLog ("SLL - RESET FCB");
				{
					expectedFcb = true;
					linkLayer.SendFixedFrameSecondary (FunctionCodeSecondary.ACK, linkLayer.LinkLayerAddress, false, false);
					//TODO can answer with single char
					applicationLayer.ResetCUReceived (true);
				}
				break;

			case FunctionCodePrimary.REQUEST_USER_DATA_CLASS_2:
				DebugLog("SLL - REQUEST USER DATA CLASS 2");
				{
					BufferFrame asdu = applicationLayer.GetCLass2Data ();

					bool accessDemand = applicationLayer.IsClass1DataAvailable ();

					if (asdu != null)
						linkLayer.SendVariableLengthFrameSecondary (FunctionCodeSecondary.RESP_USER_DATA, accessDemand, false, asdu);	
					else 
						linkLayer.SendFixedFrameSecondary (FunctionCodeSecondary.RESP_NACK_NO_DATA, linkLayer.LinkLayerAddress, false, false);

				}
				break;

			case FunctionCodePrimary.REQUEST_USER_DATA_CLASS_1:
				DebugLog ("SLL - REQUEST USER DATA CLASS 1");
				{
					BufferFrame asdu = applicationLayer.GetClass1Data ();

					bool accessDemand = applicationLayer.IsClass1DataAvailable ();

					if (asdu != null)
						linkLayer.SendVariableLengthFrameSecondary (FunctionCodeSecondary.RESP_USER_DATA, accessDemand, false, asdu);
					else
						linkLayer.SendFixedFrameSecondary (FunctionCodeSecondary.RESP_NACK_NO_DATA, linkLayer.LinkLayerAddress, false, false);
				}
				break;

			case FunctionCodePrimary.USER_DATA_CONFIRMED:
				DebugLog("SLL - USER DATA CONFIRMED");
				if (userDataLength > 0) {
					if (applicationLayer.HandleReceivedData(msg, isBroadcast, userDataStart, userDataLength)) {
						
						bool accessDemand = applicationLayer.IsClass1DataAvailable();

						linkLayer.SendFixedFrameSecondary (FunctionCodeSecondary.ACK, linkLayer.LinkLayerAddress, accessDemand, false);
					}
				}
				break;

			case FunctionCodePrimary.USER_DATA_NO_REPLY:
				DebugLog ("SLL - USER DATA NO REPLY");
				if (userDataLength > 0) {
					applicationLayer.HandleReceivedData (msg, isBroadcast, userDataStart, userDataLength);
				}
				break;

			default:
				DebugLog ("SLL - UNEXPECTED LINK LAYER MESSAGE");
				linkLayer.SendFixedFrameSecondary (FunctionCodeSecondary.LINK_SERVICE_NOT_IMPLEMENTED, linkLayer.LinkLayerAddress, false, false);
				break;
			}
		}

		public override void RunStateMachine()
		{

		}
	}

	internal class SecondaryLinkLayerBalanced : SecondaryLinkLayer
	{
		private bool expectedFcb = true; // expected value of next frame count bit (FCB)
		private Action<string> DebugLog;
		private LinkLayer linkLayer;
		private Func<byte[], int, int, bool> HandleApplicationLayer;

		public SecondaryLinkLayerBalanced(LinkLayer linkLayer,
			Func<byte[], int, int, bool> handleApplicationLayer, Action<string> debugLog)
		{
			this.linkLayer = linkLayer;
			this.DebugLog = debugLog;
			this.HandleApplicationLayer = handleApplicationLayer;
		}


		private void SendStatusOfLink() {
			linkLayer.SendFixedFrameSecondary (FunctionCodeSecondary.STATUS_OF_LINK_OR_ACCESS_DEMAND, linkLayer.LinkLayerAddress, false, false);
		}

		private bool CheckFCB(bool fcb) 
		{
			if (fcb != expectedFcb) {
				Console.WriteLine ("ERROR: Frame count bit (FCB) invalid!");
				//TODO change link status
				return false;
			} else {
				expectedFcb = !expectedFcb;
				return true;
			}
		}

		// HandleSecondaryMessageBalanced
		public override void HandleMessage (FunctionCodePrimary fcp, bool isBroadcast, bool fcb, bool fcv, byte[] msg, int userDataStart, int userDataLength) {

			if (fcv) {
				if (CheckFCB (fcb) == false)
					return;
			}

			switch (fcp) {

			case FunctionCodePrimary.RESET_REMOTE_LINK:
				expectedFcb = true;
				DebugLog ("SLL - RECV RESET REMOTE LINK");
				linkLayer.SendFixedFrameSecondary (FunctionCodeSecondary.ACK, linkLayer.LinkLayerAddress, false, false);
				//TODO can answer with single char
				break;

				//case FunctionCodePrimary.RESET_USER_PROCESS:
				//	break;

			case FunctionCodePrimary.TEST_FUNCTION_FOR_LINK:
				DebugLog ("SLL -TEST FUNCTION FOR LINK");
				// TODO check if DCF has to be sent
				//SendSingleCharACK ();
				linkLayer.SendFixedFrameSecondary (FunctionCodeSecondary.ACK, linkLayer.LinkLayerAddress, false, false);
				break;

			case FunctionCodePrimary.USER_DATA_CONFIRMED:
				DebugLog("SLL - USER DATA CONFIRMED");
				if (userDataLength > 0) {

					if (HandleApplicationLayer (msg, userDataStart, userDataLength))
						linkLayer.SendFixedFrameSecondary (FunctionCodeSecondary.ACK, linkLayer.LinkLayerAddress, false, false);
				}
				break;

			case FunctionCodePrimary.USER_DATA_NO_REPLY:
				DebugLog ("SLL - USER DATA NO REPLY");
				if (userDataLength > 0) {
					HandleApplicationLayer (msg, userDataStart, userDataLength);
				}
				break;

			case FunctionCodePrimary.REQUEST_LINK_STATUS:
				DebugLog ("SLL - RECV REQUEST LINK STATUS");
				SendStatusOfLink ();
				break;

			default:
				DebugLog ("SLL - UNEXPECTED LINK LAYER MESSAGE");
				linkLayer.SendFixedFrameSecondary (FunctionCodeSecondary.LINK_SERVICE_NOT_IMPLEMENTED, linkLayer.LinkLayerAddress, false, false);
				break;

			}
		}

		public override void RunStateMachine()
		{

		}

	}

}

