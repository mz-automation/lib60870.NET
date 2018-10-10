// /*
//  *  Copyright 2016, 2017 MZ Automation GmbH
//  *
//  *  This file is part of lib60870.NET
//  *
//  *  lib60870.NET is free software: you can redistribute it and/or modify
//  *  it under the terms of the GNU General Public License as published by
//  *  the Free Software Foundation, either version 3 of the License, or
//  *  (at your option) any later version.
//  *
//  *  lib60870.NET is distributed in the hope that it will be useful,
//  *  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  *  GNU General Public License for more details.
//  *
//  *  You should have received a copy of the GNU General Public License
//  *  along with lib60870.NET.  If not, see <http://www.gnu.org/licenses/>.
//  *
//  *  See COPYING file for the complete license text.
//  */
//
//
using System;

using lib60870;
using lib60870.CS101;
using lib60870.CS104;
using System.Net;
using System.Threading;

namespace cs104_redundancy_server
{
    class MainClass
    {
        private static bool connectionRequestHandler(object parameter, IPAddress ipAddress)
        {
            Console.WriteLine ("New connection request from IP " + ipAddress.ToString ());

            // Allow only known IP addresses!
            // You can implement your allowed client whitelist here
            if (ipAddress.ToString ().Equals ("127.0.0.1"))
                return true;
            else if (ipAddress.ToString ().Equals ("192.168.178.70"))
                return true;
            else if (ipAddress.ToString ().Equals ("192.168.2.9"))
                return true;
            else
                return false;
        }

        private static void connectionEventHandler(object parameter, ClientConnection connection, ClientConnectionEvent conEvent)
        {
            Console.WriteLine ("Connection {0}:{1} - {2}", connection.RemoteEndpoint.Address.ToString (),
                connection.RemoteEndpoint.Port, conEvent.ToString ());
        }

        private static bool interrogationHandler(object parameter, IMasterConnection connection, ASDU asdu, byte qoi)
        {
            Console.WriteLine ("Interrogation for group " + qoi);

            ApplicationLayerParameters cp = connection.GetApplicationLayerParameters ();

            connection.SendACT_CON (asdu, false);

            // send information objects
            ASDU newAsdu = new ASDU(cp, CauseOfTransmission.INTERROGATED_BY_STATION, false, false, 2, 1, false);

            newAsdu.AddInformationObject (new MeasuredValueScaled (100, -1, new QualityDescriptor ()));

            newAsdu.AddInformationObject (new MeasuredValueScaled (101, 23, new QualityDescriptor ()));

            newAsdu.AddInformationObject (new MeasuredValueScaled (102, 2300, new QualityDescriptor ()));

            connection.SendASDU (newAsdu);

            newAsdu = new ASDU (cp, CauseOfTransmission.INTERROGATED_BY_STATION, false, false, 3, 1, false);

            newAsdu.AddInformationObject(new MeasuredValueScaledWithCP56Time2a(103, 3456, new QualityDescriptor (), new CP56Time2a(DateTime.Now)));

            connection.SendASDU (newAsdu);

            newAsdu = new ASDU (cp, CauseOfTransmission.INTERROGATED_BY_STATION, false, false, 2, 1, false);

            newAsdu.AddInformationObject (new SinglePointWithCP56Time2a (104, true, new QualityDescriptor (), new CP56Time2a (DateTime.Now)));

            connection.SendASDU (newAsdu);

            // send sequence of information objects
            newAsdu = new ASDU (cp, CauseOfTransmission.INTERROGATED_BY_STATION, false, false, 2, 1, true);

            newAsdu.AddInformationObject (new SinglePointInformation (200, true, new QualityDescriptor ()));
            newAsdu.AddInformationObject (new SinglePointInformation (201, false, new QualityDescriptor ()));
            newAsdu.AddInformationObject (new SinglePointInformation (202, true, new QualityDescriptor ()));
            newAsdu.AddInformationObject (new SinglePointInformation (203, false, new QualityDescriptor ()));
            newAsdu.AddInformationObject (new SinglePointInformation (204, true, new QualityDescriptor ()));
            newAsdu.AddInformationObject (new SinglePointInformation (205, false, new QualityDescriptor ()));
            newAsdu.AddInformationObject (new SinglePointInformation (206, true, new QualityDescriptor ()));
            newAsdu.AddInformationObject (new SinglePointInformation (207, false, new QualityDescriptor ()));

            connection.SendASDU (newAsdu);

            newAsdu = new ASDU (cp, CauseOfTransmission.INTERROGATED_BY_STATION, false, false, 2, 1, true);

            newAsdu.AddInformationObject (new MeasuredValueNormalizedWithoutQuality (300, -1.0f));
            newAsdu.AddInformationObject (new MeasuredValueNormalizedWithoutQuality (301, -0.5f));
            newAsdu.AddInformationObject (new MeasuredValueNormalizedWithoutQuality (302, -0.1f));
            newAsdu.AddInformationObject (new MeasuredValueNormalizedWithoutQuality (303, .0f));
            newAsdu.AddInformationObject (new MeasuredValueNormalizedWithoutQuality (304, 0.1f));
            newAsdu.AddInformationObject (new MeasuredValueNormalizedWithoutQuality (305, 0.2f));
            newAsdu.AddInformationObject (new MeasuredValueNormalizedWithoutQuality (306, 0.5f));
            newAsdu.AddInformationObject (new MeasuredValueNormalizedWithoutQuality (307, 0.7f));
            newAsdu.AddInformationObject (new MeasuredValueNormalizedWithoutQuality (308, 0.99f));
            newAsdu.AddInformationObject (new MeasuredValueNormalizedWithoutQuality (309, 1f));

            connection.SendASDU (newAsdu);

            connection.SendACT_TERM (asdu);

            return true;
        }

        private static bool asduHandler(object parameter, IMasterConnection connection, ASDU asdu)
        {

            if (asdu.TypeId == TypeID.C_SC_NA_1) {
                Console.WriteLine ("Single command");

                SingleCommand sc = (SingleCommand)asdu.GetElement (0);

                Console.WriteLine (sc.ToString ());
            } 
            else if (asdu.TypeId == TypeID.C_CS_NA_1){


                ClockSynchronizationCommand qsc = (ClockSynchronizationCommand)asdu.GetElement (0);

                Console.WriteLine ("Received clock sync command with time " + qsc.NewTime.ToString());
            }

            return true;
        }

        public static void Main (string[] args)
        {
            bool running = true;

            Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs e) {
                e.Cancel = true;
                running = false;
            };

            Server server = new Server ();
            //server.DebugOutput = true;

            /* Configure a server with three redundancy groups */

            server.ServerMode = ServerMode.MULTIPLE_REDUNDANCY_GROUPS;
            server.MaxQueueSize = 10;
            server.MaxOpenConnections = 6;

            RedundancyGroup redGroup1 = new RedundancyGroup("red-group-1");
            redGroup1.AddAllowedClient("192.168.2.9");

            RedundancyGroup redGroup2 = new RedundancyGroup("red-group-2");
            redGroup2.AddAllowedClient("192.168.2.223");
            redGroup2.AddAllowedClient("192.168.2.222");

            /* add a "catch all" redundancy groups - all other connections are handled by this group */
            RedundancyGroup redGroup3 = new RedundancyGroup("catch all");

            server.AddRedundancyGroup(redGroup1);
            server.AddRedundancyGroup(redGroup2);
            server.AddRedundancyGroup(redGroup3);

            server.SetConnectionRequestHandler (connectionRequestHandler, null);

            server.SetConnectionEventHandler (connectionEventHandler, null);

            server.SetInterrogationHandler (interrogationHandler, null);

            server.SetASDUHandler (asduHandler, null);

            server.Start ();

            int waitTime = 1000;

            while (running) 
            {
                Thread.Sleep(100);

                if (waitTime > 0)
                    waitTime -= 100;
                else {

                    ASDU newAsdu = new ASDU (server.GetApplicationLayerParameters(), CauseOfTransmission.PERIODIC, false, false, 2, 1, false);

                    newAsdu.AddInformationObject (new MeasuredValueScaled (110, -1, new QualityDescriptor ()));

                    server.EnqueueASDU (newAsdu);

                    waitTime = 1000;
                }
            }

            Console.WriteLine ("Stop server");
            server.Stop ();
        }
    }
}