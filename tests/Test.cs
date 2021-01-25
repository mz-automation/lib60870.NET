using NUnit.Framework;
using System;
using System.Threading;
using System.Net.Sockets;
using lib60870;
using lib60870.CS101;
using lib60870.CS104;

namespace tests
{
	class TestInteger32Object : InformationObject, IPrivateIOFactory
	{
		private int value = 0;

		public TestInteger32Object ()
			: base (0)
		{
		}

		public TestInteger32Object(int ioa, int value)
			:base (ioa)
		{
			this.value = value;
		}

		public int Value {
			get {
				return this.value;
			}
			set {
				this.value = value;
			}
		}

		private TestInteger32Object (ApplicationLayerParameters parameters, byte[] msg, int startIndex, bool isSequence)
			:base(parameters, msg, startIndex, isSequence)
		{
			if (!isSequence)
				startIndex += parameters.SizeOfIOA; /* skip IOA */

			value = msg [startIndex++];
			value += ((int)msg [startIndex++] * 0x100);
			value += ((int)msg [startIndex++] * 0x10000);
			value += ((int)msg [startIndex++] * 0x1000000);
		}

		public override bool SupportsSequence {
			get {
				return true;
			}
		}

		public override TypeID Type {
			get {
				return (TypeID)41;
			}
		}

		InformationObject IPrivateIOFactory.Decode (ApplicationLayerParameters parameters, byte[] msg, int startIndex, bool isSequence)
		{
			return new TestInteger32Object (parameters, msg, startIndex, isSequence);
		}

		public override int GetEncodedSize()
		{
			return 4;
		}

		public override void Encode(Frame frame, ApplicationLayerParameters parameters, bool isSequence) {
			base.Encode(frame, parameters, isSequence);

			frame.SetNextByte((byte) (value % 0x100));
			frame.SetNextByte((byte) ((value / 0x100) % 0x100));
			frame.SetNextByte((byte) ((value / 0x10000) % 0x100));
			frame.SetNextByte((byte) (value / 0x1000000));
		}
	}



    [TestFixture ()]
    public class Test
    {
        [Test ()]
        public void TestStatusAndStatusChangedDetection ()
        {
            StatusAndStatusChangeDetection scd = new StatusAndStatusChangeDetection ();

            Assert.AreEqual (false, scd.ST (0));
            Assert.AreEqual (false, scd.ST (15));
            Assert.AreEqual (false, scd.CD (0));
            Assert.AreEqual (false, scd.CD (15));

            Assert.AreEqual (false, scd.CD (1));

            scd.CD (0, true);

            Assert.AreEqual (true, scd.CD (0));
            Assert.AreEqual (false, scd.CD (1));

            scd.CD (15, true);

            Assert.AreEqual (true, scd.CD (15));
            Assert.AreEqual (false, scd.CD (14));
        }

        [Test ()]
        public void TestBCR ()
        {
            BinaryCounterReading bcr = new BinaryCounterReading ();

            bcr.Value = 1000;

            Assert.AreEqual (1000, bcr.Value);

            bcr.Value = -1000;

            Assert.AreEqual (-1000, bcr.Value);

            bcr.SequenceNumber = 31;

            Assert.AreEqual (31, bcr.SequenceNumber);

            bcr.SequenceNumber = 0;

            Assert.AreEqual (0, bcr.SequenceNumber);

            /* Out of range sequenceNumber */
            bcr.SequenceNumber = 32;

            Assert.AreEqual (0, bcr.SequenceNumber);

            bcr = new BinaryCounterReading ();

            bcr.Invalid = true;

            Assert.AreEqual (true, bcr.Invalid);
            Assert.AreEqual (false, bcr.Carry);
            Assert.AreEqual (false, bcr.Adjusted);
            Assert.AreEqual (0, bcr.SequenceNumber);
            Assert.AreEqual (0, bcr.Value);

            bcr = new BinaryCounterReading ();

            bcr.Carry = true;

            Assert.AreEqual (false, bcr.Invalid);
            Assert.AreEqual (true, bcr.Carry);
            Assert.AreEqual (false, bcr.Adjusted);
            Assert.AreEqual (0, bcr.SequenceNumber);
            Assert.AreEqual (0, bcr.Value);

            bcr = new BinaryCounterReading ();

            bcr.Adjusted = true;

            Assert.AreEqual (false, bcr.Invalid);
            Assert.AreEqual (false, bcr.Carry);
            Assert.AreEqual (true, bcr.Adjusted);
            Assert.AreEqual (0, bcr.SequenceNumber);
            Assert.AreEqual (0, bcr.Value);


        }

        [Test ()]
        public void TestScaledValue ()
        {
            ScaledValue scaledValue = new ScaledValue (0);

            Assert.AreEqual (0, scaledValue.Value);
            Assert.AreEqual ((short)0, scaledValue.ShortValue);

            scaledValue = new ScaledValue (32767);
            Assert.AreEqual (32767, scaledValue.Value);
            Assert.AreEqual ((short)32767, scaledValue.ShortValue);

            scaledValue = new ScaledValue (32768);
            Assert.AreEqual (32767, scaledValue.Value);
            Assert.AreEqual ((short)32767, scaledValue.ShortValue);

            scaledValue = new ScaledValue (-32768);
            Assert.AreEqual (-32768, scaledValue.Value);
            Assert.AreEqual ((short)-32768, scaledValue.ShortValue);

            scaledValue = new ScaledValue (-32769);
            Assert.AreEqual (-32768, scaledValue.Value);
            Assert.AreEqual ((short)-32768, scaledValue.ShortValue);

            scaledValue = new ScaledValue(-1);
            Assert.AreEqual(-1, scaledValue.Value);

            scaledValue = new ScaledValue(-300);
            Assert.AreEqual(-300, scaledValue.Value);
        }

        [Test ()]
        public void TestSetpointCommandNormalized ()
        {
            SetpointCommandNormalized sc = new SetpointCommandNormalized (102, -0.5f,
                new SetpointCommandQualifier (true, 0));

            Assert.AreEqual (102, sc.ObjectAddress);

            Assert.AreEqual (-0.5f, sc.NormalizedValue, 0.001f);

            Assert.AreEqual (true, sc.QOS.Select);

            sc = new SetpointCommandNormalized (102, 32767, new SetpointCommandQualifier (true, 0));

            Assert.AreEqual (1.0, sc.NormalizedValue, 0.001f);

            Assert.AreEqual (32767, sc.RawValue);

            sc = new SetpointCommandNormalized (102, -32768, new SetpointCommandQualifier (true, 0));

            Assert.AreEqual (-1.0, sc.NormalizedValue, 0.001f);

            Assert.AreEqual (-32768, sc.RawValue);
        }

        [Test ()]
        public void TestStepPositionInformation ()
        {
            StepPositionInformation spi = new StepPositionInformation (103, 27, false, new QualityDescriptor ());

            Assert.IsFalse (spi.Transient);
            Assert.NotNull (spi.Quality);

            spi = null;

            try {
                spi = new StepPositionInformation (103, 64, false, new QualityDescriptor ());
            } catch (ArgumentOutOfRangeException) {
            }

            Assert.IsNull (spi);

            try {
                spi = new StepPositionInformation (103, -65, false, new QualityDescriptor ());
            } catch (ArgumentOutOfRangeException) {
            }
        }

        [Test ()]
        //[Ignore("Ignore to save execution time")]
        public void TestConnectWhileAlreadyConnected ()
        {
            ApplicationLayerParameters parameters = new ApplicationLayerParameters ();
            APCIParameters apciParameters = new APCIParameters ();

            Server server = new Server (apciParameters, parameters);

            server.SetLocalPort (20213);

            server.Start ();

            Connection connection = new Connection ("127.0.0.1", 20213, apciParameters, parameters);

            ConnectionException se = null;

            try {
                connection.Connect ();
            } catch (ConnectionException ex) {
                se = ex;
            }

            Assert.IsNull (se);

            Thread.Sleep (100);

            try {
                connection.Connect ();
            } catch (ConnectionException ex) {
                se = ex;
            }

            Assert.IsNotNull (se);
            Assert.AreEqual (se.Message, "already connected");
            Assert.AreEqual (10056, ((SocketException)se.InnerException).ErrorCode);

            connection.Close ();

            server.Stop ();
        }

        [Test()]
        //[Ignore("Ignore to save execution time")]
        public void TestSendIMessageAfterStopDT()
        {
            ApplicationLayerParameters parameters = new ApplicationLayerParameters();
            APCIParameters apciParameters = new APCIParameters();

            Server server = new Server(apciParameters, parameters);

            server.SetLocalPort(20213);

            server.Start();

            Connection connection = new Connection("127.0.0.1", 20213, apciParameters, parameters);

            ConnectionException se = null;

            try
            {
                connection.Connect();

                connection.SendStartDT();

                Thread.Sleep(200);

                connection.SendStopDT();

                // send command (should trigger server disconnect)
                connection.SendControlCommand(CauseOfTransmission.ACTIVATION, 1, new SingleCommand(5000, true, false, 0));

                Thread.Sleep(500);

                // send command (should throw exception - not connected)
                connection.SendControlCommand(CauseOfTransmission.ACTIVATION, 1, new SingleCommand(5000, true, false, 0));
            }
            catch (ConnectionException ex)
            {
                se = ex;
            }

            Assert.IsNotNull(se);
            Assert.AreEqual(se.Message, "not connected");
            Assert.AreEqual(10057, ((SocketException)se.InnerException).ErrorCode);

            server.Stop();
        }

        [Test ()]
        //[Ignore("Ignore to save execution time")]
        public void TestConnectSameConnectionMultipleTimes ()
        {
            ApplicationLayerParameters parameters = new ApplicationLayerParameters ();
            APCIParameters apciParameters = new APCIParameters ();

            Server server = new Server (apciParameters, parameters);

            server.SetLocalPort (20213);

            server.Start ();

            Connection connection = new Connection ("127.0.0.1", 20213, apciParameters, parameters);

            SocketException se = null;

            try {
                connection.Connect ();

                connection.Close ();
            } catch (SocketException ex) {
                se = ex;
            }

            Assert.IsNull (se);

            try {
                connection.Connect ();

                connection.Close ();
            } catch (SocketException ex) {
                se = ex;
            }

            Assert.Null (se);

            connection.Close ();

            server.Stop ();
        }

        [Test()]
        //[Ignore("Ignore to save execution time")]
        public void TestConnectSameConnectionMultipleTimesServerDisconnects()
        {
            ApplicationLayerParameters parameters = new ApplicationLayerParameters();
            APCIParameters apciParameters = new APCIParameters();

            Server server = new Server(apciParameters, parameters);

            server.SetLocalPort(20213);

            server.Start();

            Connection connection = new Connection("127.0.0.1", 20213, apciParameters, parameters);

            for (int i = 0; i < 3; i++)
            {
                ConnectionException se = null;

                connection.Connect();

                server.Stop();

                Thread.Sleep(1000);

                try
                {
                    connection.SendStartDT();

                    connection.Close();
                }
                catch (ConnectionException ex)
                {
                    se = ex;
                }

                Assert.IsNotNull(se);

                server.Start();
            }

            server.Stop();

            connection.Close();
        }

        [Test ()]
        public void TestASDUAddInformationObjects () {
            ApplicationLayerParameters cp = new ApplicationLayerParameters ();

            ASDU asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);

            asdu.AddInformationObject (new SinglePointInformation (100, false, new QualityDescriptor ()));
            asdu.AddInformationObject (new SinglePointInformation (101, false, new QualityDescriptor ()));

            // wrong InformationObject type expect exception
            ArgumentException ae = null;

            try {
                asdu.AddInformationObject (new DoublePointInformation (102, DoublePointValue.ON, new QualityDescriptor ()));
            } catch (ArgumentException e) {
                ae = e;
            }

            Assert.NotNull (ae);
        }

        [Test ()]
        public void TestASDUAddTooMuchInformationObjects () {
            ApplicationLayerParameters cp = new ApplicationLayerParameters ();

            ASDU asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);

            int addedCounter = 0;
            int ioa = 100;

            while (asdu.AddInformationObject (new SinglePointInformation (ioa, false, new QualityDescriptor ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (60, addedCounter);

            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, true);

            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new SinglePointInformation (ioa, false, new QualityDescriptor ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (127, addedCounter);

            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);

            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new SinglePointWithCP24Time2a (ioa, false, new QualityDescriptor (), new CP24Time2a ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (34, addedCounter);

            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, true);

            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new SinglePointWithCP56Time2a (ioa, false, new QualityDescriptor (), new CP56Time2a ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (30, addedCounter);

            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);

            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new MeasuredValueShortWithCP56Time2a (ioa, 0.0f, QualityDescriptor.VALID (), new CP56Time2a ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (16, addedCounter);
        }

        [Test ()]
        public void TestASDUAddInformationObjectsInWrongOrderToSequence () {
            ApplicationLayerParameters cp = new ApplicationLayerParameters ();

            ASDU asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, true);

            bool encoded = asdu.AddInformationObject (new SinglePointInformation (100, false, new QualityDescriptor ()));

            Assert.IsTrue (encoded);

            encoded = asdu.AddInformationObject (new SinglePointInformation (101, false, new QualityDescriptor ()));

            Assert.IsTrue (encoded);

            encoded = asdu.AddInformationObject (new SinglePointInformation (102, false, new QualityDescriptor ()));

            Assert.IsTrue (encoded);

            encoded = asdu.AddInformationObject (new SinglePointInformation (104, false, new QualityDescriptor ()));

            Assert.IsFalse (encoded);

            encoded = asdu.AddInformationObject (new SinglePointInformation (102, false, new QualityDescriptor ()));

            Assert.IsFalse (encoded);

            Assert.AreEqual (3, asdu.NumberOfElements);
        }



        [Test ()]
        public void TestEncodeASDUsWithManyInformationObjects () {
            ApplicationLayerParameters cp = new ApplicationLayerParameters ();

            ASDU asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            int addedCounter = 0;
            int ioa = 100;

            while (asdu.AddInformationObject (new SinglePointInformation (ioa, false, new QualityDescriptor ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (60, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());


            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new SinglePointWithCP24Time2a (ioa, true, new QualityDescriptor (), new CP24Time2a ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (34, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());


            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new SinglePointWithCP56Time2a (ioa, true, new QualityDescriptor (), new CP56Time2a ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (22, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());


            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new DoublePointInformation (ioa, DoublePointValue.ON, new QualityDescriptor ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (60, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());


            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new DoublePointWithCP24Time2a (ioa, DoublePointValue.ON, new QualityDescriptor (), new CP24Time2a ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (34, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());


            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new DoublePointWithCP56Time2a (ioa, DoublePointValue.ON, new QualityDescriptor (), new CP56Time2a ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (22, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());


            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new MeasuredValueNormalized (ioa, 1f, new QualityDescriptor ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (40, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());


            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new MeasuredValueNormalizedWithCP24Time2a (ioa, 1f, new QualityDescriptor (), new CP24Time2a ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (27, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());


            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new MeasuredValueNormalizedWithCP56Time2a (ioa, 1f, new QualityDescriptor (), new CP56Time2a ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (18, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());


            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new MeasuredValueScaled (ioa, 0, new QualityDescriptor ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (40, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());


            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new MeasuredValueScaledWithCP24Time2a (ioa, 0, new QualityDescriptor (), new CP24Time2a ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (27, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());


            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new MeasuredValueScaledWithCP56Time2a (ioa, 0, new QualityDescriptor (), new CP56Time2a ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (18, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());


            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new MeasuredValueShort (ioa, 0f, new QualityDescriptor ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (30, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());


            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new MeasuredValueShortWithCP24Time2a (ioa, 0f, new QualityDescriptor (), new CP24Time2a ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (22, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());


            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new MeasuredValueShortWithCP56Time2a (ioa, 0f, new QualityDescriptor (), new CP56Time2a ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (16, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());


            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new StepPositionInformation (ioa, 0, false, new QualityDescriptor ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (48, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());


            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new StepPositionWithCP24Time2a (ioa, 0, false, new QualityDescriptor (), new CP24Time2a ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (30, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());


            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new StepPositionWithCP56Time2a (ioa, 0, false, new QualityDescriptor (), new CP56Time2a ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (20, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());


            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new IntegratedTotals (ioa, new BinaryCounterReading ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (30, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());


            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new IntegratedTotalsWithCP24Time2a (ioa, new BinaryCounterReading (), new CP24Time2a ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (22, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());


            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new IntegratedTotalsWithCP56Time2a (ioa, new BinaryCounterReading (), new CP56Time2a ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (16, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());

            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new EventOfProtectionEquipment (ioa, new SingleEvent (), new CP16Time2a (10), new CP24Time2a ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (27, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());


            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new EventOfProtectionEquipmentWithCP56Time2a (ioa, new SingleEvent (), new CP16Time2a (10), new CP56Time2a ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (18, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());


            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new PackedSinglePointWithSCD (ioa, new StatusAndStatusChangeDetection (), new QualityDescriptor ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (30, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());

            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new PackedOutputCircuitInfo (ioa, new OutputCircuitInfo (), new QualityDescriptorP (), new CP16Time2a (10), new CP24Time2a ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (24, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());

            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new PackedOutputCircuitInfoWithCP56Time2a (ioa, new OutputCircuitInfo (), new QualityDescriptorP (), new CP16Time2a (10), new CP56Time2a ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (17, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());

            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new PackedStartEventsOfProtectionEquipment (ioa, new StartEvent (), new QualityDescriptorP (), new CP16Time2a (10), new CP24Time2a ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (24, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());


            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new PackedStartEventsOfProtectionEquipmentWithCP56Time2a (ioa, new StartEvent (), new QualityDescriptorP (), new CP16Time2a (10), new CP56Time2a ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (17, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());
            //TODO add missing tests
        }

        [Test ()]
        public void TestEncodeASDUsWithManyInformationObjectsSequenceOfIO () {

            ApplicationLayerParameters cp = new ApplicationLayerParameters ();

            ASDU asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, true);
            int addedCounter = 0;
            int ioa = 100;

            while (asdu.AddInformationObject (new SinglePointInformation (ioa, false, new QualityDescriptor ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (127, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());


            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, true);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new DoublePointInformation (ioa, DoublePointValue.OFF, new QualityDescriptor ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (127, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());


            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, true);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new MeasuredValueNormalized (ioa, 1f, new QualityDescriptor ()))) {
                ioa++;
                addedCounter++;
            }


            Assert.AreEqual (80, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());

            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, true);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new MeasuredValueScaled (ioa, 0, new QualityDescriptor ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (80, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());


            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, true);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new MeasuredValueShort (ioa, 0f, new QualityDescriptor ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (48, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());


            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, true);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new StepPositionInformation (ioa, 0, false, new QualityDescriptor ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (120, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());


            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, true);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new IntegratedTotals (ioa, new BinaryCounterReading ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (48, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());


            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, true);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new PackedSinglePointWithSCD (ioa, new StatusAndStatusChangeDetection (), new QualityDescriptor ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (48, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());


            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, true);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new PackedOutputCircuitInfo (ioa, new OutputCircuitInfo (), new QualityDescriptorP (), new CP16Time2a (10), new CP24Time2a ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (34, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());

            asdu = new ASDU (cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, true);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject (new PackedStartEventsOfProtectionEquipment (ioa, new StartEvent (), new QualityDescriptorP (), new CP16Time2a (0), new CP24Time2a ()))) {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual (34, addedCounter);
            Assert.NotNull (asdu.AsByteArray ());

        }

        [Test ()]
        //[Ignore("Ignore to save execution time")]
        public void TestSendTestFR () {
            ApplicationLayerParameters clientParameters = new ApplicationLayerParameters ();
            APCIParameters clientApciParamters = new APCIParameters ();
            ApplicationLayerParameters serverParameters = new ApplicationLayerParameters ();
            APCIParameters serverApciParamters = new APCIParameters ();

            clientApciParamters.T3 = 1;

            Server server = new Server (serverApciParamters, serverParameters);

            server.SetLocalPort (20213);

            server.Start ();

            Connection connection = new Connection ("127.0.0.1", 20213, clientApciParamters, clientParameters);

            connection.Connect ();

            ASDU asdu = new ASDU (clientParameters, CauseOfTransmission.SPONTANEOUS, false, false, 0, 1, false);
            asdu.AddInformationObject (new SinglePointInformation (100, false, new QualityDescriptor ()));

            connection.SendASDU (asdu);

            Assert.AreEqual (2, connection.GetStatistics ().SentMsgCounter); /* STARTDT + ASDU */

            while (connection.GetStatistics ().RcvdMsgCounter < 2)
                Thread.Sleep (1);

            Assert.AreEqual (2, connection.GetStatistics ().RcvdMsgCounter); /* STARTDT_CON + ASDU */

            Thread.Sleep (2500);

            connection.Close ();
            server.Stop ();

            Assert.AreEqual (4, connection.GetStatistics ().RcvdMsgCounter); /* STARTDT_CON + ASDU + TESTFR_CON */

            Assert.AreEqual (2, connection.GetStatistics ().RcvdTestFrConCounter);
        }


        private static bool testSendTestFRTimeoutMasterRawMessageHandler (object param, byte [] msg, int msgSize)
        {
            // intercept TESTFR_CON message
            if ((msgSize == 6) && (msg [2] == 0x83))
                return false;
            else
                return true;
        }

        /// <summary>
        /// This test checks that the connection will be closed when the master
        /// doesn't receive the TESTFR_CON messages
        /// </summary>
        [Test ()]
        //[Ignore("Ignore to save execution time")]
        public void TestSendTestFRTimeoutMaster () {
            ApplicationLayerParameters clientParameters = new ApplicationLayerParameters ();
            APCIParameters clientApciParamters = new APCIParameters ();
            ApplicationLayerParameters serverParameters = new ApplicationLayerParameters ();
            APCIParameters serverApciParamters = new APCIParameters ();

            clientApciParamters.T3 = 1;

            Server server = new Server (serverApciParamters, serverParameters);

            server.SetLocalPort (20213);

            server.Start ();

            Connection connection = new Connection ("127.0.0.1", 20213, clientApciParamters, clientParameters);

            connection.Connect ();

            connection.SetReceivedRawMessageHandler (testSendTestFRTimeoutMasterRawMessageHandler, null);

            ASDU asdu = new ASDU (clientParameters, CauseOfTransmission.SPONTANEOUS, false, false, 0, 1, false);
            asdu.AddInformationObject (new SinglePointInformation (100, false, new QualityDescriptor ()));

            connection.SendASDU (asdu);

            Assert.AreEqual (2, connection.GetStatistics ().SentMsgCounter); /* STARTDT + ASDU */

            while (connection.GetStatistics ().RcvdMsgCounter < 2)
                Thread.Sleep (1);

            Assert.AreEqual (2, connection.GetStatistics ().RcvdMsgCounter); /* STARTDT_CON + ASDU */

            Thread.Sleep (6000);

            // Expect connection to be closed due to three missing TESTFR_CON responses
            Assert.IsFalse (connection.IsRunning);

            ConnectionException ce = null;

            // Connection is closed. SendASDU should fail
            try {
                connection.SendASDU (asdu);
            } catch (ConnectionException e) {
                ce = e;
            }

            Assert.IsNotNull (ce);
            Assert.AreEqual ("not connected", ce.Message);

            connection.Close ();
            server.Stop ();

            Assert.AreEqual (5, connection.GetStatistics ().RcvdMsgCounter); /* STARTDT_CON + ASDU + TESTFR_CON */

            Assert.AreEqual (0, connection.GetStatistics ().RcvdTestFrConCounter);
        }

        private static bool testSendTestFRTimeoutSlaveRawMessageHandler (object param, byte [] msg, int msgSize)
        {
            // intercept TESTFR_ACT messages for so that the master doesn't response
            if ((msgSize == 6) && (msg [2] == 0x43))
                return false;
            else
                return true;
        }

        /// <summary>
        /// This test checks that the connection will be closed when the master
        /// doesn't send the TESTFR_CON messages
        /// </summary>
        [Test ()]
        //[Ignore("Ignore to save execution time")]
        public void TestSendTestFRTimeoutSlave () {
            ApplicationLayerParameters clientParameters = new ApplicationLayerParameters ();
            APCIParameters clientApciParamters = new APCIParameters ();
            ApplicationLayerParameters serverParameters = new ApplicationLayerParameters ();
            APCIParameters serverApciParamters = new APCIParameters ();

            serverApciParamters.T3 = 1;

            Server server = new Server (serverApciParamters, serverParameters);

            server.SetLocalPort (20213);

            server.Start ();

            Connection connection = new Connection ("127.0.0.1", 20213, clientApciParamters, clientParameters);

            connection.DebugOutput = true;
            connection.SetReceivedRawMessageHandler (testSendTestFRTimeoutSlaveRawMessageHandler, null);

            connection.Connect ();

            Assert.AreEqual (1, connection.GetStatistics ().SentMsgCounter); /* STARTDT */

            while (connection.GetStatistics ().RcvdMsgCounter < 1)
                Thread.Sleep (1);

            Assert.AreEqual (1, connection.GetStatistics ().RcvdMsgCounter); /* STARTDT_CON */

            Thread.Sleep (6000);


            // Connection is closed. SendASDU should fail
            try {
                ASDU asdu = new ASDU (clientParameters, CauseOfTransmission.SPONTANEOUS, false, false, 0, 1, false);
                asdu.AddInformationObject (new SinglePointInformation (100, false, new QualityDescriptor ()));

                connection.SendASDU (asdu);
            } catch (ConnectionException) {
            }


            while (connection.IsRunning == true)
                Thread.Sleep (10);

            connection.Close ();
            server.Stop ();

            //	Assert.AreEqual (5, connection.GetStatistics ().RcvdMsgCounter); /* STARTDT_CON + ASDU + TESTFR_CON */

            //	Assert.AreEqual (0, connection.GetStatistics ().RcvdTestFrConCounter);
        }


        [Test ()]
        public void TestEncodeDecodeSetpointCommandNormalized () {
            Server server = new Server ();
            server.SetLocalPort (20213);

            float recvValue = 0f;
            float sendValue = 1.0f;
            bool hasReceived = false;

            server.SetASDUHandler (delegate (object parameter, IMasterConnection con, ASDU asdu) {

                if (asdu.TypeId == TypeID.C_SE_NA_1) {
                    SetpointCommandNormalized spn = (SetpointCommandNormalized)asdu.GetElement (0);

                    recvValue = spn.NormalizedValue;
                    hasReceived = true;
                }

                return true;
            }, null);
            server.Start ();

            Connection connection = new Connection ("127.0.0.1", 20213);
            connection.Connect ();

            ASDU newAsdu = new ASDU (server.GetApplicationLayerParameters (), CauseOfTransmission.ACTIVATION, false, false, 0, 1, false);
            newAsdu.AddInformationObject (new SetpointCommandNormalized (100, sendValue, new SetpointCommandQualifier (false, 0)));

            connection.SendASDU (newAsdu);

            while (hasReceived == false)
                Thread.Sleep (50);

            connection.Close ();
            server.Stop ();

            Assert.AreEqual (sendValue, recvValue, 0.001);
        }

        [Test ()]
        public void TestEncodeDecodePrivateInformationObject ()
        {
            Server server = new Server ();
            server.SetLocalPort (20213);
            server.DebugOutput = true;

            int recvValue = 0;
            int sendValue = 12345;
            bool hasReceived = false;

            PrivateInformationObjectTypes privateObjects = new PrivateInformationObjectTypes ();
            privateObjects.AddPrivateInformationObjectType ((TypeID)41, new TestInteger32Object ());

            server.SetASDUHandler (delegate (object parameter, IMasterConnection con, ASDU asdu) {

                if (asdu.TypeId == (TypeID)41) {

                    TestInteger32Object spn = (TestInteger32Object)asdu.GetElement (0, privateObjects);

                    recvValue = spn.Value;
                    hasReceived = true;
                }

                return true;
            }, null);

            server.Start ();

            Connection connection = new Connection ("127.0.0.1", 20213);
            connection.Connect ();

            ASDU newAsdu = new ASDU (server.GetApplicationLayerParameters (), CauseOfTransmission.ACTIVATION, false, false, 0, 1, false);

            newAsdu.AddInformationObject (new TestInteger32Object (100, sendValue));

            connection.SendASDU (newAsdu);

            while (hasReceived == false)
                Thread.Sleep (50);

            connection.Close ();
            server.Stop ();

            Assert.AreEqual (sendValue, recvValue);

        }

        [Test ()]
        public void TestSingleCommand ()
        {
            SingleCommand sc = new SingleCommand (10002, true, false, 12);

            Assert.AreEqual (10002, sc.ObjectAddress);
            Assert.AreEqual (true, sc.State);
            Assert.AreEqual (false, sc.Select);
            Assert.AreEqual (12, sc.QU);

            sc = new SingleCommand (10002, false, true, 3);

            Assert.AreEqual (10002, sc.ObjectAddress);
            Assert.AreEqual (false, sc.State);
            Assert.AreEqual (true, sc.Select);
            Assert.AreEqual (3, sc.QU);

            sc.QU = 17;

            Assert.AreEqual (17, sc.QU);
            Assert.AreEqual (false, sc.State);
            Assert.AreEqual (true, sc.Select);
        }

        [Test ()]
        public void TestSinglePointInformationClientServer ()
        {
            SinglePointInformation spi = new SinglePointInformation (101, true, new QualityDescriptor ());

            ASDU newAsdu = new ASDU (new ApplicationLayerParameters (), CauseOfTransmission.PERIODIC,
                false, false, 0, 1, false);

            newAsdu.AddInformationObject (spi);

            Server server = new Server ();
            server.SetLocalPort (20213);

            bool hasReceived = false;

            server.SetASDUHandler (delegate (object parameter, IMasterConnection con, ASDU asdu) {

                if (asdu.TypeId == TypeID.M_SP_NA_1) {

                    SinglePointInformation spn = (SinglePointInformation)asdu.GetElement (0);

                    Assert.AreEqual (spi.Value, spn.Value);
                    hasReceived = true;
                }

                return true;
            }, null);

            server.Start ();

            Connection connection = new Connection ("127.0.0.1", 20213);
            connection.Connect ();

            connection.SendASDU (newAsdu);

            while (hasReceived == false)
                Thread.Sleep (50);

            connection.Close ();
            server.Stop ();
        }

        [Test ()]
        public void TestSinglePointInformation ()
        {
            byte [] buffer = new byte [257];

            BufferFrame bf = new BufferFrame (buffer, 0);

            ApplicationLayerParameters alParameters = new ApplicationLayerParameters ();

            SinglePointInformation spi = new SinglePointInformation (101, true, new QualityDescriptor ());

            spi.Encode (bf, alParameters, true);
            Assert.AreEqual (1, bf.GetMsgSize ());

            bf.ResetFrame ();

            spi.Encode (bf, alParameters, false);

            Assert.AreEqual (alParameters.SizeOfIOA + spi.GetEncodedSize (), bf.GetMsgSize ());
            Assert.AreEqual (4, bf.GetMsgSize ());

            SinglePointInformation spi2 = new SinglePointInformation (alParameters, buffer, 0, false);

            Assert.AreEqual (101, spi2.ObjectAddress);
            Assert.AreEqual (true, spi2.Value);
        }

        [Test ()]
        public void TestSinglePointInformationWithCp24Time2a ()
        {
            byte [] buffer = new byte [257];

            BufferFrame bf = new BufferFrame (buffer, 0);

            ApplicationLayerParameters alParameters = new ApplicationLayerParameters ();

            CP24Time2a time = new CP24Time2a (45, 23, 538);

            SinglePointWithCP24Time2a spi = new SinglePointWithCP24Time2a (102, false, new QualityDescriptor (), time);

            spi.Encode (bf, alParameters, false);

            Assert.AreEqual (alParameters.SizeOfIOA + spi.GetEncodedSize (), bf.GetMsgSize ());
            Assert.AreEqual (7, bf.GetMsgSize ());

            SinglePointWithCP24Time2a spi2 = new SinglePointWithCP24Time2a (alParameters, buffer, 0, false);

            Assert.AreEqual (102, spi2.ObjectAddress);
            Assert.AreEqual (false, spi2.Value);
            Assert.AreEqual (45, spi2.Timestamp.Minute);
            Assert.AreEqual (23, spi2.Timestamp.Second);
            Assert.AreEqual (538, spi2.Timestamp.Millisecond);
        }

        [Test ()]
        public void TestSinglePointInformationWithCP56Time2a ()
        {
            byte [] buffer = new byte [257];

            BufferFrame bf = new BufferFrame (buffer, 0);

            ApplicationLayerParameters alParameters = new ApplicationLayerParameters ();

            DateTime dateTime = DateTime.UtcNow;

            CP56Time2a time = new CP56Time2a (dateTime);

            SinglePointWithCP56Time2a spi = new SinglePointWithCP56Time2a (103, true, new QualityDescriptor (), time);

            spi.Encode (bf, alParameters, false);

            Assert.AreEqual (alParameters.SizeOfIOA + spi.GetEncodedSize (), bf.GetMsgSize ());
            Assert.AreEqual (11, bf.GetMsgSize ());

            SinglePointWithCP56Time2a spi2 = new SinglePointWithCP56Time2a (alParameters, buffer, 0, false);

            Assert.AreEqual (103, spi2.ObjectAddress);
            Assert.AreEqual (true, spi2.Value);

            Assert.AreEqual (time.Year, spi2.Timestamp.Year);
            Assert.AreEqual (time.Month, spi2.Timestamp.Month);
            Assert.AreEqual (time.DayOfMonth, spi2.Timestamp.DayOfMonth);
            Assert.AreEqual (time.Minute, spi2.Timestamp.Minute);
            Assert.AreEqual (time.Second, spi2.Timestamp.Second);
            Assert.AreEqual (time.Millisecond, spi2.Timestamp.Millisecond);
        }

        [Test ()]
        public void TestDoublePointInformation ()
        {
            byte [] buffer = new byte [257];

            BufferFrame bf = new BufferFrame (buffer, 0);

            ApplicationLayerParameters alParameters = new ApplicationLayerParameters ();

            DoublePointInformation dpi = new DoublePointInformation (101, DoublePointValue.OFF, new QualityDescriptor ());

            dpi.Encode (bf, alParameters, true);
            Assert.AreEqual (1, bf.GetMsgSize ());

            bf.ResetFrame ();

            dpi.Encode (bf, alParameters, false);

            Assert.AreEqual (alParameters.SizeOfIOA + dpi.GetEncodedSize (), bf.GetMsgSize ());
            Assert.AreEqual (4, bf.GetMsgSize ());

            DoublePointInformation dpi2 = new DoublePointInformation (alParameters, buffer, 0, false);

            Assert.AreEqual (101, dpi2.ObjectAddress);
            Assert.AreEqual (DoublePointValue.OFF, dpi2.Value);
        }

        [Test ()]
        public void TestDoublePointInformationWithCP24Time2a ()
        {
            byte [] buffer = new byte [257];

            BufferFrame bf = new BufferFrame (buffer, 0);

            ApplicationLayerParameters alParameters = new ApplicationLayerParameters ();

            CP24Time2a time = new CP24Time2a (45, 23, 538);

            DoublePointWithCP24Time2a dpi = new DoublePointWithCP24Time2a (101, DoublePointValue.ON, new QualityDescriptor (), time);

            dpi.Encode (bf, alParameters, true);
            Assert.AreEqual (4, bf.GetMsgSize ());

            bf.ResetFrame ();

            dpi.Encode (bf, alParameters, false);

            Assert.AreEqual (alParameters.SizeOfIOA + dpi.GetEncodedSize (), bf.GetMsgSize ());
            Assert.AreEqual (7, bf.GetMsgSize ());

            DoublePointWithCP24Time2a dpi2 = new DoublePointWithCP24Time2a (alParameters, buffer, 0, false);

            Assert.AreEqual (101, dpi2.ObjectAddress);
            Assert.AreEqual (DoublePointValue.ON, dpi2.Value);
            Assert.AreEqual (45, dpi2.Timestamp.Minute);
            Assert.AreEqual (23, dpi2.Timestamp.Second);
            Assert.AreEqual (538, dpi2.Timestamp.Millisecond);
        }

        [Test ()]
        public void TestDoublePointInformationWithCP56Time2a ()
        {
            byte [] buffer = new byte [257];

            BufferFrame bf = new BufferFrame (buffer, 0);

            ApplicationLayerParameters alParameters = new ApplicationLayerParameters ();

            DateTime dateTime = DateTime.UtcNow;

            CP56Time2a time = new CP56Time2a (dateTime);

            DoublePointWithCP56Time2a dpi = new DoublePointWithCP56Time2a (101, DoublePointValue.INTERMEDIATE, new QualityDescriptor (), time);

            dpi.Encode (bf, alParameters, true);
            Assert.AreEqual (8, bf.GetMsgSize ());

            bf.ResetFrame ();

            dpi.Encode (bf, alParameters, false);

            Assert.AreEqual (alParameters.SizeOfIOA + dpi.GetEncodedSize (), bf.GetMsgSize ());
            Assert.AreEqual (11, bf.GetMsgSize ());

            DoublePointWithCP56Time2a dpi2 = new DoublePointWithCP56Time2a (alParameters, buffer, 0, false);

            Assert.AreEqual (101, dpi2.ObjectAddress);
            Assert.AreEqual (DoublePointValue.INTERMEDIATE, dpi2.Value);
            Assert.AreEqual (time.Year, dpi2.Timestamp.Year);
            Assert.AreEqual (time.Month, dpi2.Timestamp.Month);
            Assert.AreEqual (time.DayOfMonth, dpi2.Timestamp.DayOfMonth);
            Assert.AreEqual (time.Minute, dpi2.Timestamp.Minute);
            Assert.AreEqual (time.Second, dpi2.Timestamp.Second);
            Assert.AreEqual (time.Millisecond, dpi2.Timestamp.Millisecond);
        }

        [Test ()]
        public void TestCP56Time2a ()
        {
            CP56Time2a time = new CP56Time2a ();

            Assert.AreEqual (time.Year, 0);

            time.Year = 2017;

            Assert.AreEqual (time.Year, 17);

            time.Year = 1980;

            Assert.AreEqual (time.Year, 80);
        }

        [Test ()]
        public void TestMeasuredValueNormalized ()
        {
            byte [] buffer = new byte [257];

            BufferFrame bf = new BufferFrame (buffer, 0);

            ApplicationLayerParameters alParameters = new ApplicationLayerParameters ();

            MeasuredValueNormalized mvn = new MeasuredValueNormalized (201, 0.5f, new QualityDescriptor ());

            mvn.Encode (bf, alParameters, true);
            Assert.AreEqual (3, bf.GetMsgSize ());

            bf.ResetFrame ();

            mvn.Encode (bf, alParameters, false);
            Assert.AreEqual (alParameters.SizeOfIOA + mvn.GetEncodedSize (), bf.GetMsgSize ());

            MeasuredValueNormalized mvn2 = new MeasuredValueNormalized (alParameters, buffer, 0, false);

            Assert.AreEqual (201, mvn2.ObjectAddress);
            Assert.AreEqual (0.5f, mvn2.NormalizedValue, 0.001);
        }

        public class SimpleFile : TransparentFile
        {
            public SimpleFile (int ca, int ioa, NameOfFile nof)
                : base (ca, ioa, nof)
            {
            }

            public bool transferComplete = false;
            public bool success = false;

            public override void TransferComplete (bool success)
            {
                Console.WriteLine ("Transfer complete: " + success.ToString ());
                transferComplete = true;
                this.success = success;
            }
        }

        public class Receiver : IFileReceiver
        {
            public bool finishedCalled = false;

            public byte[] recvBuffer = new byte [10000];
            public int recvdBytes = 0;
            public byte lastSection = 0;

            public void Finished (FileErrorCode result)
            {
                Console.WriteLine ("File download finished - code: " + result.ToString ());
                finishedCalled = true;
            }


            public void SegmentReceived (byte sectionName, int offset, int size, byte [] data)
            {
                lastSection = sectionName;
                Array.Copy (data, 0, recvBuffer, recvdBytes, size);
                recvdBytes += size;
                Console.WriteLine ("File segment - sectionName: {0} offset: {1} size: {2}", sectionName, offset, size);
                for (int i = 0; i < size; i++) {
                    Console.Write (" " + data [i]);
                }
                Console.WriteLine ();
            }
        }

        [Test ()]
        public void TestFileUploadSingleSection ()
        {
            Server server = new Server ();
            server.SetLocalPort (20213);
            server.Start ();

            SimpleFile file = new SimpleFile (1, 30000, NameOfFile.TRANSPARENT_FILE);

            byte [] fileData = new byte [100];

            for (int i = 0; i < 100; i++)
                fileData [i] = (byte)(i);
               
            file.AddSection (fileData);

            server.GetAvailableFiles ().AddFile (file);

            Connection con = new Connection ("127.0.0.1", 20213);
            con.Connect ();


            Receiver receiver = new Receiver ();

            con.GetFile (1, 30000, NameOfFile.TRANSPARENT_FILE, receiver);

            Thread.Sleep (3000);
            Assert.IsTrue (receiver.finishedCalled);
            Assert.AreEqual (100, receiver.recvdBytes);
            Assert.AreEqual (1, receiver.lastSection);

            for (int i = 0; i < 100; i++) {
                Assert.AreEqual (receiver.recvBuffer [i], i);
            }

            con.Close ();

            server.Stop ();
        }

        [Test ()]
        public void TestFileUploadMultipleSections ()
        {
            Server server = new Server ();
            server.SetLocalPort (20213);
            server.Start ();

            SimpleFile file = new SimpleFile (1, 30000, NameOfFile.TRANSPARENT_FILE);

            byte [] fileData = new byte [100];

            for (int i = 0; i < 100; i++)
                fileData [i] = (byte)(i);

            byte [] fileData2 = new byte [100];

            for (int i = 0; i < 100; i++)
                fileData2 [i] = (byte)(100 + i);

            file.AddSection (fileData);
            file.AddSection (fileData2);

            server.GetAvailableFiles().AddFile (file);

            Connection con = new Connection ("127.0.0.1", 20213);
            con.Connect ();


            Receiver receiver = new Receiver ();

            con.GetFile (1, 30000, NameOfFile.TRANSPARENT_FILE, receiver);

            Thread.Sleep (3000);
            Assert.IsTrue (receiver.finishedCalled);
            Assert.AreEqual (200, receiver.recvdBytes);
            Assert.AreEqual (2, receiver.lastSection);

            for (int i = 0; i < 200; i++) {
                Assert.AreEqual (receiver.recvBuffer [i], i);
            }

            con.Close ();

            server.Stop ();
        }

        [Test()]
        public void TestFileUploadMultipleSectionsFreeFileName()
        {
            Server server = new Server();
            server.SetLocalPort(20213);
            server.Start();

            SimpleFile file = new SimpleFile(1, 30000, (NameOfFile) 12);

            byte[] fileData = new byte[100];

            for (int i = 0; i < 100; i++)
                fileData[i] = (byte)(i);

            byte[] fileData2 = new byte[100];

            for (int i = 0; i < 100; i++)
                fileData2[i] = (byte)(100 + i);

            file.AddSection(fileData);
            file.AddSection(fileData2);

            server.GetAvailableFiles().AddFile(file);

            Connection con = new Connection("127.0.0.1", 20213);
            con.Connect();


            Receiver receiver = new Receiver();

            con.GetFile(1, 30000, (NameOfFile) 12,  receiver);

            Thread.Sleep(3000);
            Assert.IsTrue(receiver.finishedCalled);
            Assert.AreEqual(200, receiver.recvdBytes);
            Assert.AreEqual(2, receiver.lastSection);

            for (int i = 0; i < 200; i++)
            {
                Assert.AreEqual(receiver.recvBuffer[i], i);
            }

            con.Close();

            server.Stop();
        }

        [Test ()]
        public void TestFileUploadMultipleSegments ()
        {
            Server server = new Server ();
            server.SetLocalPort (20213);
            server.Start ();

            SimpleFile file = new SimpleFile (1, 30000, NameOfFile.TRANSPARENT_FILE);

            byte [] fileData = new byte [1000];

            for (int i = 0; i < 1000; i++)
                fileData [i] = (byte)(i);
;
            file.AddSection (fileData);

            server.GetAvailableFiles ().AddFile (file);

            Connection con = new Connection ("127.0.0.1", 20213);
            con.Connect ();


            Receiver receiver = new Receiver ();

            con.GetFile (1, 30000, NameOfFile.TRANSPARENT_FILE, receiver);

            Thread.Sleep (3000);
            Assert.IsTrue (receiver.finishedCalled);
            Assert.AreEqual (1000, receiver.recvdBytes);
            Assert.AreEqual (1, receiver.lastSection);
            Assert.IsTrue (file.transferComplete);
            Assert.IsTrue (file.success);

            for (int i = 0; i < 1000; i++) {
                Assert.AreEqual (receiver.recvBuffer [i], (byte) i);
            }

            con.Close ();

            server.Stop ();
        }


        [Test ()]
        public void TestFileDownloadSingleSection ()
        {
            Server server = new Server ();
            server.SetLocalPort (20213);
            server.DebugOutput = true;
            server.Start ();

            SimpleFile file = new SimpleFile (1, 30000, NameOfFile.TRANSPARENT_FILE);

            byte [] fileData = new byte [100];

            for (int i = 0; i < 100; i++)
                fileData [i] = (byte)(i);

            file.AddSection (fileData);

            Receiver receiver = new Receiver ();

            server.SetFileReadyHandler (delegate (object parameter, int ca, int ioa, NameOfFile nof, int lengthOfFile) {
                return receiver;
            }, null);

            Connection con = new Connection ("127.0.0.1", 20213);
            con.DebugOutput = true;
            con.Connect ();

            con.SendFile (1, 30000, NameOfFile.TRANSPARENT_FILE, file);

            Thread.Sleep (3000);
            Assert.IsTrue (receiver.finishedCalled);
            Assert.AreEqual (100, receiver.recvdBytes);
            Assert.AreEqual (1, receiver.lastSection);

            for (int i = 0; i < 100; i++) {
                Assert.AreEqual (receiver.recvBuffer [i], i);
            }

            con.Close ();

            server.Stop ();
        }

        [Test ()]
        public void TestFileDownloadMultipleSegments ()
        {
            Server server = new Server ();
            server.SetLocalPort (20213);
            server.DebugOutput = true;
            server.Start ();

            SimpleFile file = new SimpleFile (1, 30000, NameOfFile.TRANSPARENT_FILE);

            byte [] fileData = new byte [1000];

            for (int i = 0; i < 1000; i++)
                fileData [i] = (byte)(i);

            file.AddSection (fileData);

            Receiver receiver = new Receiver ();

            server.SetFileReadyHandler (delegate (object parameter, int ca, int ioa, NameOfFile nof, int lengthOfFile) {
                return receiver;
            }, null);

            Connection con = new Connection ("127.0.0.1", 20213);
            con.DebugOutput = true;
            con.Connect ();

            con.SendFile (1, 30000, NameOfFile.TRANSPARENT_FILE, file);

            Thread.Sleep (3000);
            Assert.IsTrue (receiver.finishedCalled);
            Assert.AreEqual (1000, receiver.recvdBytes);
            Assert.AreEqual (1, receiver.lastSection);
            Assert.IsTrue (file.transferComplete);
            Assert.IsTrue (file.success);

            for (int i = 0; i < 1000; i++) {
                Assert.AreEqual (receiver.recvBuffer [i], (byte)i);
            }

            con.Close ();

            server.Stop ();
        }

        [Test ()]
        public void TestFileDownloadMultipleSegmentsMultipleSections ()
        {
            Server server = new Server ();
            server.SetLocalPort (20213);
            server.DebugOutput = true;
            server.Start ();

            SimpleFile file = new SimpleFile (1, 30000, NameOfFile.TRANSPARENT_FILE);

            byte [] fileData = new byte [1000];

            for (int i = 0; i < 1000; i++)
                fileData [i] = (byte)(i);

            file.AddSection (fileData);

            byte [] fileData2 = new byte [1000];

            for (int i = 0; i < 1000; i++)
                fileData2 [i] = (byte)(i * 2);

            file.AddSection (fileData2);

            Receiver receiver = new Receiver ();

            server.SetFileReadyHandler (delegate (object parameter, int ca, int ioa, NameOfFile nof, int lengthOfFile) {
                return receiver;
            }, null);

            Connection con = new Connection ("127.0.0.1", 20213);
            con.DebugOutput = true;
            con.Connect ();

            con.SendFile (1, 30000, NameOfFile.TRANSPARENT_FILE, file);

            Thread.Sleep (3000);
            Assert.IsTrue (receiver.finishedCalled);
            Assert.AreEqual (2000, receiver.recvdBytes);
            Assert.AreEqual (2, receiver.lastSection);
            Assert.IsTrue (file.transferComplete);
            Assert.IsTrue (file.success);

            for (int i = 0; i < 1000; i++) {
                Assert.AreEqual (receiver.recvBuffer [i], (byte)i);
            }

            for (int i = 0; i < 1000; i++) {
                Assert.AreEqual (receiver.recvBuffer [i + 1000], (byte)(i * 2));
            }

            con.Close ();

            server.Stop ();
        }

        [Test ()]
        public void TestFileDownloadSlaveRejectsFile()
        {
            Server server = new Server ();
            server.SetLocalPort (20213);
            server.DebugOutput = true;
            server.Start ();

            SimpleFile file = new SimpleFile (1, 30000, NameOfFile.TRANSPARENT_FILE);

            byte [] fileData = new byte [100];

            for (int i = 0; i < 100; i++)
                fileData [i] = (byte)(i);

            file.AddSection (fileData);

            Receiver receiver = new Receiver ();

            server.SetFileReadyHandler (delegate (object parameter, int ca, int ioa, NameOfFile nof, int lengthOfFile) {
                return null;
            }, null);

            Connection con = new Connection ("127.0.0.1", 20213);
            con.DebugOutput = true;
            con.Connect ();

            con.SendFile (1, 30000, NameOfFile.TRANSPARENT_FILE, file);

            Thread.Sleep (1000);

            Assert.IsTrue (file.transferComplete);
            Assert.IsFalse (file.success);

            Assert.IsFalse (receiver.finishedCalled);
            Assert.AreEqual (0, receiver.recvdBytes);
            Assert.AreEqual (0, receiver.lastSection);

            con.Close ();

            server.Stop ();
        }

    }
}

