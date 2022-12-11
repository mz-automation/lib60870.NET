using System;
using System.IO.Ports;

using lib60870;
using lib60870.CS101;
using lib60870.linklayer;
using System.Threading;

namespace cs101_master_balanced
{
    class MainClass
    {
        private static void linkLayerStateChanged (object parameter, int address, lib60870.linklayer.LinkLayerState newState)
        {
            Console.WriteLine ("LL state event: " + newState.ToString ());
        }

        private static bool asduReceivedHandler (object parameter, int address, ASDU asdu)
        {
            Console.WriteLine (asdu.ToString ());

            return true;
        }

        public static void Main (string [] args)
        {
            bool running = true;

            // use Ctrl-C to stop the programm
            Console.CancelKeyPress += delegate (object? sender, ConsoleCancelEventArgs e) {
                e.Cancel = true;
                running = false;
            };

            string portName = "/dev/ttyUSB1";

            if (args.Length > 0)
                portName = args [0];

            // Setup serial port
            SerialPort port = new SerialPort ();
            port.PortName = portName;
            port.BaudRate = 9600;
            port.Parity = Parity.Even;
            port.Handshake = Handshake.None;
            port.Open ();
            port.DiscardInBuffer ();

            // Setup balanced CS101 master
            LinkLayerParameters llParameters = new LinkLayerParameters ();
            llParameters.AddressLength = 1;
            llParameters.UseSingleCharACK = false;

            CS101Master master = new CS101Master (port, LinkLayerMode.BALANCED, llParameters);
            master.DebugOutput = false;
            master.OwnAddress = 3;
            master.SlaveAddress = 2;
            master.SetASDUReceivedHandler (asduReceivedHandler, null);
            master.SetLinkLayerStateChangedHandler (linkLayerStateChanged, null);
            master.SetReceivedRawMessageHandler ((object parameter, byte [] message, int messageSize) => {
                Console.WriteLine ("RECV " + BitConverter.ToString (message, 0, messageSize));
                return true;
            }, null);

            master.SetSentRawMessageHandler ((object parameter, byte [] message, int messageSize) => {
                Console.WriteLine ("SEND " + BitConverter.ToString (message, 0, messageSize));
                return true;
            }, null);

            long lastTimestamp = SystemUtils.currentTimeMillis ();

            // This will start a separate thread!
            // alternativley you can you master.Run() inside the loop
            master.Start ();

            while (running) {

                if ((SystemUtils.currentTimeMillis () - lastTimestamp) >= 5000) {

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

            port.Close ();
        }
    }
}
