/*
 *  Copyright 2016-2019 MZ Automation GmbH
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

using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.IO;

using lib60870;
using lib60870.CS101;
using System.Collections.Concurrent;

namespace lib60870.CS104
{
    /// <summary>
    /// Represents a client (master) connection
    /// </summary>
    public class ClientConnection : IMasterConnection
    {
        private static int connectionsCounter = 0;

        private int connectionID;

        private void DebugLog(string msg)
        {
            if (debugOutput)
            {
                Console.Write("CS104 SLAVE CONNECTION ");
                Console.Write(connectionID);
                Console.Write(": ");
                Console.WriteLine(msg);
            }
        }

        static byte[] STARTDT_CON_MSG = new byte[] { 0x68, 0x04, 0x0b, 0x00, 0x00, 0x00 };

        static byte[] STOPDT_CON_MSG = new byte[] { 0x68, 0x04, 0x23, 0x00, 0x00, 0x00 };

        static byte[] TESTFR_CON_MSG = new byte[] { 0x68, 0x04, 0x83, 0x00, 0x00, 0x00 };

        static byte[] TESTFR_ACT_MSG = new byte[] { 0x68, 0x04, 0x43, 0x00, 0x00, 0x00 };

        private int sendCount = 0;
        private int receiveCount = 0;

        /* number of unconfirmed messages received */
        private int unconfirmedReceivedIMessages = 0;

        /* T3 parameter handling */
        private UInt64 nextT3Timeout;

        /* TEST-FR con timeout handling */
        private bool waitingForTestFRcon = false;
        private UInt64 nextTestFRConTimeout = 0;

        /* T2 parameter handling */
        private bool timeoutT2Triggered = false;

        /* timestamp when the last confirmation message was sent */
        private UInt64 lastConfirmationTime = System.UInt64.MaxValue;
      
        private TlsSecurityInformation tlsSecInfo = null;

        private APCIParameters apciParameters;
        private ApplicationLayerParameters alParameters;

        private Server server;

        private ConcurrentQueue<ASDU> receivedASDUs = null;
        private Thread callbackThread = null;
        private bool callbackThreadRunning = false;

        private Queue<BufferFrame> waitingASDUsHighPrio = null;

        /* data structure for k-size sent ASDU buffer */
        private struct SentASDU
        {
            // required to identify message in server (low-priority) queue
            public long entryTime;

            /* -1 if ASDU is not from low-priority queue */
            public int queueIndex;

            /* timestamp when the message was sent (for T1 timeout) */
            public long sentTime;

            /* sequence number used to send the message */
            public int seqNo;
        }

        private int maxSentASDUs;
        private int oldestSentASDU = -1;
        private int newestSentASDU = -1;
        private SentASDU[] sentASDUs = null;

        private ASDUQueue asduQueue = null;

        private FileServer fileServer;

        internal ASDUQueue GetASDUQueue()
        {
            return asduQueue;
        }

        private void ProcessASDUs()
        {
            callbackThreadRunning = true;

            while (callbackThreadRunning)
            {

                try
                {
                    while ((receivedASDUs.Count > 0) && (callbackThreadRunning) && (running))
                    {
    				
                        ASDU asdu;

                        if (receivedASDUs.TryDequeue(out asdu))
                        {
                            HandleASDU(asdu);
                        }
    						
                    }

                    Thread.Sleep(50);
                }
                catch (ASDUParsingException)
                {
                    DebugLog("Failed to parse ASDU --> close connection");
                    running = false;
                }

            }

            DebugLog("ProcessASDUs exit thread");
        }

        private IPEndPoint remoteEndpoint;

        /// <summary>
        /// Gets the remote endpoint (client IP address and TCP port)
        /// </summary>
        /// <value>The remote IP endpoint</value>
        public IPEndPoint RemoteEndpoint
        {
            get
            {
                return remoteEndpoint;
            }
        }

        internal ClientConnection(Socket socket, TlsSecurityInformation tlsSecInfo, APCIParameters apciParameters, ApplicationLayerParameters parameters, Server server, ASDUQueue asduQueue, bool debugOutput)
        {
            connectionsCounter++;
            connectionID = connectionsCounter;

            this.remoteEndpoint = (IPEndPoint)socket.RemoteEndPoint;

            this.apciParameters = apciParameters;
            this.alParameters = parameters;
            this.server = server;
            this.asduQueue = asduQueue;
            this.debugOutput = debugOutput;

            ResetT3Timeout((UInt64)SystemUtils.currentTimeMillis());

            maxSentASDUs = apciParameters.K;
            this.sentASDUs = new SentASDU[maxSentASDUs];

            receivedASDUs = new ConcurrentQueue<ASDU>();
            waitingASDUsHighPrio = new Queue<BufferFrame>();

            socketStream = new NetworkStream(socket);
            this.socket = socket;
            this.tlsSecInfo = tlsSecInfo;

            this.fileServer = new FileServer(this, server.GetAvailableFiles(), DebugLog);

            if (server.fileTimeout != null)
                this.fileServer.Timeout = (long) server.fileTimeout;

            this.fileServer.SetFileReadyHandler (server.fileReadyHandler, server.fileReadyHandlerParameter);

            Thread workerThread = new Thread(HandleConnection);

            workerThread.Start();
        }

        /// <summary>
        /// Gets the connection parameters.
        /// </summary>
        /// <returns>The connection parameters used by the server.</returns>
        public ApplicationLayerParameters GetApplicationLayerParameters()
        {
            return alParameters;
        }

        private void ResetT3Timeout(UInt64 currentTime)
        {
            nextT3Timeout = (UInt64)SystemUtils.currentTimeMillis() + (UInt64)(apciParameters.T3 * 1000);
        }

        private bool CheckT3Timeout(UInt64 currentTime)
        {
            if (waitingForTestFRcon)
                return false;

            if (nextT3Timeout > (currentTime + (UInt64)(apciParameters.T3 * 1000)))
            {
                /* timeout value not plausible (maybe system time changed) */
                ResetT3Timeout(currentTime);
            }

            if (currentTime > nextT3Timeout)
                return true;
            else
                return false;
        }

        private void ResetTestFRConTimeout(UInt64 currentTime)
        {
            nextTestFRConTimeout = currentTime + (UInt64)(apciParameters.T1 * 1000);
        }

        private bool CheckTestFRConTimeout(UInt64 currentTime)
        {
            if (nextTestFRConTimeout > (currentTime + (UInt64)(apciParameters.T1 * 1000)))
            {
                /* timeout value not plausible (maybe system time changed) */
                ResetTestFRConTimeout(currentTime);
            }

            if (currentTime > nextTestFRConTimeout)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Flag indicating that this connection is the active connection.
        /// The active connection is the only connection that is answering
        /// application layer requests and sends cyclic, and spontaneous messages.
        /// </summary>
        private bool isActive = false;

        /// <summary>
        /// Gets or sets a value indicating whether this connection is active.
        /// The active connection is the only connection that is answering
        /// application layer requests and sends cyclic, and spontaneous messages.
        /// </summary>
        /// <value><c>true</c> if this instance is active; otherwise, <c>false</c>.</value>
        public bool IsActive
        {
            get
            {
                return this.isActive;
            }
            set
            {

                if (isActive != value)
                {

                    isActive = value;

                    if (isActive)
                        DebugLog("is active");
                    else
                        DebugLog("is not active");
                }
            }
        }

        private Socket socket;
        private Stream socketStream;

        private bool running = false;

        private bool debugOutput = true;

        private int readState = 0; /* 0 - idle, 1 - start received, 2 - reading remaining bytes */
        private int currentReadPos = 0;
        private int currentReadMsgLength = 0;
        private int remainingReadLength = 0;
        private long currentReadTimeout = 0;

        private int receiveMessage(byte[] buffer)
        {
            /* check receive timeout */
            if (readState != 0)
            {
                if (SystemUtils.currentTimeMillis() > currentReadTimeout)
                {
                    DebugLog("Receive timeout!");
                    return -1;
                }
            }

            if (socket.Poll(50, SelectMode.SelectRead))
            {

                if (readState == 0)
                {
                    // wait for start byte
                    if (socketStream.Read(buffer, 0, 1) != 1)
                        return -1;

                    if (buffer[0] != 0x68)
                    {
                        DebugLog("Missing SOF indicator!");

                        return -1;
                    }

                    readState = 1;
                }

                if (readState == 1)
                {
                    // read length byte
                    if (socketStream.Read(buffer, 1, 1) != 1)
                        return 0;

                    currentReadMsgLength = buffer[1];
                    remainingReadLength = currentReadMsgLength;
                    currentReadPos = 2;

                    readState = 2;
                }

                if (readState == 2)
                {
                    int readLength = socketStream.Read(buffer, currentReadPos, remainingReadLength);

                    if (readLength == remainingReadLength)
                    {
                        readState = 0;
                        currentReadTimeout = 0;
                        return 2 + currentReadMsgLength;
                    }
                    else
                    {
                        currentReadPos += readLength;
                        remainingReadLength = remainingReadLength - readLength;
                    }
                }

                if (currentReadTimeout == 0)
                {
                    currentReadTimeout = SystemUtils.currentTimeMillis() + server.ReceiveTimeout;
                }
            }

            return 0;
        }

        private void SendSMessage()
        {
            DebugLog("Send S message");

            byte[] msg = new byte[6];

            msg[0] = 0x68;
            msg[1] = 0x04;
            msg[2] = 0x01;
            msg[3] = 0;

            lock (socketStream)
            {
                msg[4] = (byte)((receiveCount % 128) * 2);
                msg[5] = (byte)(receiveCount / 128);

                try
                {
                    socketStream.Write(msg, 0, msg.Length);
                }
                catch (System.IO.IOException)
                {
                    // socket error --> close connection
                    running = false;
                }
            }
        }

        private int SendIMessage(BufferFrame asdu)
        {

            byte[] buffer = asdu.GetBuffer();

            int msgSize = asdu.GetMsgSize(); /* ASDU size + ACPI size */

            buffer[0] = 0x68;

            /* set size field */
            buffer[1] = (byte)(msgSize - 2);

            buffer[2] = (byte)((sendCount % 128) * 2);
            buffer[3] = (byte)(sendCount / 128);

            buffer[4] = (byte)((receiveCount % 128) * 2);
            buffer[5] = (byte)(receiveCount / 128);

            try
            {
                lock (socketStream)
                {
                    socketStream.Write(buffer, 0, msgSize);
                    DebugLog("SEND I (size = " + msgSize + ") : " +	BitConverter.ToString(buffer, 0, msgSize));
                    sendCount = (sendCount + 1) % 32768;
                    unconfirmedReceivedIMessages = 0;
                    timeoutT2Triggered = false;
                }
            }
            catch (System.IO.IOException)
            {
                // socket error --> close connection
                running = false;
            }

            return sendCount;
        }

        private bool isSentBufferFull()
        {

            if (oldestSentASDU == -1)
                return false;

            int newIndex = (newestSentASDU + 1) % maxSentASDUs;

            if (newIndex == oldestSentASDU)
                return true;
            else
                return false;
        }

        private void PrintSendBuffer()
        {
            if (debugOutput)
            {
                if (oldestSentASDU != -1)
                {

                    int currentIndex = oldestSentASDU;

                    int nextIndex = 0;

                    DebugLog("------k-buffer------");

                    do
                    {
                        DebugLog(currentIndex + " : S " + sentASDUs[currentIndex].seqNo + " : time " +
                            sentASDUs[currentIndex].sentTime + " : " + sentASDUs[currentIndex].queueIndex);

                        if (currentIndex == newestSentASDU)
                            nextIndex = -1;
                        else
                            currentIndex = (currentIndex + 1) % maxSentASDUs;

                    } while (nextIndex != -1);

                    DebugLog("--------------------");
					
                }
            }
        }

        private void sendNextAvailableASDU()
        {
            lock (sentASDUs)
            {
                if (isSentBufferFull())
                    return;

                long timestamp;
                int index;

                asduQueue.LockASDUQueue();
                BufferFrame asdu = asduQueue.GetNextWaitingASDU(out timestamp, out index);

                try
                {
                    if (asdu != null)
                    {
                        int currentIndex = 0;

                        if (oldestSentASDU == -1)
                        {
                            oldestSentASDU = 0;
                            newestSentASDU = 0;

                        }
                        else
                        {
                            currentIndex = (newestSentASDU + 1) % maxSentASDUs;
                        }
							
                        sentASDUs[currentIndex].entryTime = timestamp;
                        sentASDUs[currentIndex].queueIndex = index;
                        sentASDUs[currentIndex].seqNo = SendIMessage(asdu);
                        sentASDUs[currentIndex].sentTime = SystemUtils.currentTimeMillis();

                        newestSentASDU = currentIndex;

                        PrintSendBuffer();
                    }
                }
                finally
                {
                    asduQueue.UnlockASDUQueue();
                }
            }
        }

        private bool sendNextHighPriorityASDU()
        {
            lock (sentASDUs)
            {
                if (isSentBufferFull())
                    return false;

                BufferFrame asdu = waitingASDUsHighPrio.Dequeue();

                if (asdu != null)
                {

                    int currentIndex = 0;

                    if (oldestSentASDU == -1)
                    {
                        oldestSentASDU = 0;
                        newestSentASDU = 0;

                    }
                    else
                    {
                        currentIndex = (newestSentASDU + 1) % maxSentASDUs;
                    }
						
                    sentASDUs[currentIndex].queueIndex = -1;
                    sentASDUs[currentIndex].seqNo = SendIMessage(asdu);
                    sentASDUs[currentIndex].sentTime = SystemUtils.currentTimeMillis();

                    newestSentASDU = currentIndex;

                    PrintSendBuffer();
                }
                else
                    return false;
            }

            return true;
        }

        private void SendWaitingASDUs()
        {

            lock (waitingASDUsHighPrio)
            {

                while (waitingASDUsHighPrio.Count > 0)
                {

                    if (sendNextHighPriorityASDU() == false)
                        return;

                    if (running == false)
                        return;
                }
            }

            // send messages from low-priority queue
            sendNextAvailableASDU();
        }

        private void SendASDUInternal(ASDU asdu)
        {
            if (isActive)
            {
                lock (waitingASDUsHighPrio)
                {

                    BufferFrame frame = new BufferFrame(new byte[256], 6);

                    asdu.Encode(frame, alParameters);

                    waitingASDUsHighPrio.Enqueue(frame);
                }

                SendWaitingASDUs();
            } 
        }

        /// <summary>
        /// Send a response ASDU over this connection
        /// </summary>
        /// <exception cref="ConnectionException">Throws an exception if the connection is no longer active (e.g. because it has been closed by the other side).</exception>
        /// <param name="asdu">The ASDU to send</param>
        public void SendASDU(ASDU asdu)
        {
            if (isActive)
                SendASDUInternal(asdu);
            else
                throw new ConnectionException("Connection not active");
        }

        public void SendACT_CON(ASDU asdu, bool negative)
        {
            asdu.Cot = CauseOfTransmission.ACTIVATION_CON;
            asdu.IsNegative = negative;

            SendASDU(asdu);
        }

        public void SendACT_TERM(ASDU asdu)
        {
            asdu.Cot = CauseOfTransmission.ACTIVATION_TERMINATION;
            asdu.IsNegative = false;

            SendASDU(asdu);
        }

        private void HandleASDU(ASDU asdu)
        {		
            DebugLog("Handle received ASDU");

            bool messageHandled = false;

            switch (asdu.TypeId)
            {

                case TypeID.C_IC_NA_1: /* 100 - interrogation command */

                    DebugLog("Rcvd interrogation command C_IC_NA_1");

                    if ((asdu.Cot == CauseOfTransmission.ACTIVATION) || (asdu.Cot == CauseOfTransmission.DEACTIVATION))
                    {
                        if (server.interrogationHandler != null)
                        {

                            InterrogationCommand irc = (InterrogationCommand)asdu.GetElement(0);

                            if (server.interrogationHandler(server.InterrogationHandlerParameter, this, asdu, irc.QOI))
                                messageHandled = true;
                        }
                    }
                    else
                    {
                        asdu.Cot = CauseOfTransmission.UNKNOWN_CAUSE_OF_TRANSMISSION;
                        asdu.IsNegative = true;
                        this.SendASDUInternal(asdu);
                    }

                    break;

                case TypeID.C_CI_NA_1: /* 101 - counter interrogation command */

                    DebugLog("Rcvd counter interrogation command C_CI_NA_1");

                    if ((asdu.Cot == CauseOfTransmission.ACTIVATION) || (asdu.Cot == CauseOfTransmission.DEACTIVATION))
                    {
                        if (server.counterInterrogationHandler != null)
                        {

                            CounterInterrogationCommand cic = (CounterInterrogationCommand)asdu.GetElement(0);

                            if (server.counterInterrogationHandler(server.counterInterrogationHandlerParameter, this, asdu, cic.QCC))
                                messageHandled = true;
                        }
                    }
                    else
                    {
                        asdu.Cot = CauseOfTransmission.UNKNOWN_CAUSE_OF_TRANSMISSION;
                        asdu.IsNegative = true;
                        this.SendASDUInternal(asdu);
                    }

                    break;

                case TypeID.C_RD_NA_1: /* 102 - read command */

                    DebugLog("Rcvd read command C_RD_NA_1");

                    if (asdu.Cot == CauseOfTransmission.REQUEST)
                    {

                        DebugLog("Read request for object: " + asdu.Ca);

                        if (server.readHandler != null)
                        {
                            ReadCommand rc = (ReadCommand)asdu.GetElement(0);

                            if (server.readHandler(server.readHandlerParameter, this, asdu, rc.ObjectAddress))
                                messageHandled = true;

                        }

                    }
                    else
                    {
                        asdu.Cot = CauseOfTransmission.UNKNOWN_CAUSE_OF_TRANSMISSION;
                        asdu.IsNegative = true;
                        this.SendASDUInternal(asdu);
                    }

                    break;

                case TypeID.C_CS_NA_1: /* 103 - Clock synchronization command */

                    DebugLog("Rcvd clock sync command C_CS_NA_1");

                    if (asdu.Cot == CauseOfTransmission.ACTIVATION)
                    {

                        if (server.clockSynchronizationHandler != null)
                        {

                            ClockSynchronizationCommand csc = (ClockSynchronizationCommand)asdu.GetElement(0);

                            if (server.clockSynchronizationHandler(server.clockSynchronizationHandlerParameter,
                                this, asdu, csc.NewTime))
                                messageHandled = true;
                        }

                    }
                    else
                    {
                        asdu.Cot = CauseOfTransmission.UNKNOWN_CAUSE_OF_TRANSMISSION;
                        asdu.IsNegative = true;
                        this.SendASDUInternal(asdu);
                    }

                    break;

                case TypeID.C_TS_NA_1: /* 104 - test command */

                    DebugLog("Rcvd test command C_TS_NA_1");

                    if (asdu.Cot != CauseOfTransmission.ACTIVATION)
                    {
                        asdu.Cot = CauseOfTransmission.UNKNOWN_CAUSE_OF_TRANSMISSION;
                        asdu.IsNegative = true;
                    }
                    else
                        asdu.Cot = CauseOfTransmission.ACTIVATION_CON;

                    this.SendASDUInternal(asdu);

                    messageHandled = true;

                    break;

                case TypeID.C_RP_NA_1: /* 105 - Reset process command */

                    DebugLog("Rcvd reset process command C_RP_NA_1");

                    if (asdu.Cot == CauseOfTransmission.ACTIVATION)
                    {

                        if (server.resetProcessHandler != null)
                        {

                            ResetProcessCommand rpc = (ResetProcessCommand)asdu.GetElement(0);

                            if (server.resetProcessHandler(server.resetProcessHandlerParameter,
                                this, asdu, rpc.QRP))
                                messageHandled = true;
                        }

                    }
                    else
                    {
                        asdu.Cot = CauseOfTransmission.UNKNOWN_CAUSE_OF_TRANSMISSION;
                        asdu.IsNegative = true;
                        this.SendASDUInternal(asdu);
                    }


                    break;

                case TypeID.C_CD_NA_1: /* 106 - Delay acquisition command */

                    DebugLog("Rcvd delay acquisition command C_CD_NA_1");

                    if ((asdu.Cot == CauseOfTransmission.ACTIVATION) || (asdu.Cot == CauseOfTransmission.SPONTANEOUS))
                    {
                        if (server.delayAcquisitionHandler != null)
                        {

                            DelayAcquisitionCommand dac = (DelayAcquisitionCommand)asdu.GetElement(0);

                            if (server.delayAcquisitionHandler(server.delayAcquisitionHandlerParameter,
                                this, asdu, dac.Delay))
                                messageHandled = true;
                        }
                    }
                    else
                    {
                        asdu.Cot = CauseOfTransmission.UNKNOWN_CAUSE_OF_TRANSMISSION;
                        asdu.IsNegative = true;
                        this.SendASDUInternal(asdu);
                    }

                    break;
            }

            if (messageHandled == false)
                messageHandled = fileServer.HandleFileAsdu(asdu);

            if ((messageHandled == false) && (server.asduHandler != null))
                if (server.asduHandler(server.asduHandlerParameter, this, asdu))
                    messageHandled = true;

            if (messageHandled == false)
            {
                asdu.Cot = CauseOfTransmission.UNKNOWN_TYPE_ID;
                asdu.IsNegative = true;
                this.SendASDUInternal(asdu);
            }

        }

        private bool CheckSequenceNumber(int seqNo)
        {
            lock (sentASDUs)
            {
                /* check if received sequence number is valid */

                bool seqNoIsValid = false;
                bool counterOverflowDetected = false;
                int oldestValidSeqNo = -1;

                if (oldestSentASDU == -1)
                { /* if k-Buffer is empty */
                    if (seqNo == sendCount)
                        seqNoIsValid = true;
                }
                else
                {
                    // Two cases are required to reflect sequence number overflow
                    if (sentASDUs[oldestSentASDU].seqNo <= sentASDUs[newestSentASDU].seqNo)
                    {
                        if ((seqNo >= sentASDUs[oldestSentASDU].seqNo) &&
                        (seqNo <= sentASDUs[newestSentASDU].seqNo))
                        {
                            seqNoIsValid = true;
                        }
                    }
                    else
                    {
                        if ((seqNo >= sentASDUs[oldestSentASDU].seqNo) ||
                        (seqNo <= sentASDUs[newestSentASDU].seqNo))
                        {
                            seqNoIsValid = true;
                        }

                        counterOverflowDetected = true;
                    }

                    if (sentASDUs[oldestSentASDU].seqNo == 0)
                        oldestValidSeqNo = 32767;
                    else
                        oldestValidSeqNo = sentASDUs[oldestSentASDU].seqNo - 1;

                    if (oldestValidSeqNo == seqNo)
                        seqNoIsValid = true;
                }
					
                if (seqNoIsValid == false)
                {
                    DebugLog("Received sequence number out of range");
                    return false;
                }
								
                if (oldestSentASDU != -1)
                {
                    /* remove confirmed messages from list */
                    do
                    {
                        /* skip removing messages if confirmed message was already removed */
                        if (counterOverflowDetected == false)
                        {
                            if (seqNo < sentASDUs[oldestSentASDU].seqNo)
                                break;
                        }

                        if (seqNo == oldestValidSeqNo)
                            break;

                        /* remove from server (low-priority) queue if required */
                        if (sentASDUs[oldestSentASDU].queueIndex != -1)
                        {
                            asduQueue.MarkASDUAsConfirmed(sentASDUs[oldestSentASDU].queueIndex,
                                sentASDUs[oldestSentASDU].entryTime);
                        }

                        if (sentASDUs[oldestSentASDU].seqNo == seqNo)
                        {
                            /* we arrived at the seq# that has been confirmed */

                            if (oldestSentASDU == newestSentASDU)
                                oldestSentASDU = -1;
                            else
                                oldestSentASDU = (oldestSentASDU + 1) % maxSentASDUs;

                            break;
                        }

                        oldestSentASDU = (oldestSentASDU + 1) % maxSentASDUs;

                        int checkIndex = (newestSentASDU + 1) % maxSentASDUs;

                        if (oldestSentASDU == checkIndex)
                        {
                            oldestSentASDU = -1;
                            break;
                        }

                    } while (true);
                }
            }

            return true;
        }

        private bool HandleMessage(byte[] buffer, int msgSize)
        {
            UInt64 currentTime = (UInt64) SystemUtils.currentTimeMillis();

            if ((buffer[2] & 1) == 0)
            {

                if (msgSize < 7)
                {
                    DebugLog("I msg too small!");
                    return false;
                }
					
                if (timeoutT2Triggered == false)
                {
                    timeoutT2Triggered = true;
                    lastConfirmationTime = currentTime; /* start timeout T2 */
                }

                int frameSendSequenceNumber = ((buffer[3] * 0x100) + (buffer[2] & 0xfe)) / 2;
                int frameRecvSequenceNumber = ((buffer[5] * 0x100) + (buffer[4] & 0xfe)) / 2;

                DebugLog("Received I frame: N(S) = " + frameSendSequenceNumber + " N(R) = " + frameRecvSequenceNumber);

                /* check the receive sequence number N(R) - connection will be closed on an unexpected value */
                if (frameSendSequenceNumber != receiveCount)
                {
                    DebugLog("Sequence error: Close connection!");
                    return false;
                }

                if (CheckSequenceNumber(frameRecvSequenceNumber) == false)
                {
                    DebugLog("Sequence number check failed");
                    return false;
                }

                receiveCount = (receiveCount + 1) % 32768;
                unconfirmedReceivedIMessages++;

                if (isActive)
                {
                    try
                    {
                        ASDU asdu = new ASDU(alParameters, buffer, 6, msgSize);
					
                        // push to handler thread for processing
                        DebugLog("Enqueue received I-message for processing");
                        receivedASDUs.Enqueue(asdu);
                    }
                    catch (ASDUParsingException e)
                    {
                        DebugLog("ASDU parsing failed: " + e.Message);
                        return false;
                    }
                }
                else
                {
                    // connection not active
                    DebugLog("Connection not active -> close connection");

                    return false;
                }
            }

			// Check for TESTFR_ACT message
			else if ((buffer[2] & 0x43) == 0x43)
            {

                DebugLog("Send TESTFR_CON");

                socketStream.Write(TESTFR_CON_MSG, 0, TESTFR_CON_MSG.Length);
            } 

			// Check for STARTDT_ACT message
			else if ((buffer[2] & 0x07) == 0x07)
            {

                DebugLog("Send STARTDT_CON");

                if (this.isActive == false)
                {
                    this.isActive = true;

                    this.server.Activated(this);
                }

                socketStream.Write(STARTDT_CON_MSG, 0, TESTFR_CON_MSG.Length);
            }

			// Check for STOPDT_ACT message
			else if ((buffer[2] & 0x13) == 0x13)
            {
				
                DebugLog("Send STOPDT_CON");

                if (this.isActive == true)
                {
                    this.isActive = false;

                    this.server.Deactivated(this);
                }

                socketStream.Write(STOPDT_CON_MSG, 0, TESTFR_CON_MSG.Length);
            } 

			// Check for TESTFR_CON message
			else if ((buffer[2] & 0x83) == 0x83)
            {
                DebugLog("Recv TESTFR_CON");

                waitingForTestFRcon = false;

                ResetT3Timeout(currentTime);
            }

			// S-message
			else if (buffer[2] == 0x01)
            {
                if (isActive == false)
                {
                    // connection not active
                    DebugLog("Connection not active -> close connection");

                    return false;
                }

                int seqNo = (buffer[4] + buffer[5] * 0x100) / 2;

                DebugLog("Recv S(" + seqNo + ") (own sendcounter = " + sendCount + ")");

                if (CheckSequenceNumber(seqNo) == false)
                    return false;
					
            }
            else
            {
                DebugLog("Unknown message");
            }

            ResetT3Timeout(currentTime);

            return true;
        }

        private bool handleTimeouts()
        {
            UInt64 currentTime = (UInt64)SystemUtils.currentTimeMillis();

            if (CheckT3Timeout(currentTime))
            {
                try
                {
                    socketStream.Write(TESTFR_ACT_MSG, 0, TESTFR_ACT_MSG.Length);

                    DebugLog("U message T3 timeout");
                    ResetT3Timeout(currentTime);
                }
                catch (System.IO.IOException)
                {
                    running = false;
                }

                waitingForTestFRcon = true;

                ResetTestFRConTimeout(currentTime);                
            }

            /* Check for TEST FR con timeout */
            if (waitingForTestFRcon)
            {
                if (CheckTestFRConTimeout(currentTime))
                {
                    DebugLog("Timeout for TESTFR_CON message");

                    // close connection
                    return false;
                }
            }

            if (unconfirmedReceivedIMessages > 0)
            {

                if ((currentTime - lastConfirmationTime) >= (UInt64)(apciParameters.T2 * 1000))
                {

                    lastConfirmationTime = currentTime;
                    unconfirmedReceivedIMessages = 0;
                    timeoutT2Triggered = false;
                    SendSMessage();
                }
            }
				
            /* check if counterpart confirmed I messages */
            lock (sentASDUs)
            {
                if (oldestSentASDU != -1)
                {
                    if (((long)currentTime - sentASDUs[oldestSentASDU].sentTime) >= (apciParameters.T1 * 1000))
                    {

                        PrintSendBuffer();
                        DebugLog("I message timeout for " + oldestSentASDU + " seqNo: " + sentASDUs[oldestSentASDU].seqNo);
                        return false;
                    }
                }
            }

            return true;
        }

        private bool AreByteArraysEqual(byte[] array1, byte[] array2)
        {
            if (array1.Length == array2.Length)
            {

                for (int i = 0; i < array1.Length; i++)
                {
                    if (array1[i] != array2[i])
                        return false;
                }

                return true;
            }
            else
                return false;
        }

        public bool CertificateValidationCallback(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None || sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors)
            {
                if (tlsSecInfo.ChainValidation)
                {
                    X509Chain newChain = new X509Chain();

                    newChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                    newChain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
                    newChain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
                    newChain.ChainPolicy.VerificationTime = DateTime.Now;
                    newChain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan(0, 0, 0);

                    foreach (X509Certificate2 caCert in tlsSecInfo.CaCertificates)
                        newChain.ChainPolicy.ExtraStore.Add(caCert);
					
                    bool certificateStatus = newChain.Build(new X509Certificate2(cert.GetRawCertData()));

                    if (certificateStatus == false)
                        return false;
                }

                if (tlsSecInfo.AllowOnlySpecificCertificates)
                {
                    foreach (X509Certificate2 allowedCert in tlsSecInfo.AllowedCertificates)
                    {
                        if (AreByteArraysEqual(allowedCert.GetCertHash(), cert.GetCertHash()))
                        {
                            return true;
                        }
                    }

                    return false;
                }

                return true;
            }
            else
                return false;
        }

        private void HandleConnection()
        {
            byte[] bytes = new byte[300];

            try
            {
                try
                {
                    running = true;

                    if (tlsSecInfo != null)
                    {
                        DebugLog("Setup TLS");

                        RemoteCertificateValidationCallback validationCallback = CertificateValidationCallback;

                        if (tlsSecInfo.CertificateValidationCallback != null)
                            validationCallback = tlsSecInfo.CertificateValidationCallback;

                        SslStream sslStream = new SslStream(socketStream, true, validationCallback);

                        bool authenticationSuccess = false;

                        try
                        {
                            sslStream.AuthenticateAsServer(tlsSecInfo.OwnCertificate, true, tlsSecInfo.TlsVersion, false);
						
                            if (sslStream.IsAuthenticated == true)
                            {
                                socketStream = sslStream;
                                authenticationSuccess = true;
                            }
							
                        }
                        catch (IOException e)
                        {
                            if (e.GetBaseException() != null)
                            {
                                DebugLog("TLS authentication error: " + e.GetBaseException().Message);
                            }
                            else
                            {
                                DebugLog("TLS authentication error: " + e.Message);
                            }
                        }
							
                        if (authenticationSuccess == true)
                            socketStream = sslStream;
                        else
                        {
                            DebugLog("TLS authentication failed");
                            running = false;
                        }
                    }

                    if (running)
                    {
                        socketStream.ReadTimeout = 50;

                        callbackThread = new Thread(ProcessASDUs);
                        callbackThread.Start();

                        ResetT3Timeout((UInt64)SystemUtils.currentTimeMillis());
                    }

                    while (running)
                    {

                        try
                        {
                            // Receive the response from the remote device.
                            int bytesRec = receiveMessage(bytes);

                            if (bytesRec > 0)
                            {
							
                                DebugLog("RCVD: " +	BitConverter.ToString(bytes, 0, bytesRec));

                                if (HandleMessage(bytes, bytesRec) == false)
                                {
                                    /* close connection on error */
                                    running = false;
                                }

                                if (unconfirmedReceivedIMessages >= apciParameters.W)
                                {
                                    lastConfirmationTime = (UInt64)SystemUtils.currentTimeMillis();
                                    unconfirmedReceivedIMessages = 0;
                                    timeoutT2Triggered = false;
                                    SendSMessage();
                                }	
                            }
                            else if (bytesRec == -1)
                            {
                                running = false;	
                            }
                        }
                        catch (System.IO.IOException)
                        {
                            running = false;
                        }

                        if (fileServer != null)
                            fileServer.HandleFileTransmission();

                        if (handleTimeouts() == false)
                            running = false;

                        if (running)
                        {
                            if (isActive)
                                SendWaitingASDUs();

                            Thread.Sleep(1);
                        }
                    }

                    isActive = false;

                    DebugLog("CLOSE CONNECTION!");

                    // Release the socket.

                    socket.Shutdown(SocketShutdown.Both);
                    socket.Close();

                    socketStream.Dispose();
                    socket.Dispose();

                    DebugLog("CONNECTION CLOSED!");

                }
                catch (ArgumentNullException ane)
                {
                    DebugLog("ArgumentNullException : " + ane.ToString());
                }
                catch (SocketException se)
                {
                    DebugLog("SocketException : " + se.ToString());
                }
                catch (Exception e)
                {
                    DebugLog("Unexpected exception : " + e.ToString());
                }

            }
            catch (Exception e)
            {
                DebugLog(e.ToString());
            }

            // unmark unconfirmed messages in queue if k-buffer not empty
            if (oldestSentASDU != -1)
                asduQueue.UnmarkAllASDUs();

            server.Remove(this);

            if (callbackThreadRunning)
            {
                callbackThreadRunning = false;
                callbackThread.Join();
            }

            DebugLog("Connection thread finished");
        }

        void HandleRemoteCertificateValidationCallback (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
        }

        public void Close()
        {
            running = false;
        }

        public void ASDUReadyToSend()
        {
            if (isActive)
                SendWaitingASDUs();
        }

    }
	
}
