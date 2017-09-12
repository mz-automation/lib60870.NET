/*
 *  PrimaryLinkLayer.cs
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
using System.Collections.Generic;

namespace lib60870.linklayer
{



	public class LinkLayerBusyException : lib60870.ConnectionException
	{
		public LinkLayerBusyException(string message)
			:base(message)
		{
		}

		public LinkLayerBusyException(string message, Exception e)
			:base(message, e)
		{
		}
	}

	internal interface IPrimaryLinkLayerCallbacks {

		/// <summary>
		/// Indicate an access demand request form the client (ACD bit set in response)
		/// </summary>
		/// <param name="slaveAddress">address of the slave that requested the access demand</param>
		void AccessDemand(int slaveAddress);

		/// <summary>
		/// User data (application layer data) received from a slave
		/// </summary>
		/// <param name="slaveAddress">address of the slave that sent the data</param>
		/// <param name="message">buffer containing the received message</param>
		/// <param name="start">start of user data in the buffer</param>
		/// <param name="length">length of user data in the buffer</param>
		void UserData(int slaveAddress, byte[] message, int start, int length);

		/// <summary>
		/// A former request to the slave (UD Class 1, UD Class 2, confirmed...) resulted in a timeout
		/// Station does not respond indication
		/// </summary>
		/// <param name="slaveAddress">address of the slave that caused the timeout</param>
		void Timeout(int slaveAddress);
	}

	internal interface IPrimaryLinkLayerUnbalanced {
		
		void ResetCU(int slaveAddress);

		/// <summary>
		/// Determines whether this channel (slave connecrtion) is ready to transmit a new application layer message
		/// </summary>
		/// <returns><c>true</c> if this instance is channel available; otherwise, <c>false</c>.</returns>
		/// <param name="slaveAddress">link layer address of the slave</param>
		bool IsChannelAvailable(int slaveAddress);


		void RequestClass1Data(int slaveAddress);



		void RequestClass2Data(int slaveAddress);


		void SendConfirmed(int slaveAddress, BufferFrame message);
		void SendNoReply(int slaveAddress, BufferFrame message);
	}

	
	internal abstract class PrimaryLinkLayer
	{
		public abstract void HandleMessage(FunctionCodeSecondary fcs, bool dir, bool dfc, 
			int address, byte[] msg, int userDataStart, int userDataLength);
		public abstract void RunStateMachine();
		public abstract void SendLinkLayerTestFunction();
	}
		

	internal class PrimaryLinkLayerUnbalanced : PrimaryLinkLayer, IPrimaryLinkLayerUnbalanced
	{
		private LinkLayer linkLayer;
		private Action<string> DebugLog;

	//	private bool waitingForResponse = false;

		private List<SlaveConnection> slaveConnections;

		/// <summary>
		/// The current active slave connection.
		/// </summary>
		private SlaveConnection currentSlave = null;

		private BufferFrame nextBroadcastMessage = null;

		private IPrimaryLinkLayerCallbacks callbacks = null;

		// can this class implement Master interface?
		private class SlaveConnection {

			private Action<string> DebugLog = null;

			public int address;
			public PrimaryLinkLayerState primaryState = PrimaryLinkLayerState.IDLE;
			public long lastSendTime = 0;             
			public long originalSendTime = 0;
			public bool nextFcb = true;
			public bool waitingForResponse = false;
			public LinkLayerState linkLayerState = LinkLayerState.IDLE;

			PrimaryLinkLayerUnbalanced linkLayerUnbalanced;

			private bool sendLinkLayerTestFunction = false;

			// don't send new application layer messages to avoid data flow congestion
			private bool dontSendMessages = false;

			public BufferFrame nextMessage = null;
			private BufferFrame lastSentASDU = null;

			public bool requireConfirmation = false;

			public bool resetCu = false;
			public bool requestClass2Data = false;
			public bool requestClass1Data = false;

			private LinkLayer linkLayer;


			public SlaveConnection(int address, LinkLayer linkLayer, Action<string> debugLog, PrimaryLinkLayerUnbalanced linkLayerUnbalanced) 
			{
				this.address = address;
				this.linkLayer = linkLayer;
				this.DebugLog = debugLog;
				this.linkLayerUnbalanced = linkLayerUnbalanced;
			}

			public bool IsMessageWaitingToSend()
			{
				if (requestClass1Data || requestClass2Data || (nextMessage != null))
					return true;
				else
					return false;
			}

			internal void HandleMessage(FunctionCodeSecondary fcs, bool acd, bool dfc, 
				int address, byte[] msg, int userDataStart, int userDataLength)
			{
				//Console.WriteLine ("Received msg FC=" + (int)fcs + " " + fcs.ToString () + " ACD=" + acd.ToString() + " DFC=" + dfc.ToString());

				PrimaryLinkLayerState newState = primaryState;

				if (acd)
					Console.WriteLine ("ACD set");

				if (dfc) {

					//stop sending ASDUs; only send Status of link requests
					dontSendMessages = true;

					switch (primaryState) {
					case PrimaryLinkLayerState.EXECUTE_REQUEST_STATUS_OF_LINK:
					case PrimaryLinkLayerState.EXECUTE_RESET_REMOTE_LINK:
						newState = PrimaryLinkLayerState.EXECUTE_REQUEST_STATUS_OF_LINK;
						break;
					case PrimaryLinkLayerState.EXECUTE_SERVICE_SEND_CONFIRM:
						//TODO message must be handled and switched to BUSY state later!
					case PrimaryLinkLayerState.SECONDARY_LINK_LAYER_BUSY:
						newState = PrimaryLinkLayerState.SECONDARY_LINK_LAYER_BUSY;
						break;
					}

					primaryState = newState;
					return;

				} else {
					// unblock transmission of application layer messages
					dontSendMessages = false;
				}

				switch (fcs) {

				case FunctionCodeSecondary.ACK:
					//TODO what to do if we are not waiting for a response?
					DebugLog ("PLL - received ACK");
					if (primaryState == PrimaryLinkLayerState.EXECUTE_RESET_REMOTE_LINK)
						newState = PrimaryLinkLayerState.LINK_LAYERS_AVAILABLE;
					else if (primaryState == PrimaryLinkLayerState.EXECUTE_SERVICE_SEND_CONFIRM) {

//						if (sendLinkLayerTestFunction) {
//							nextFcb = !nextFcb;
//							sendLinkLayerTestFunction = false;
//						}

						newState = PrimaryLinkLayerState.LINK_LAYERS_AVAILABLE;
					} else if (primaryState == PrimaryLinkLayerState.EXECUTE_SERVICE_REQUEST_RESPOND) {
						newState = PrimaryLinkLayerState.LINK_LAYERS_AVAILABLE;
					}

					waitingForResponse = false;
					break;

				case FunctionCodeSecondary.NACK:
					DebugLog ("PLL - received NACK");
					//TODO what to do? repeat message?

					break;

				case FunctionCodeSecondary.STATUS_OF_LINK_OR_ACCESS_DEMAND:	
					DebugLog ("PLL - received STATUS OF LINK");
					if (primaryState == PrimaryLinkLayerState.EXECUTE_REQUEST_STATUS_OF_LINK) {
						DebugLog ("PLL - SEND RESET REMOTE LINK");
						linkLayer.SendFixedFramePrimary (FunctionCodePrimary.RESET_REMOTE_LINK, address, false, false);
						lastSendTime = SystemUtils.currentTimeMillis ();
						waitingForResponse = true;
						newState = PrimaryLinkLayerState.EXECUTE_RESET_REMOTE_LINK;
					} else /* illegal message */
						newState = PrimaryLinkLayerState.IDLE;

					break;

				case FunctionCodeSecondary.RESP_USER_DATA:
					DebugLog ("PLL - received USER DATA");

					if (primaryState == PrimaryLinkLayerState.EXECUTE_SERVICE_REQUEST_RESPOND) {
						linkLayerUnbalanced.callbacks.UserData (address, msg, userDataStart, userDataLength);

					

						newState = PrimaryLinkLayerState.LINK_LAYERS_AVAILABLE;
					}
					else /* illegal message */
						newState = PrimaryLinkLayerState.IDLE;

					break;

				case FunctionCodeSecondary.RESP_NACK_NO_DATA:
					DebugLog ("PLL - received RESP NO DATA");

					if (primaryState == PrimaryLinkLayerState.EXECUTE_SERVICE_REQUEST_RESPOND)
						newState = PrimaryLinkLayerState.LINK_LAYERS_AVAILABLE;
					else /* illegal message */
						newState = PrimaryLinkLayerState.IDLE;

					break;

				case FunctionCodeSecondary.LINK_SERVICE_NOT_FUNCTIONING:
				case FunctionCodeSecondary.LINK_SERVICE_NOT_IMPLEMENTED:
					DebugLog ("PLL - link layer service not functioning/not implemented in secondary station ");
					if (primaryState == PrimaryLinkLayerState.EXECUTE_SERVICE_SEND_CONFIRM)
						newState = PrimaryLinkLayerState.LINK_LAYERS_AVAILABLE;
					break;

				default:
					DebugLog ("UNEXPECTED SECONDARY LINK LAYER MESSAGE");
					break;
				}

				if (acd) {
					if (linkLayerUnbalanced.callbacks != null)
						linkLayerUnbalanced.callbacks.AccessDemand (address);
				}

				DebugLog ("PLL RECV - old state: " + primaryState.ToString () + " new state: " + newState.ToString ());

				primaryState = newState;
			}

			public void RunStateMachine()
			{
				PrimaryLinkLayerState newState = primaryState;

				switch (primaryState) {

				case PrimaryLinkLayerState.IDLE:

					waitingForResponse = false;
					originalSendTime = 0;
					lastSendTime = 0;
					sendLinkLayerTestFunction = false;
					newState = PrimaryLinkLayerState.EXECUTE_REQUEST_STATUS_OF_LINK;

					break;

				case PrimaryLinkLayerState.EXECUTE_REQUEST_STATUS_OF_LINK:

					if (waitingForResponse) {
						if (SystemUtils.currentTimeMillis () > (lastSendTime + linkLayer.TimeoutForACK)) {
							linkLayer.SendFixedFramePrimary (FunctionCodePrimary.REQUEST_LINK_STATUS, address, false, false);
							lastSendTime = SystemUtils.currentTimeMillis ();
						}
					}
					else {
						DebugLog ("PLL - SEND RESET REMOTE LINK");
						linkLayer.SendFixedFramePrimary (FunctionCodePrimary.RESET_REMOTE_LINK, address, false, false);
						lastSendTime = SystemUtils.currentTimeMillis ();
						waitingForResponse = true;
						newState = PrimaryLinkLayerState.EXECUTE_RESET_REMOTE_LINK; 
					}

					break;

				case PrimaryLinkLayerState.EXECUTE_RESET_REMOTE_LINK:

					if (waitingForResponse) {
						if (SystemUtils.currentTimeMillis () > (lastSendTime + linkLayer.TimeoutForACK)) {
							waitingForResponse = false;
							newState = PrimaryLinkLayerState.IDLE;
						}
					} else {
						newState = PrimaryLinkLayerState.LINK_LAYERS_AVAILABLE;
					}

					break;

				case PrimaryLinkLayerState.LINK_LAYERS_AVAILABLE:

					if (sendLinkLayerTestFunction) {
						DebugLog ("PLL - SEND TEST LINK");
						linkLayer.SendFixedFramePrimary (FunctionCodePrimary.TEST_FUNCTION_FOR_LINK, address, nextFcb, true);
						nextFcb = !nextFcb;
						lastSendTime = SystemUtils.currentTimeMillis ();
						originalSendTime = lastSendTime;
						newState = PrimaryLinkLayerState.EXECUTE_SERVICE_SEND_CONFIRM;
					} else if (requestClass1Data || requestClass2Data) {

						if (requestClass1Data) {
							DebugLog ("PLL - SEND REQ UD 1");
							linkLayer.SendFixedFramePrimary (FunctionCodePrimary.REQUEST_USER_DATA_CLASS_1, address, nextFcb, true);
							requestClass1Data = false; //TODO move this to messageReceiver or timeout handler
						} else {
							DebugLog ("PLL - SEND REQ UD 2");
							linkLayer.SendFixedFramePrimary (FunctionCodePrimary.REQUEST_USER_DATA_CLASS_2, address, nextFcb, true);
							requestClass2Data = false;
						}

						nextFcb = !nextFcb;
						lastSendTime = SystemUtils.currentTimeMillis ();
						originalSendTime = lastSendTime;
						newState = PrimaryLinkLayerState.EXECUTE_SERVICE_REQUEST_RESPOND;
					}
					else {
						BufferFrame asdu = nextMessage;

						if (asdu != null) {

							DebugLog ("PLL - SEND APPLICATION LAYER MESSAGE");

							linkLayer.SendVariableLengthFramePrimary (FunctionCodePrimary.USER_DATA_CONFIRMED, address, nextFcb, true, asdu);

							lastSentASDU = nextMessage;
							nextMessage = null;


							nextFcb = !nextFcb;

							lastSendTime = SystemUtils.currentTimeMillis ();
							originalSendTime = lastSendTime;
							waitingForResponse = true;

							newState = PrimaryLinkLayerState.EXECUTE_SERVICE_SEND_CONFIRM;
						}
					}

					break;

				case PrimaryLinkLayerState.EXECUTE_SERVICE_SEND_CONFIRM:

					if (SystemUtils.currentTimeMillis () > (lastSendTime + linkLayer.TimeoutForACK)) {

						if (SystemUtils.currentTimeMillis () > (originalSendTime + linkLayer.TimeoutRepeat)) {
							DebugLog ("TIMEOUT: ASDU not confirmed after repeated transmission");
							newState = PrimaryLinkLayerState.IDLE;
						} else {
							DebugLog ("TIMEOUT: ASDU not confirmed");

							if (sendLinkLayerTestFunction) {
								DebugLog ("PLL - REPEAT SEND RESET REMOTE LINK");
								linkLayer.SendFixedFramePrimary (FunctionCodePrimary.TEST_FUNCTION_FOR_LINK, address, !nextFcb, true);
								lastSendTime = SystemUtils.currentTimeMillis ();
							} else {
								linkLayer.LinkLayerAddress = address;
								linkLayer.SendVariableLengthFramePrimary (FunctionCodePrimary.USER_DATA_CONFIRMED, address, !nextFcb, true, lastSentASDU);
							}
							lastSendTime = SystemUtils.currentTimeMillis ();
						}
					}

					break;

				case PrimaryLinkLayerState.EXECUTE_SERVICE_REQUEST_RESPOND:

					if (SystemUtils.currentTimeMillis () > (lastSendTime + linkLayer.TimeoutForACK)) {

						if (SystemUtils.currentTimeMillis () > (originalSendTime + linkLayer.TimeoutRepeat)) {
							DebugLog ("TIMEOUT: ASDU not confirmed after repeated transmission");
							newState = PrimaryLinkLayerState.IDLE;
							requestClass1Data = false;
							requestClass2Data = false;

						} else {
							DebugLog ("TIMEOUT: ASDU not confirmed");

							if (requestClass1Data) {
								linkLayer.SendFixedFramePrimary (FunctionCodePrimary.REQUEST_USER_DATA_CLASS_1, address, !nextFcb, true);
							} else if (requestClass2Data) {
								linkLayer.SendFixedFramePrimary (FunctionCodePrimary.REQUEST_USER_DATA_CLASS_2, address, !nextFcb, true);
							}


							lastSendTime = SystemUtils.currentTimeMillis ();
						}
					}

					break;

				case PrimaryLinkLayerState.SECONDARY_LINK_LAYER_BUSY:
					//TODO - reject new requests from application layer?
					break;

				}

				if (primaryState != newState)
					DebugLog ("PLL - old state: " + primaryState.ToString () + " new state: " + newState.ToString ());

				primaryState = newState;

			}
		}

		/********************************
		 * IPrimaryLinkLayerUnbalanced
		 ********************************/


		public void ResetCU(int slaveAddress)
		{				
			SlaveConnection slave = GetSlaveConnection (slaveAddress);

			if (slave != null)
				slave.resetCu = true;
		}

		public bool IsChannelAvailable(int slaveAddress)
		{
			SlaveConnection slave = GetSlaveConnection (slaveAddress);

			if (slave != null) {
				if (slave.IsMessageWaitingToSend () == false)
					return true;
			}

			return false;
		}


		public void RequestClass1Data(int slaveAddress)
		{
			SlaveConnection slave = GetSlaveConnection (slaveAddress);

			if (slave != null) {
				slave.requestClass1Data = true;;
			}
		}



		public void RequestClass2Data(int slaveAddress)
		{
			SlaveConnection slave = GetSlaveConnection (slaveAddress);

			if (slave != null) {
				if (slave.IsMessageWaitingToSend ())
					throw new LinkLayerBusyException ("Message pending");
				else
					slave.requestClass2Data = true;
			}
		}


		public void SendConfirmed(int slaveAddress, BufferFrame message)
		{
			SlaveConnection slave = GetSlaveConnection (slaveAddress);

			if (slave != null) {
				if (slave.nextMessage != null)
					throw new LinkLayerBusyException ("Message pending");
				else {
					slave.nextMessage = message.Clone ();
					slave.requireConfirmation = true;
				}
			}
		}

		public void SendNoReply(int slaveAddress, BufferFrame message)
		{
			if (slaveAddress == linkLayer.GetBroadcastAddress ()) {
				if (nextBroadcastMessage != null)
					throw new LinkLayerBusyException ("Broadcast message pending");
				else
					nextBroadcastMessage = message;
			} else {
				SlaveConnection slave = GetSlaveConnection (slaveAddress);

				if (slave != null) {
					if (slave.IsMessageWaitingToSend ())
						throw new LinkLayerBusyException ("Message pending");
					else {
						slave.nextMessage = message;
						slave.requireConfirmation = false;
					}
				}
			}
		}

		/********************************
		 * END IPrimaryLinkLayerUnbalanced
		 ********************************/

		public PrimaryLinkLayerUnbalanced(LinkLayer linkLayer, IPrimaryLinkLayerCallbacks callbacks, Action<string> debugLog)
		{
			this.linkLayer = linkLayer;
			this.callbacks = callbacks;
			this.DebugLog = debugLog;
			this.slaveConnections = new List<SlaveConnection> ();
		}

		private SlaveConnection GetSlaveConnection(int slaveAddres)
		{
			foreach (SlaveConnection connection in slaveConnections) {
				if (connection.address == slaveAddres)
					return connection;
			}

			return null;
		}

		public void AddSlaveConnection(int slaveAddress)
		{
			SlaveConnection slave = GetSlaveConnection (slaveAddress);

			if (slave == null)
				slaveConnections.Add (new SlaveConnection (slaveAddress, linkLayer, DebugLog, this));
		}


		public LinkLayerState GetStateOfSlave(int slaveAddress)
		{	
			SlaveConnection connection = GetSlaveConnection (slaveAddress);

			if (connection != null)
				return connection.linkLayerState;
			else
				throw new ArgumentException ("No slave with this address found");
		}



		public override void HandleMessage(FunctionCodeSecondary fcs, bool acd, bool dfc, 
			int address, byte[] msg, int userDataStart, int userDataLength)
		{
			SlaveConnection slave = GetSlaveConnection (address);

			if (slave != null) {

				slave.HandleMessage (fcs, acd, dfc, address, msg, userDataStart, userDataLength);

			} else {
				// response from unknown slave? What to do?
				DebugLog ("PLL RECV - response from unknown slave " + address + " !");
			}
		}
			

		private int currentSlaveIndex = 0;

		public override void RunStateMachine()
		{
			// run all the link layer state machines for the registered slaves

			if (slaveConnections.Count > 0) {

				if (currentSlave == null) {

					/* schedule next slave connection */
					currentSlave = slaveConnections [currentSlaveIndex];
					currentSlaveIndex = (currentSlaveIndex + 1) % slaveConnections.Count;

				}


				currentSlave.RunStateMachine ();

				if (currentSlave.waitingForResponse == false)
					currentSlave = null;
			}
		}

		public override void SendLinkLayerTestFunction()
		{
		}
	}
		
	internal class PrimaryLinkLayerBalanced : PrimaryLinkLayer
	{
		private Action<string> DebugLog;

		private PrimaryLinkLayerState primaryState = PrimaryLinkLayerState.IDLE;
		private LinkLayerState state = LinkLayerState.IDLE;

		private bool waitingForResponse = false; 
		private long lastSendTime;              
		private long originalSendTime;
		private bool sendLinkLayerTestFunction = false;
		private bool nextFcb = true;

		private BufferFrame lastSendASDU = null; // last send ASDU for message repetition after timeout

		private int linkLayerAddressOtherStation = 0;

		private LinkLayer linkLayer;

		Func<BufferFrame> GetUserData;


		public PrimaryLinkLayerBalanced(LinkLayer linkLayer, Func<BufferFrame> getUserData, Action<string> debugLog) 
		{
			this.DebugLog = debugLog;
			this.GetUserData = getUserData;
			this.linkLayer = linkLayer;
		}

		public LinkLayerState GetLinkLayerState()
		{
			return state;
		}

		public int LinkLayerAddressOtherStation {
			set { linkLayerAddressOtherStation = value; }
		}

		public override void HandleMessage(FunctionCodeSecondary fcs, bool dir, bool dfc, 
			int address, byte[] msg, int userDataStart, int userDataLength)
		{
			PrimaryLinkLayerState newState = primaryState;

			if (dfc) {

				//TODO stop sending ASDUs; only send Status of link requests
				//TODO switch to new state...

				switch (primaryState) {
				case PrimaryLinkLayerState.EXECUTE_REQUEST_STATUS_OF_LINK:
				case PrimaryLinkLayerState.EXECUTE_RESET_REMOTE_LINK:
					newState = PrimaryLinkLayerState.EXECUTE_REQUEST_STATUS_OF_LINK;
					break;
				case PrimaryLinkLayerState.EXECUTE_SERVICE_SEND_CONFIRM:
					//TODO message must be handled and switched to BUSY state later!
				case PrimaryLinkLayerState.SECONDARY_LINK_LAYER_BUSY:
					newState = PrimaryLinkLayerState.SECONDARY_LINK_LAYER_BUSY;
					break;
				}

				state = LinkLayerState.BUSY;
				primaryState = newState;
				return;

			}

			switch (fcs) {

			case FunctionCodeSecondary.ACK:
				//TODO what to do if we are not waiting for a response?
				DebugLog ("PLL - received ACK");
				if (primaryState == PrimaryLinkLayerState.EXECUTE_RESET_REMOTE_LINK) {
					newState = PrimaryLinkLayerState.LINK_LAYERS_AVAILABLE;
					state = LinkLayerState.AVAILABLE;
				}
				else if (primaryState == PrimaryLinkLayerState.EXECUTE_SERVICE_SEND_CONFIRM) {

					if (sendLinkLayerTestFunction) {
						nextFcb = !nextFcb;
						sendLinkLayerTestFunction = false;
					}

					state = LinkLayerState.AVAILABLE;
					newState = PrimaryLinkLayerState.LINK_LAYERS_AVAILABLE;
				}

				waitingForResponse = false;
				break;

			case FunctionCodeSecondary.NACK:
				DebugLog ("PLL - received NACK");
				if (primaryState == PrimaryLinkLayerState.EXECUTE_SERVICE_SEND_CONFIRM) {
					newState = PrimaryLinkLayerState.SECONDARY_LINK_LAYER_BUSY;
					state = LinkLayerState.BUSY;
				}
				break;

			case FunctionCodeSecondary.RESP_USER_DATA:

				newState = PrimaryLinkLayerState.IDLE;
				state = LinkLayerState.ERROR;

				break;

			case FunctionCodeSecondary.RESP_NACK_NO_DATA:

				newState = PrimaryLinkLayerState.IDLE;
				state = LinkLayerState.ERROR;

				break;


			case FunctionCodeSecondary.STATUS_OF_LINK_OR_ACCESS_DEMAND:	
				DebugLog ("PLL - received STATUS OF LINK");
				if (primaryState == PrimaryLinkLayerState.EXECUTE_REQUEST_STATUS_OF_LINK) {
					DebugLog ("PLL - SEND RESET REMOTE LINK");
					linkLayer.SendFixedFramePrimary (FunctionCodePrimary.RESET_REMOTE_LINK, linkLayerAddressOtherStation, false, false);
					lastSendTime = SystemUtils.currentTimeMillis ();
					waitingForResponse = true;
					newState = PrimaryLinkLayerState.EXECUTE_RESET_REMOTE_LINK;
					state = LinkLayerState.BUSY;
				} 
				else { /* illegal message */
					newState = PrimaryLinkLayerState.IDLE;
					state = LinkLayerState.ERROR;
				}

				break;

			case FunctionCodeSecondary.LINK_SERVICE_NOT_FUNCTIONING:
			case FunctionCodeSecondary.LINK_SERVICE_NOT_IMPLEMENTED:
				DebugLog ("PLL - link layer service not functioning/not implemented in secondary station ");
				if (primaryState == PrimaryLinkLayerState.EXECUTE_SERVICE_SEND_CONFIRM) {
					newState = PrimaryLinkLayerState.LINK_LAYERS_AVAILABLE;
					state = LinkLayerState.AVAILABLE;
				}
				break;

			default:
				DebugLog ("UNEXPECTED SECONDARY LINK LAYER MESSAGE");
				break;
			}

			DebugLog ("PLL RECV - old state: " + primaryState.ToString () + " new state: " + newState.ToString ());

			primaryState = newState;

		}

		public override void SendLinkLayerTestFunction()
		{
			sendLinkLayerTestFunction = true;
		}

		public override void RunStateMachine()
		{
			PrimaryLinkLayerState newState = primaryState;

			switch (primaryState) {

			case PrimaryLinkLayerState.IDLE:
				
				waitingForResponse = false;
				originalSendTime = 0;
				lastSendTime = 0;
				sendLinkLayerTestFunction = false;
				newState = PrimaryLinkLayerState.EXECUTE_REQUEST_STATUS_OF_LINK;

				break;

			case PrimaryLinkLayerState.EXECUTE_REQUEST_STATUS_OF_LINK:
				
				if (waitingForResponse) {
					if (SystemUtils.currentTimeMillis () > (lastSendTime + linkLayer.TimeoutForACK)) {
						linkLayer.SendFixedFramePrimary (FunctionCodePrimary.REQUEST_LINK_STATUS, linkLayerAddressOtherStation, false, false);
						lastSendTime = SystemUtils.currentTimeMillis ();
					}
				}
				else {
					DebugLog ("PLL - SEND RESET REMOTE LINK");
					linkLayer.SendFixedFramePrimary (FunctionCodePrimary.RESET_REMOTE_LINK, linkLayerAddressOtherStation, false, false);
					lastSendTime = SystemUtils.currentTimeMillis ();
					waitingForResponse = true;
					newState = PrimaryLinkLayerState.EXECUTE_RESET_REMOTE_LINK; 
				}

				break;

			case PrimaryLinkLayerState.EXECUTE_RESET_REMOTE_LINK:
				
				if (waitingForResponse) {
					if (SystemUtils.currentTimeMillis () > (lastSendTime + linkLayer.TimeoutForACK)) {
						waitingForResponse = false;
						newState = PrimaryLinkLayerState.IDLE;
						state = LinkLayerState.ERROR;
					}
				} else {
					newState = PrimaryLinkLayerState.LINK_LAYERS_AVAILABLE;
					state = LinkLayerState.AVAILABLE;
				}

				break;

			case PrimaryLinkLayerState.LINK_LAYERS_AVAILABLE:

				if (sendLinkLayerTestFunction) {
					DebugLog ("PLL - SEND TEST LINK");
					linkLayer.SendFixedFramePrimary (FunctionCodePrimary.TEST_FUNCTION_FOR_LINK, linkLayerAddressOtherStation, nextFcb, true);
					nextFcb = !nextFcb;
					lastSendTime = SystemUtils.currentTimeMillis ();
					originalSendTime = lastSendTime;
					newState = PrimaryLinkLayerState.EXECUTE_SERVICE_SEND_CONFIRM;
				}
				else {
					BufferFrame asdu = GetUserData ();

					if (asdu != null) {

						linkLayer.SendVariableLengthFramePrimary (FunctionCodePrimary.USER_DATA_CONFIRMED, linkLayerAddressOtherStation, nextFcb, true, asdu);

						nextFcb = !nextFcb;
						lastSendASDU = asdu;
						lastSendTime = SystemUtils.currentTimeMillis ();
						originalSendTime = lastSendTime;
						waitingForResponse = true;

						newState = PrimaryLinkLayerState.EXECUTE_SERVICE_SEND_CONFIRM;
					}
				}

				break;

			case PrimaryLinkLayerState.EXECUTE_SERVICE_SEND_CONFIRM:

				if (SystemUtils.currentTimeMillis () > (lastSendTime + linkLayer.TimeoutForACK)) {

					if (SystemUtils.currentTimeMillis () > (originalSendTime + linkLayer.TimeoutRepeat)) {
						DebugLog ("TIMEOUT: ASDU not confirmed after repeated transmission");
						newState = PrimaryLinkLayerState.IDLE;
						state = LinkLayerState.ERROR;
					} else {
						DebugLog ("TIMEOUT: ASDU not confirmed");

						if (sendLinkLayerTestFunction) {
							DebugLog ("PLL - REPEAT SEND RESET REMOTE LINK");
							linkLayer.SendFixedFramePrimary (FunctionCodePrimary.TEST_FUNCTION_FOR_LINK, linkLayerAddressOtherStation, !nextFcb, true);
							lastSendTime = SystemUtils.currentTimeMillis ();
						}
						else
							linkLayer.SendVariableLengthFramePrimary (FunctionCodePrimary.USER_DATA_CONFIRMED, linkLayerAddressOtherStation, !nextFcb, true, lastSendASDU);

						lastSendTime = SystemUtils.currentTimeMillis ();
					}
				}

				break;

			case PrimaryLinkLayerState.SECONDARY_LINK_LAYER_BUSY:
				//TODO - reject new requests from application layer?
				break;

			}

			if (primaryState != newState)
				DebugLog ("PLL - old state: " + primaryState.ToString () + " new state: " + newState.ToString ());

			primaryState = newState;

		}
	}
	

}

