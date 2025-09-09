using lib60870;
using lib60870.CS101;
using lib60870.CS104;
using NUnit.Framework;
using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

namespace tests
{

    class TestInteger32Object : InformationObject, IPrivateIOFactory
    {
        private int value = 0;

        public TestInteger32Object()
            : base(0)
        {
        }

        public TestInteger32Object(int ioa, int value)
            : base(ioa)
        {
            this.value = value;
        }

        public int Value
        {
            get
            {
                return value;
            }
            set
            {
                this.value = value;
            }
        }

        private TestInteger32Object(ApplicationLayerParameters parameters, byte[] msg, int startIndex, bool isSequence)
            : base(parameters, msg, startIndex, isSequence)
        {
            if (!isSequence)
                startIndex += parameters.SizeOfIOA; /* skip IOA */

            value = msg[startIndex++];
            value += (msg[startIndex++] * 0x100);
            value += (msg[startIndex++] * 0x10000);
            value += (msg[startIndex++] * 0x1000000);
        }

        public override bool SupportsSequence
        {
            get
            {
                return true;
            }
        }

        public override TypeID Type
        {
            get
            {
                return (TypeID)41;
            }
        }

        InformationObject IPrivateIOFactory.Decode(ApplicationLayerParameters parameters, byte[] msg, int startIndex, bool isSequence)
        {
            return new TestInteger32Object(parameters, msg, startIndex, isSequence);
        }

        public override int GetEncodedSize()
        {
            return 4;
        }

        public override void Encode(Frame frame, ApplicationLayerParameters parameters, bool isSequence)
        {
            base.Encode(frame, parameters, isSequence);

            frame.SetNextByte((byte)(value % 0x100));
            frame.SetNextByte((byte)((value / 0x100) % 0x100));
            frame.SetNextByte((byte)((value / 0x10000) % 0x100));
            frame.SetNextByte((byte)(value / 0x1000000));
        }
    }

    [TestFixture()]
    public class Test
    {
        private int port = 24000;
        private static readonly object _lockObject = new object();

        private int GetPort()
        {
            int newPort;

            lock (_lockObject)
            {
                port++;
                newPort = port;
            }

            return newPort;
        }

        [Test()]
        public void TestStatusAndStatusChangedDetection()
        {

            StatusAndStatusChangeDetection scd = new StatusAndStatusChangeDetection();

            Assert.AreEqual(false, scd.ST(0));
            Assert.AreEqual(false, scd.ST(15));
            Assert.AreEqual(false, scd.CD(0));
            Assert.AreEqual(false, scd.CD(15));

            Assert.AreEqual(false, scd.CD(1));

            scd.CD(0, true);

            Assert.AreEqual(true, scd.CD(0));
            Assert.AreEqual(false, scd.CD(1));

            scd.CD(15, true);

            Assert.AreEqual(true, scd.CD(15));
            Assert.AreEqual(false, scd.CD(14));
        }

        [Test()]
        public void TestBCR()
        {
            BinaryCounterReading bcr = new BinaryCounterReading();

            bcr.Value = 1000;

            Assert.AreEqual(1000, bcr.Value);

            bcr.Value = -1000;

            Assert.AreEqual(-1000, bcr.Value);

            bcr.SequenceNumber = 31;

            Assert.AreEqual(31, bcr.SequenceNumber);

            bcr.SequenceNumber = 0;

            Assert.AreEqual(0, bcr.SequenceNumber);

            /* Out of range sequenceNumber */
            bcr.SequenceNumber = 32;

            Assert.AreEqual(0, bcr.SequenceNumber);

            bcr = new BinaryCounterReading();

            bcr.Invalid = true;

            Assert.AreEqual(true, bcr.Invalid);
            Assert.AreEqual(false, bcr.Carry);
            Assert.AreEqual(false, bcr.Adjusted);
            Assert.AreEqual(0, bcr.SequenceNumber);
            Assert.AreEqual(0, bcr.Value);

            bcr = new BinaryCounterReading();

            bcr.Carry = true;

            Assert.AreEqual(false, bcr.Invalid);
            Assert.AreEqual(true, bcr.Carry);
            Assert.AreEqual(false, bcr.Adjusted);
            Assert.AreEqual(0, bcr.SequenceNumber);
            Assert.AreEqual(0, bcr.Value);

            bcr = new BinaryCounterReading();

            bcr.Adjusted = true;

            Assert.AreEqual(false, bcr.Invalid);
            Assert.AreEqual(false, bcr.Carry);
            Assert.AreEqual(true, bcr.Adjusted);
            Assert.AreEqual(0, bcr.SequenceNumber);
            Assert.AreEqual(0, bcr.Value);
        }

        [Test()]
        public void TestScaledValue()
        {
            ScaledValue scaledValue = new ScaledValue(0);

            Assert.AreEqual(0, scaledValue.Value);
            Assert.AreEqual((short)0, scaledValue.ShortValue);

            scaledValue = new ScaledValue(32767);
            Assert.AreEqual(32767, scaledValue.Value);
            Assert.AreEqual((short)32767, scaledValue.ShortValue);

            scaledValue = new ScaledValue(32768);
            Assert.AreEqual(32767, scaledValue.Value);
            Assert.AreEqual((short)32767, scaledValue.ShortValue);

            scaledValue = new ScaledValue(-32768);
            Assert.AreEqual(-32768, scaledValue.Value);
            Assert.AreEqual((short)-32768, scaledValue.ShortValue);

            scaledValue = new ScaledValue(-32769);
            Assert.AreEqual(-32768, scaledValue.Value);
            Assert.AreEqual((short)-32768, scaledValue.ShortValue);

            scaledValue = new ScaledValue(-1);
            Assert.AreEqual(-1, scaledValue.Value);

            scaledValue = new ScaledValue(-300);
            Assert.AreEqual(-300, scaledValue.Value);
        }

        [Test()]
        public void TestReadCommand()
        {
            ReadCommand rc = new ReadCommand(101);

            Assert.AreEqual(101, rc.ObjectAddress);

            rc = new ReadCommand(102);

            Assert.AreEqual(102, rc.ObjectAddress);
        }

        [Test()]
        public void TestTestCommand()
        {
            TestCommand tc = new TestCommand();

            Assert.IsTrue(tc.Valid);

        }

        public void TestClockSynchronizationCommand()
        {
            DateTime dateTime = DateTime.UtcNow;

            CP56Time2a time = new CP56Time2a(dateTime);

            ClockSynchronizationCommand csc = new ClockSynchronizationCommand(101, time);

            Assert.AreEqual(101, csc.ObjectAddress);
            Assert.AreEqual(time.Year, csc.NewTime.Year);
            Assert.AreEqual(time.Month, csc.NewTime.Month);
            Assert.AreEqual(time.DayOfMonth, csc.NewTime.DayOfMonth);
            Assert.AreEqual(time.Minute, csc.NewTime.Minute);
            Assert.AreEqual(time.Second, csc.NewTime.Second);
            Assert.AreEqual(time.Millisecond, csc.NewTime.Millisecond);

            csc = new ClockSynchronizationCommand(102, time);

            Assert.AreEqual(102, csc.ObjectAddress);
            Assert.AreEqual(time.Year, csc.NewTime.Year);
            Assert.AreEqual(time.Month, csc.NewTime.Month);
            Assert.AreEqual(time.DayOfMonth, csc.NewTime.DayOfMonth);
            Assert.AreEqual(time.Minute, csc.NewTime.Minute);
            Assert.AreEqual(time.Second, csc.NewTime.Second);
            Assert.AreEqual(time.Millisecond, csc.NewTime.Millisecond);
        }

        [Test()]
        public void TestResetProcessCommand()
        {
            ResetProcessCommand rp = new ResetProcessCommand(101, 0);

            Assert.AreEqual(101, rp.ObjectAddress);
            Assert.AreEqual(0, rp.QRP);

            rp = new ResetProcessCommand(102, 1);

            Assert.AreEqual(102, rp.ObjectAddress);
            Assert.AreEqual(1, rp.QRP);
        }

        [Test()]
        public void TestDelayAcquisitionCommand()
        {
            CP16Time2a time = new CP16Time2a(24123);

            DelayAcquisitionCommand da = new DelayAcquisitionCommand(101, time);

            Assert.AreEqual(101, da.ObjectAddress);
            Assert.AreEqual(24123, da.Delay.ElapsedTimeInMs);
        }

        [Test()]
        public void TestBitString32()
        {
            Bitstring32 bs = new Bitstring32(101, 0xaaaa_aaaa, new QualityDescriptor());

            Assert.AreEqual(101, bs.ObjectAddress);
            Assert.AreEqual(0xaaaa_aaaa, bs.Value);

            bs = new Bitstring32(101, 0xffff_0000, new QualityDescriptor());

            Assert.AreEqual(101, bs.ObjectAddress);
            Assert.AreEqual(0xffff_0000, bs.Value);
        }

        [Test()]
        public void TestBitString32CommandWithCP56Time2a()
        {
            DateTime dateTime = DateTime.UtcNow;

            CP56Time2a time = new CP56Time2a(dateTime);

            Bitstring32CommandWithCP56Time2a bsc = new Bitstring32CommandWithCP56Time2a(101, 0x00000000, time);

            Assert.AreEqual(101, bsc.ObjectAddress);
            Assert.AreEqual(0x00000000, bsc.Value);

            bsc = new Bitstring32CommandWithCP56Time2a(101, 0x12345678, time);

            Assert.AreEqual(101, bsc.ObjectAddress);
            Assert.AreEqual(0x12345678, bsc.Value);
            Assert.AreEqual(time.Year, bsc.Timestamp.Year);
            Assert.AreEqual(time.Month, bsc.Timestamp.Month);
            Assert.AreEqual(time.DayOfMonth, bsc.Timestamp.DayOfMonth);
            Assert.AreEqual(time.Minute, bsc.Timestamp.Minute);
            Assert.AreEqual(time.Second, bsc.Timestamp.Second);
            Assert.AreEqual(time.Millisecond, bsc.Timestamp.Millisecond);
        }

        [Test()]
        public void TestEventOfProtectionEquipmentWithTime()
        {
            CP16Time2a elapsedTime = new CP16Time2a(24123);
            CP24Time2a timestamp = new CP24Time2a(45, 23, 538);

            EventOfProtectionEquipment e = new EventOfProtectionEquipment(101, new SingleEvent(), elapsedTime, timestamp);

            Assert.AreEqual(101, e.ObjectAddress);
            Assert.AreEqual(24123, e.ElapsedTime.ElapsedTimeInMs);
            Assert.AreEqual(45, e.Timestamp.Minute);
            Assert.AreEqual(23, e.Timestamp.Second);
            Assert.AreEqual(538, e.Timestamp.Millisecond);
        }

        [Test()]
        public void TestInterrogationCommand()
        {
            InterrogationCommand ic = new InterrogationCommand(101, 20);

            Assert.AreEqual(101, ic.ObjectAddress);
            Assert.AreEqual(20, ic.QOI);

            ic = new InterrogationCommand(101, 21);

            Assert.AreEqual(101, ic.ObjectAddress);
            Assert.AreEqual(21, ic.QOI);

            ic = new InterrogationCommand(101, 24);

            Assert.AreEqual(101, ic.ObjectAddress);
            Assert.AreEqual(24, ic.QOI);
        }

        [Test()]
        public void TestCounterInterrogationCommand()
        {
            CounterInterrogationCommand cic = new CounterInterrogationCommand(101, 20);

            Assert.AreEqual(101, cic.ObjectAddress);
            Assert.AreEqual(20, cic.QCC);

            cic = new CounterInterrogationCommand(101, 21);

            Assert.AreEqual(101, cic.ObjectAddress);
            Assert.AreEqual(21, cic.QCC);

            cic = new CounterInterrogationCommand(101, 24);

            Assert.AreEqual(101, cic.ObjectAddress);
            Assert.AreEqual(24, cic.QCC);
        }

        [Test()]
        public void TestSetpointCommandShort()
        {
            SetpointCommandShort sc = new SetpointCommandShort(101, 10.5f, new SetpointCommandQualifier(true, 0));

            Assert.AreEqual(101, sc.ObjectAddress);
            Assert.AreEqual(10.5f, sc.Value, 0.001f);
            Assert.AreEqual(true, sc.QOS.Select);

            sc = new SetpointCommandShort(102, 1.0f, new SetpointCommandQualifier(true, 0));

            Assert.AreEqual(102, sc.ObjectAddress);
            Assert.AreEqual(1.0f, sc.Value, 0.001f);
            Assert.AreEqual(true, sc.QOS.Select);

            sc = new SetpointCommandShort(102, -1.0f, new SetpointCommandQualifier(true, 0));

            Assert.AreEqual(102, sc.ObjectAddress);
            Assert.AreEqual(-1.0f, sc.Value, 0.001f);
            Assert.AreEqual(true, sc.QOS.Select);
        }

        [Test()]
        public void TestSetpointCommandShortWithCP56Time2a()
        {
            DateTime dateTime = DateTime.UtcNow;

            CP56Time2a time = new CP56Time2a(dateTime);

            SetpointCommandShortWithCP56Time2a sc = new SetpointCommandShortWithCP56Time2a(101, 10.5f, new SetpointCommandQualifier(true, 0), time);

            Assert.AreEqual(101, sc.ObjectAddress);
            Assert.AreEqual(10.5f, sc.Value, 0.001f);
            Assert.AreEqual(true, sc.QOS.Select);
            Assert.AreEqual(time.Year, sc.Timestamp.Year);
            Assert.AreEqual(time.Month, sc.Timestamp.Month);
            Assert.AreEqual(time.DayOfMonth, sc.Timestamp.DayOfMonth);
            Assert.AreEqual(time.Minute, sc.Timestamp.Minute);
            Assert.AreEqual(time.Second, sc.Timestamp.Second);
            Assert.AreEqual(time.Millisecond, sc.Timestamp.Millisecond);

            sc = new SetpointCommandShortWithCP56Time2a(102, 1.0f, new SetpointCommandQualifier(true, 0), time);

            Assert.AreEqual(102, sc.ObjectAddress);
            Assert.AreEqual(1.0f, sc.Value, 0.001f);
            Assert.AreEqual(true, sc.QOS.Select);
            Assert.AreEqual(time.Year, sc.Timestamp.Year);
            Assert.AreEqual(time.Month, sc.Timestamp.Month);
            Assert.AreEqual(time.DayOfMonth, sc.Timestamp.DayOfMonth);
            Assert.AreEqual(time.Minute, sc.Timestamp.Minute);
            Assert.AreEqual(time.Second, sc.Timestamp.Second);
            Assert.AreEqual(time.Millisecond, sc.Timestamp.Millisecond);

            sc = new SetpointCommandShortWithCP56Time2a(102, -1.0f, new SetpointCommandQualifier(true, 0), time);

            Assert.AreEqual(102, sc.ObjectAddress);
            Assert.AreEqual(-1.0f, sc.Value, 0.001f);
            Assert.AreEqual(true, sc.QOS.Select);
            Assert.AreEqual(time.Year, sc.Timestamp.Year);
            Assert.AreEqual(time.Month, sc.Timestamp.Month);
            Assert.AreEqual(time.DayOfMonth, sc.Timestamp.DayOfMonth);
            Assert.AreEqual(time.Minute, sc.Timestamp.Minute);
            Assert.AreEqual(time.Second, sc.Timestamp.Second);
            Assert.AreEqual(time.Millisecond, sc.Timestamp.Millisecond);
        }

        [Test()]
        public void TestSetpointCommandScaled()
        {
            SetpointCommandScaled sc = new SetpointCommandScaled(101, new ScaledValue(-32767), new SetpointCommandQualifier(true, 0));

            Assert.AreEqual(101, sc.ObjectAddress);
            Assert.AreEqual(-32767, sc.ScaledValue.Value);
            Assert.AreEqual(true, sc.QOS.Select);

            sc = new SetpointCommandScaled(101, new ScaledValue(32767), new SetpointCommandQualifier(true, 0));

            Assert.AreEqual(101, sc.ObjectAddress);
            Assert.AreEqual(32767, sc.ScaledValue.ShortValue);
            Assert.AreEqual(true, sc.QOS.Select);

            sc = new SetpointCommandScaled(101, new ScaledValue(-32768), new SetpointCommandQualifier(true, 0));

            Assert.AreEqual(101, sc.ObjectAddress);
            Assert.AreEqual(-32768, sc.ScaledValue.ShortValue);
            Assert.AreEqual(true, sc.QOS.Select);
        }

        [Test()]
        public void TestSetpointCommandScaledWithCP56Time2a()
        {
            DateTime dateTime = DateTime.UtcNow;

            CP56Time2a time = new CP56Time2a(dateTime);

            SetpointCommandScaledWithCP56Time2a sc = new SetpointCommandScaledWithCP56Time2a(101, new ScaledValue(1), new SetpointCommandQualifier(true, 0), time);

            Assert.AreEqual(101, sc.ObjectAddress);
            Assert.AreEqual(1, sc.ScaledValue.Value);
            Assert.AreEqual(true, sc.QOS.Select);
            Assert.AreEqual(time.Year, sc.Timestamp.Year);
            Assert.AreEqual(time.Month, sc.Timestamp.Month);
            Assert.AreEqual(time.DayOfMonth, sc.Timestamp.DayOfMonth);
            Assert.AreEqual(time.Minute, sc.Timestamp.Minute);
            Assert.AreEqual(time.Second, sc.Timestamp.Second);
            Assert.AreEqual(time.Millisecond, sc.Timestamp.Millisecond);

            sc = new SetpointCommandScaledWithCP56Time2a(101, new ScaledValue(32767), new SetpointCommandQualifier(true, 0), time);

            Assert.AreEqual(101, sc.ObjectAddress);
            Assert.AreEqual(32767, sc.ScaledValue.ShortValue);
            Assert.AreEqual(true, sc.QOS.Select);
            Assert.AreEqual(time.Year, sc.Timestamp.Year);
            Assert.AreEqual(time.Month, sc.Timestamp.Month);
            Assert.AreEqual(time.DayOfMonth, sc.Timestamp.DayOfMonth);
            Assert.AreEqual(time.Minute, sc.Timestamp.Minute);
            Assert.AreEqual(time.Second, sc.Timestamp.Second);
            Assert.AreEqual(time.Millisecond, sc.Timestamp.Millisecond);

            sc = new SetpointCommandScaledWithCP56Time2a(101, new ScaledValue(-32768), new SetpointCommandQualifier(true, 0), time);

            Assert.AreEqual(101, sc.ObjectAddress);
            Assert.AreEqual(-32768, sc.ScaledValue.ShortValue);
            Assert.AreEqual(true, sc.QOS.Select);
            Assert.AreEqual(time.Year, sc.Timestamp.Year);
            Assert.AreEqual(time.Month, sc.Timestamp.Month);
            Assert.AreEqual(time.DayOfMonth, sc.Timestamp.DayOfMonth);
            Assert.AreEqual(time.Minute, sc.Timestamp.Minute);
            Assert.AreEqual(time.Second, sc.Timestamp.Second);
            Assert.AreEqual(time.Millisecond, sc.Timestamp.Millisecond);
        }

        [Test()]
        public void TestSetpointCommandNormalized()
        {
            SetpointCommandNormalized sc = new SetpointCommandNormalized(102, -0.5f,
                new SetpointCommandQualifier(true, 0));

            Assert.AreEqual(102, sc.ObjectAddress);
            Assert.AreEqual(-0.5f, sc.NormalizedValue, 0.001f);
            Assert.AreEqual(true, sc.QOS.Select);

            sc = new SetpointCommandNormalized(102, 32767, new SetpointCommandQualifier(true, 0));

            Assert.AreEqual(1.0, sc.NormalizedValue, 0.001f);
            Assert.AreEqual(32767, sc.RawValue);

            sc = new SetpointCommandNormalized(102, -32768, new SetpointCommandQualifier(true, 0));

            Assert.AreEqual(-1.0, sc.NormalizedValue, 0.001f);
            Assert.AreEqual(-32768, sc.RawValue);
        }

        [Test()]
        public void TestSetpointCommandNormalizedWithCP56Time2a()
        {
            DateTime dateTime = DateTime.UtcNow;

            CP56Time2a time = new CP56Time2a(dateTime);

            SetpointCommandNormalizedWithCP56Time2a sc = new SetpointCommandNormalizedWithCP56Time2a(102, -0.5f, new SetpointCommandQualifier(true, 0), time);

            Assert.AreEqual(102, sc.ObjectAddress);
            Assert.AreEqual(-0.5f, sc.NormalizedValue, 0.001f);
            Assert.AreEqual(true, sc.QOS.Select);
            Assert.AreEqual(time.Year, sc.Timestamp.Year);
            Assert.AreEqual(time.Month, sc.Timestamp.Month);
            Assert.AreEqual(time.DayOfMonth, sc.Timestamp.DayOfMonth);
            Assert.AreEqual(time.Minute, sc.Timestamp.Minute);
            Assert.AreEqual(time.Second, sc.Timestamp.Second);
            Assert.AreEqual(time.Millisecond, sc.Timestamp.Millisecond);

            sc = new SetpointCommandNormalizedWithCP56Time2a(102, 32767, new SetpointCommandQualifier(true, 0), time);

            Assert.AreEqual(1.0, sc.NormalizedValue, 0.001f);
            Assert.AreEqual(32767, sc.RawValue);
            Assert.AreEqual(time.Year, sc.Timestamp.Year);
            Assert.AreEqual(time.Month, sc.Timestamp.Month);
            Assert.AreEqual(time.DayOfMonth, sc.Timestamp.DayOfMonth);
            Assert.AreEqual(time.Minute, sc.Timestamp.Minute);
            Assert.AreEqual(time.Second, sc.Timestamp.Second);
            Assert.AreEqual(time.Millisecond, sc.Timestamp.Millisecond);

            sc = new SetpointCommandNormalizedWithCP56Time2a(102, -32768, new SetpointCommandQualifier(true, 0), time);

            Assert.AreEqual(-1.0, sc.NormalizedValue, 0.001f);
            Assert.AreEqual(-32768, sc.RawValue);
            Assert.AreEqual(time.Year, sc.Timestamp.Year);
            Assert.AreEqual(time.Month, sc.Timestamp.Month);
            Assert.AreEqual(time.DayOfMonth, sc.Timestamp.DayOfMonth);
            Assert.AreEqual(time.Minute, sc.Timestamp.Minute);
            Assert.AreEqual(time.Second, sc.Timestamp.Second);
            Assert.AreEqual(time.Millisecond, sc.Timestamp.Millisecond);
        }

        [Test()]
        public void TestStepPositionInformation()
        {
            StepPositionInformation spi = new StepPositionInformation(103, 27, false, new QualityDescriptor());

            Assert.IsFalse(spi.Transient);
            Assert.NotNull(spi.Quality);

            spi = null;

            try
            {
                spi = new StepPositionInformation(103, 64, false, new QualityDescriptor());
            }
            catch (ArgumentOutOfRangeException)
            {
            }

            Assert.IsNull(spi);

            try
            {
                spi = new StepPositionInformation(103, -65, false, new QualityDescriptor());
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }

        [Test()]
        public void TestStepPositionInformationWithCP24Time2a()
        {
            CP24Time2a time = new CP24Time2a(45, 23, 538);

            StepPositionWithCP24Time2a spi = new StepPositionWithCP24Time2a(103, 27, false, new QualityDescriptor(), time);

            Assert.IsFalse(spi.Transient);
            Assert.NotNull(spi.Quality);
            Assert.AreEqual(45, spi.Timestamp.Minute);
            Assert.AreEqual(23, spi.Timestamp.Second);
            Assert.AreEqual(538, spi.Timestamp.Millisecond);

            spi = null;

            try
            {
                spi = new StepPositionWithCP24Time2a(103, 64, false, new QualityDescriptor(), time);
            }
            catch (ArgumentOutOfRangeException)
            {
            }

            Assert.IsNull(spi);

            try
            {
                spi = new StepPositionWithCP24Time2a(103, -65, false, new QualityDescriptor(), time);
            }
            catch (ArgumentOutOfRangeException)
            {
            }

        }

        [Test()]
        public void TestStepPositionInformationWithCP56Time2a()
        {
            DateTime dateTime = DateTime.UtcNow;

            CP56Time2a time = new CP56Time2a(dateTime);

            StepPositionWithCP56Time2a spi = new StepPositionWithCP56Time2a(103, 27, false, new QualityDescriptor(), time);

            Assert.IsFalse(spi.Transient);
            Assert.NotNull(spi.Quality);
            Assert.AreEqual(time.Year, spi.Timestamp.Year);
            Assert.AreEqual(time.Month, spi.Timestamp.Month);
            Assert.AreEqual(time.DayOfMonth, spi.Timestamp.DayOfMonth);
            Assert.AreEqual(time.Minute, spi.Timestamp.Minute);
            Assert.AreEqual(time.Second, spi.Timestamp.Second);
            Assert.AreEqual(time.Millisecond, spi.Timestamp.Millisecond);

            spi = null;

            try
            {
                spi = new StepPositionWithCP56Time2a(103, 64, false, new QualityDescriptor(), time);
            }
            catch (ArgumentOutOfRangeException)
            {
            }

            Assert.IsNull(spi);

            try
            {
                spi = new StepPositionWithCP56Time2a(103, -65, false, new QualityDescriptor(), time);
            }
            catch (ArgumentOutOfRangeException)
            {
            }

        }

        [Test()]
        //[Ignore("Ignore to save execution time")]
        public void TestConnectWhileAlreadyConnected()
        {
            ApplicationLayerParameters parameters = new ApplicationLayerParameters();
            APCIParameters apciParameters = new APCIParameters();

            Server server = new Server(apciParameters, parameters);

            int port = GetPort();

            server.SetLocalPort(port);

            server.Start();

            Connection connection = new Connection("127.0.0.1", port, apciParameters, parameters);

            ConnectionException se = null;

            try
            {
                connection.Connect();
            }
            catch (ConnectionException ex)
            {
                se = ex;
            }

            Assert.IsNull(se);

            Thread.Sleep(100);

            try
            {
                connection.Connect();
            }
            catch (ConnectionException ex)
            {
                se = ex;
            }

            Assert.IsNotNull(se);
            Assert.AreEqual(se.Message, "already connected");

            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Assert.AreEqual(106, ((SocketException)se.InnerException).ErrorCode);
            }

            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.AreEqual(10056, ((SocketException)se.InnerException).ErrorCode);
            }

            connection.Close();

            server.Stop();
        }

        struct CS104SlaveEventQueue1
        {
            public int asduHandlerCalled;
            public int spontCount;
            public short lastScaledValue;
        }

        private static bool EventQueue1_asduReceivedHandler(object param, ASDU asdu)
        {
            CS104SlaveEventQueue1 info = (CS104SlaveEventQueue1)param;
            Console.WriteLine($"[Handler] Received ASDU with Type: {asdu.TypeId}, COT: {asdu.Cot}");
            info.asduHandlerCalled++;

            if (asdu.Cot == CauseOfTransmission.SPONTANEOUS)
            {
                info.spontCount++;

                if (asdu.TypeId == TypeID.M_ME_NB_1)
                {
                    MeasuredValueScaled mv = (MeasuredValueScaled)asdu.GetElement(0);
                    info.lastScaledValue = (short)mv.ScaledValue.Value;
                    Console.WriteLine($"[Handler] Updated lastScaledValue: {info.lastScaledValue}");
                }
            }
            return true;
        }

        [Test()]
        public void TestAddUntilOverflow()
        {

            ASDU asdu = new ASDU(new ApplicationLayerParameters(), CauseOfTransmission.PERIODIC, false, false, 0, 1, false);

            int i = 0;

            for (i = 0; i < 60; i++)
            {
                SinglePointInformation sp = new SinglePointInformation(100 + i, true, new QualityDescriptor());

                bool added = asdu.AddInformationObject(sp);

                Assert.IsTrue(added);

                Assert.AreEqual(i + 1, asdu.NumberOfElements);
            }

            SinglePointInformation sp2 = new SinglePointInformation(100 + i, true, new QualityDescriptor());

            bool addedIO = asdu.AddInformationObject(sp2);

            Assert.IsFalse(addedIO);

        }

        [Test()]
        public void TestUnconfirmedStoppedState()
        {
            ApplicationLayerParameters parameters = new ApplicationLayerParameters();
            APCIParameters apciParameters = new APCIParameters();

            Server server = new Server(apciParameters, parameters);

            server.ServerMode = ServerMode.SINGLE_REDUNDANCY_GROUP;

            int port = GetPort();
            server.SetLocalPort(port);

            server.Start();

            server.DebugOutput = true;

            CS104SlaveEventQueue1 info = new CS104SlaveEventQueue1
            {
                asduHandlerCalled = 0,
                spontCount = 0,
                lastScaledValue = 0
            };

            //short scaledValue = 0;

            for (int i = 0; i < 15; i++)
            {
                ASDU newASDU = new ASDU(server.GetApplicationLayerParameters(), CauseOfTransmission.SPONTANEOUS, false, false, 0, 1, false);

                info.lastScaledValue++;

                newASDU.AddInformationObject(new MeasuredValueScaled(110, info.lastScaledValue, new QualityDescriptor()));

                Console.WriteLine($"[Test] Enqueuing ASDU with ScaledValue: {info.lastScaledValue}");

                server.EnqueueASDU(newASDU);

                Thread.Sleep(50); // Increase delay slightly
            }

            Thread.Sleep(1000); // Ensure processing time

            Connection connection = new Connection("127.0.0.1", port, apciParameters, parameters);

            connection.SetASDUReceivedHandler(EventQueue1_asduReceivedHandler, info);

            connection.Connect();

            connection.SendStartDT();

            Thread.Sleep(500);

            connection.SendStopDT();

            connection.Close();

            Assert.AreEqual(15, info.lastScaledValue);

            info.asduHandlerCalled = 0;
            info.spontCount = 0;

            connection.Connect();

            connection.SendStartDT();

            Thread.Sleep(500);

            for (int i = 0; i < 5; i++)
            {
                Thread.Sleep(10);

                ASDU newASDU = new ASDU(server.GetApplicationLayerParameters(), CauseOfTransmission.SPONTANEOUS, false, false, 0, 1, false);

                info.asduHandlerCalled++;

                info.spontCount++;

                info.lastScaledValue++;

                newASDU.AddInformationObject(new MeasuredValueScaled(110, info.lastScaledValue, new QualityDescriptor()));

                Console.WriteLine($"[Test] Enqueuing ASDU with ScaledValue: {info.lastScaledValue}");

                server.EnqueueASDU(newASDU);
            }

            Thread.Sleep(500);

            connection.SendStopDT();

            Thread.Sleep(5000);

            connection.Close();

            Assert.AreEqual(5, info.asduHandlerCalled);
            Assert.AreEqual(5, info.spontCount);
            Assert.AreEqual(20, info.lastScaledValue);

            server.Stop();
            server = null;
        }

        [Test()]
        //[Ignore("Ignore to save execution time")]
        public void TestSendIMessageAfterStopDT()
        {
            ApplicationLayerParameters parameters = new ApplicationLayerParameters();
            APCIParameters apciParameters = new APCIParameters();

            Server server = new Server(apciParameters, parameters);

            int port = GetPort();
            server.SetLocalPort(port);

            server.Start();

            Connection connection = new Connection("127.0.0.1", port, apciParameters, parameters);

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


            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Assert.AreEqual(107, ((SocketException)se.InnerException).ErrorCode);
            }

            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.AreEqual(10057, ((SocketException)se.InnerException).ErrorCode);
            }

            server.Stop();
        }

        [Test()]
        //[Ignore("Ignore to save execution time")]
        public void TestConnectSameConnectionMultipleTimes()
        {
            ApplicationLayerParameters parameters = new ApplicationLayerParameters();
            APCIParameters apciParameters = new APCIParameters();

            Server server = new Server(apciParameters, parameters);
            int port = GetPort();
            server.SetLocalPort(port);

            server.Start();

            Connection connection = new Connection("127.0.0.1", port, apciParameters, parameters);

            SocketException se = null;

            try
            {
                connection.Connect();

                connection.Close();
            }
            catch (SocketException ex)
            {
                se = ex;
            }

            Assert.IsNull(se);

            try
            {
                connection.Connect();

                connection.Close();
            }
            catch (SocketException ex)
            {
                se = ex;
            }

            Assert.Null(se);

            connection.Close();

            server.Stop();
        }

        [Test()]
        //[Ignore("Ignore to save execution time")]
        public void TestConnectSameConnectionMultipleTimesServerDisconnects()
        {
            ApplicationLayerParameters parameters = new ApplicationLayerParameters();
            APCIParameters apciParameters = new APCIParameters();

            Server server = new Server(apciParameters, parameters);

            server.DebugOutput = true;
            int port = GetPort();
            server.SetLocalPort(port);

            server.Start();

            Connection connection = new Connection("127.0.0.1", port, apciParameters, parameters);

            connection.DebugOutput = true;

            for (int i = 0; i < 3; i++)
            {
                ConnectionException se = null;

                connection.Connect();

                Thread.Sleep(1000);

                server.Stop();

                Thread.Sleep(1000);

                try
                {
                    connection.SendStartDT();

                    ASDU newASDU = new ASDU(server.GetApplicationLayerParameters(), CauseOfTransmission.SPONTANEOUS, false, false, 0, 1, false);

                    newASDU.AddInformationObject(new MeasuredValueShort(1001, 0.1f, QualityDescriptor.INVALID()));

                    server.EnqueueASDU(newASDU);

                    newASDU = new ASDU(server.GetApplicationLayerParameters(), CauseOfTransmission.SPONTANEOUS, false, false, 0, 1, false);

                    newASDU.AddInformationObject(new MeasuredValueShort(1001, 0.2f, QualityDescriptor.INVALID()));

                    server.EnqueueASDU(newASDU);

                    Thread.Sleep(1000);

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

        [Test()]
        public void TestCS104ConnectionUseAfterClose()
        {
            ApplicationLayerParameters parameters = new ApplicationLayerParameters();
            APCIParameters apciParameters = new APCIParameters();

            Server server = new Server(apciParameters, parameters);

            Assert.NotNull(server);

            server.ServerMode = ServerMode.SINGLE_REDUNDANCY_GROUP;

            int port = GetPort();
            server.SetLocalPort(port);

            server.Start();

            Connection con = new Connection("127.0.0.1", port);

            Assert.NotNull(con);

            con.Connect();

            con.Close();

            try
            {
                con.SendInterrogationCommand(CauseOfTransmission.ACTIVATION, 1, 20);
                Assert.Fail("Expected ConnectionException was not thrown.");
            }
            catch (ConnectionException ex)
            {
                Console.WriteLine("Expected exception caught: " + ex.Message);
            }

            server.Stop();

        }

        [Test()]
        public void TestCS104ConnectionUseAfterServerCloseConnection()
        {
            ApplicationLayerParameters parameters = new ApplicationLayerParameters();
            APCIParameters apciParameters = new APCIParameters();

            Server server = new Server(apciParameters, parameters);

            Assert.NotNull(server);

            server.ServerMode = ServerMode.SINGLE_REDUNDANCY_GROUP;

            int port = GetPort();

            server.SetLocalPort(port);

            server.Start();

            Connection con = new Connection("127.0.0.1", port);

            Assert.NotNull(con);

            con.Connect();

            server.Stop();

            /* wait to allow client side to detect connection loss */
            Thread.Sleep(500);

            try
            {
                con.SendInterrogationCommand(CauseOfTransmission.ACTIVATION, 1, 20);
                Assert.Fail("Expected ConnectionException was not thrown.");
            }
            catch (ConnectionException ex)
            {
                Console.WriteLine("Expected exception: " + ex.Message);
            }

            con.Close();

        }

        [Test()]
        public void TestASDUAddInformationObjects()
        {
            ApplicationLayerParameters cp = new ApplicationLayerParameters();

            ASDU asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);

            asdu.AddInformationObject(new SinglePointInformation(100, false, new QualityDescriptor()));
            asdu.AddInformationObject(new SinglePointInformation(101, false, new QualityDescriptor()));

            // wrong InformationObject type expect exception
            ArgumentException ae = null;

            try
            {
                asdu.AddInformationObject(new DoublePointInformation(102, DoublePointValue.ON, new QualityDescriptor()));
            }
            catch (ArgumentException e)
            {
                ae = e;
            }

            Assert.NotNull(ae);
        }

        [Test()]
        public void TestASDUAddTooMuchInformationObjects()
        {
            ApplicationLayerParameters cp = new ApplicationLayerParameters();

            ASDU asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);

            int addedCounter = 0;
            int ioa = 100;

            while (asdu.AddInformationObject(new SinglePointInformation(ioa, false, new QualityDescriptor())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(60, addedCounter);

            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, true);

            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new SinglePointInformation(ioa, false, new QualityDescriptor())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(127, addedCounter);

            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);

            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new SinglePointWithCP24Time2a(ioa, false, new QualityDescriptor(), new CP24Time2a())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(34, addedCounter);

            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, true);

            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new SinglePointWithCP56Time2a(ioa, false, new QualityDescriptor(), new CP56Time2a())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(30, addedCounter);

            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);

            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new MeasuredValueShortWithCP56Time2a(ioa, 0.0f, QualityDescriptor.VALID(), new CP56Time2a())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(16, addedCounter);
        }

        [Test()]
        public void TestASDUAddInformationObjectsInWrongOrderToSequence()
        {
            ApplicationLayerParameters cp = new ApplicationLayerParameters();

            ASDU asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, true);

            bool encoded = asdu.AddInformationObject(new SinglePointInformation(100, false, new QualityDescriptor()));

            Assert.IsTrue(encoded);

            encoded = asdu.AddInformationObject(new SinglePointInformation(101, false, new QualityDescriptor()));

            Assert.IsTrue(encoded);

            encoded = asdu.AddInformationObject(new SinglePointInformation(102, false, new QualityDescriptor()));

            Assert.IsTrue(encoded);

            encoded = asdu.AddInformationObject(new SinglePointInformation(104, false, new QualityDescriptor()));

            Assert.IsFalse(encoded);

            encoded = asdu.AddInformationObject(new SinglePointInformation(102, false, new QualityDescriptor()));

            Assert.IsFalse(encoded);

            Assert.AreEqual(3, asdu.NumberOfElements);
        }



        [Test()]
        public void TestEncodeASDUsWithManyInformationObjects()
        {
            ApplicationLayerParameters cp = new ApplicationLayerParameters();

            ASDU asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            int addedCounter = 0;
            int ioa = 100;

            while (asdu.AddInformationObject(new SinglePointInformation(ioa, false, new QualityDescriptor())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(60, addedCounter);
            Assert.NotNull(asdu.AsByteArray());


            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new SinglePointWithCP24Time2a(ioa, true, new QualityDescriptor(), new CP24Time2a())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(34, addedCounter);
            Assert.NotNull(asdu.AsByteArray());


            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new SinglePointWithCP56Time2a(ioa, true, new QualityDescriptor(), new CP56Time2a())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(22, addedCounter);
            Assert.NotNull(asdu.AsByteArray());


            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new DoublePointInformation(ioa, DoublePointValue.ON, new QualityDescriptor())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(60, addedCounter);
            Assert.NotNull(asdu.AsByteArray());


            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new DoublePointWithCP24Time2a(ioa, DoublePointValue.ON, new QualityDescriptor(), new CP24Time2a())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(34, addedCounter);
            Assert.NotNull(asdu.AsByteArray());


            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new DoublePointWithCP56Time2a(ioa, DoublePointValue.ON, new QualityDescriptor(), new CP56Time2a())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(22, addedCounter);
            Assert.NotNull(asdu.AsByteArray());


            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new MeasuredValueNormalized(ioa, 1f, new QualityDescriptor())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(40, addedCounter);
            Assert.NotNull(asdu.AsByteArray());


            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new MeasuredValueNormalizedWithCP24Time2a(ioa, 1f, new QualityDescriptor(), new CP24Time2a())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(27, addedCounter);
            Assert.NotNull(asdu.AsByteArray());


            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new MeasuredValueNormalizedWithCP56Time2a(ioa, 1f, new QualityDescriptor(), new CP56Time2a())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(18, addedCounter);
            Assert.NotNull(asdu.AsByteArray());


            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new MeasuredValueScaled(ioa, 0, new QualityDescriptor())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(40, addedCounter);
            Assert.NotNull(asdu.AsByteArray());


            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new MeasuredValueScaledWithCP24Time2a(ioa, 0, new QualityDescriptor(), new CP24Time2a())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(27, addedCounter);
            Assert.NotNull(asdu.AsByteArray());


            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new MeasuredValueScaledWithCP56Time2a(ioa, 0, new QualityDescriptor(), new CP56Time2a())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(18, addedCounter);
            Assert.NotNull(asdu.AsByteArray());


            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new MeasuredValueShort(ioa, 0f, new QualityDescriptor())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(30, addedCounter);
            Assert.NotNull(asdu.AsByteArray());


            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new MeasuredValueShortWithCP24Time2a(ioa, 0f, new QualityDescriptor(), new CP24Time2a())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(22, addedCounter);
            Assert.NotNull(asdu.AsByteArray());


            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new MeasuredValueShortWithCP56Time2a(ioa, 0f, new QualityDescriptor(), new CP56Time2a())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(16, addedCounter);
            Assert.NotNull(asdu.AsByteArray());


            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new StepPositionInformation(ioa, 0, false, new QualityDescriptor())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(48, addedCounter);
            Assert.NotNull(asdu.AsByteArray());


            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new StepPositionWithCP24Time2a(ioa, 0, false, new QualityDescriptor(), new CP24Time2a())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(30, addedCounter);
            Assert.NotNull(asdu.AsByteArray());


            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new StepPositionWithCP56Time2a(ioa, 0, false, new QualityDescriptor(), new CP56Time2a())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(20, addedCounter);
            Assert.NotNull(asdu.AsByteArray());


            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new IntegratedTotals(ioa, new BinaryCounterReading())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(30, addedCounter);
            Assert.NotNull(asdu.AsByteArray());


            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new IntegratedTotalsWithCP24Time2a(ioa, new BinaryCounterReading(), new CP24Time2a())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(22, addedCounter);
            Assert.NotNull(asdu.AsByteArray());


            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new IntegratedTotalsWithCP56Time2a(ioa, new BinaryCounterReading(), new CP56Time2a())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(16, addedCounter);
            Assert.NotNull(asdu.AsByteArray());

            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new EventOfProtectionEquipment(ioa, new SingleEvent(), new CP16Time2a(10), new CP24Time2a())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(27, addedCounter);
            Assert.NotNull(asdu.AsByteArray());


            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new EventOfProtectionEquipmentWithCP56Time2a(ioa, new SingleEvent(), new CP16Time2a(10), new CP56Time2a())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(18, addedCounter);
            Assert.NotNull(asdu.AsByteArray());


            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new PackedSinglePointWithSCD(ioa, new StatusAndStatusChangeDetection(), new QualityDescriptor())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(30, addedCounter);
            Assert.NotNull(asdu.AsByteArray());

            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new PackedOutputCircuitInfo(ioa, new OutputCircuitInfo(), new QualityDescriptorP(), new CP16Time2a(10), new CP24Time2a())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(24, addedCounter);
            Assert.NotNull(asdu.AsByteArray());

            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new PackedOutputCircuitInfoWithCP56Time2a(ioa, new OutputCircuitInfo(), new QualityDescriptorP(), new CP16Time2a(10), new CP56Time2a())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(17, addedCounter);
            Assert.NotNull(asdu.AsByteArray());

            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new PackedStartEventsOfProtectionEquipment(ioa, new StartEvent(), new QualityDescriptorP(), new CP16Time2a(10), new CP24Time2a())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(24, addedCounter);
            Assert.NotNull(asdu.AsByteArray());


            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new PackedStartEventsOfProtectionEquipmentWithCP56Time2a(ioa, new StartEvent(), new QualityDescriptorP(), new CP16Time2a(10), new CP56Time2a())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(17, addedCounter);
            Assert.NotNull(asdu.AsByteArray());
            //TODO add missing tests
        }

        [Test()]
        public void TestEncodeASDUsWithManyInformationObjectsSequenceOfIO()
        {

            ApplicationLayerParameters cp = new ApplicationLayerParameters();

            ASDU asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, true);
            int addedCounter = 0;
            int ioa = 100;

            while (asdu.AddInformationObject(new SinglePointInformation(ioa, false, new QualityDescriptor())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(127, addedCounter);
            Assert.NotNull(asdu.AsByteArray());


            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, true);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new DoublePointInformation(ioa, DoublePointValue.OFF, new QualityDescriptor())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(127, addedCounter);
            Assert.NotNull(asdu.AsByteArray());


            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, true);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new MeasuredValueNormalized(ioa, 1f, new QualityDescriptor())))
            {
                ioa++;
                addedCounter++;
            }


            Assert.AreEqual(80, addedCounter);
            Assert.NotNull(asdu.AsByteArray());

            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, true);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new MeasuredValueScaled(ioa, 0, new QualityDescriptor())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(80, addedCounter);
            Assert.NotNull(asdu.AsByteArray());


            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, true);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new MeasuredValueShort(ioa, 0f, new QualityDescriptor())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(48, addedCounter);
            Assert.NotNull(asdu.AsByteArray());


            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, true);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new StepPositionInformation(ioa, 0, false, new QualityDescriptor())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(120, addedCounter);
            Assert.NotNull(asdu.AsByteArray());


            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, true);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new IntegratedTotals(ioa, new BinaryCounterReading())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(48, addedCounter);
            Assert.NotNull(asdu.AsByteArray());


            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, true);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new PackedSinglePointWithSCD(ioa, new StatusAndStatusChangeDetection(), new QualityDescriptor())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(48, addedCounter);
            Assert.NotNull(asdu.AsByteArray());


            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, true);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new PackedOutputCircuitInfo(ioa, new OutputCircuitInfo(), new QualityDescriptorP(), new CP16Time2a(10), new CP24Time2a())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(34, addedCounter);
            Assert.NotNull(asdu.AsByteArray());

            asdu = new ASDU(cp, CauseOfTransmission.PERIODIC, false, false, 0, 1, true);
            addedCounter = 0;
            ioa = 100;

            while (asdu.AddInformationObject(new PackedStartEventsOfProtectionEquipment(ioa, new StartEvent(), new QualityDescriptorP(), new CP16Time2a(0), new CP24Time2a())))
            {
                ioa++;
                addedCounter++;
            }

            Assert.AreEqual(34, addedCounter);
            Assert.NotNull(asdu.AsByteArray());

        }

        [Test()]
        //[Ignore("Ignore to save execution time")]
        public void TestSendTestFR()
        {
            ApplicationLayerParameters clientParameters = new ApplicationLayerParameters();
            APCIParameters clientApciParamters = new APCIParameters();
            ApplicationLayerParameters serverParameters = new ApplicationLayerParameters();
            APCIParameters serverApciParamters = new APCIParameters();

            clientApciParamters.T3 = 1;

            Server server = new Server(serverApciParamters, serverParameters);
            int port = GetPort();
            server.SetLocalPort(port);
            server.DebugOutput = true;
            server.Start();

            Connection connection = new Connection("127.0.0.1", port, clientApciParamters, clientParameters);

            connection.Connect();

            connection.DebugOutput = true;
            connection.SetReceivedRawMessageHandler(testSendTestFRTimeoutMasterRawMessageHandler, null);

            ASDU asdu = new ASDU(clientParameters, CauseOfTransmission.SPONTANEOUS, false, false, 0, 1, false);
            asdu.AddInformationObject(new SinglePointInformation(100, false, new QualityDescriptor()));

            Assert.AreEqual(1, connection.GetStatistics().SentMsgCounter); /* STARTDT + ASDU */

            while (connection.GetStatistics().RcvdMsgCounter < 2)
                Thread.Sleep(1);

            Assert.AreEqual(2, connection.GetStatistics().RcvdMsgCounter); /* STARTDT_CON + ASDU */

            Thread.Sleep(6000);

            Assert.IsFalse(connection.IsRunning);

            try
            {
                connection.SendASDU(asdu);
            }
            catch (ConnectionException)
            {
            }


            while (connection.IsRunning == true)
                Thread.Sleep(10);

            connection.Close();
            server.Stop();

            Assert.AreEqual(4, connection.GetStatistics().RcvdMsgCounter); /* STARTDT_CON + ASDU + TESTFR_CON */

            Assert.AreEqual(0, connection.GetStatistics().RcvdTestFrConCounter);
        }

        private static bool testSendTestFRTimeoutMasterRawMessageHandler(object param, byte[] msg, int msgSize)
        {
            // intercept TESTFR_CON message
            if ((msgSize == 6) && (msg[2] == 0x83))
                return false;
            else
                return true;
        }

        /// <summary>
        /// This test checks that the connection will be closed when the master
        /// doesn't receive the TESTFR_CON messages
        /// </summary>
        [Test()]
        //[Ignore("Ignore to save execution time")]
        public void TestSendTestFRTimeoutMaster()
        {
            ApplicationLayerParameters clientParameters = new ApplicationLayerParameters();
            APCIParameters clientApciParamters = new APCIParameters();
            ApplicationLayerParameters serverParameters = new ApplicationLayerParameters();
            APCIParameters serverApciParamters = new APCIParameters();

            clientApciParamters.T3 = 1;

            Server server = new Server(serverApciParamters, serverParameters);
            int port = GetPort();
            server.SetLocalPort(port);

            server.Start();

            Connection connection = new Connection("127.0.0.1", port, clientApciParamters, clientParameters);

            connection.Connect();

            connection.DebugOutput = true;
            connection.SetReceivedRawMessageHandler(testSendTestFRTimeoutMasterRawMessageHandler, null);

            ASDU asdu = new ASDU(clientParameters, CauseOfTransmission.SPONTANEOUS, false, false, 0, 1, false);
            asdu.AddInformationObject(new SinglePointInformation(100, false, new QualityDescriptor()));

            Assert.AreEqual(1, connection.GetStatistics().SentMsgCounter); /* STARTDT + ASDU */

            while (connection.GetStatistics().RcvdMsgCounter < 2)
                Thread.Sleep(1);

            Assert.AreEqual(2, connection.GetStatistics().RcvdMsgCounter); /* STARTDT_CON + ASDU */

            Thread.Sleep(6000);

            // Expect connection to be closed due to three missing TESTFR_CON responses
            Assert.IsFalse(connection.IsRunning);

            // Connection is closed. SendASDU should fail
            try
            {
                connection.SendASDU(asdu);
            }
            catch (ConnectionException)
            {
            }


            while (connection.IsRunning == true)
                Thread.Sleep(10);

            connection.Close();
            server.Stop();

            Assert.AreEqual(4, connection.GetStatistics().RcvdMsgCounter); /* STARTDT_CON + ASDU + TESTFR_CON */

            Assert.AreEqual(0, connection.GetStatistics().RcvdTestFrConCounter);
        }

        private static bool testSendTestFRTimeoutSlaveRawMessageHandler(object param, byte[] msg, int msgSize)
        {
            // intercept TESTFR_ACT messages for so that the master doesn't response
            if ((msgSize == 6) && (msg[2] == 0x43))
                return false;
            else
                return true;
        }

        /// <summary>
        /// This test checks that the connection will be closed when the master
        /// doesn't send the TESTFR_CON messages
        /// </summary>
        [Test()]
        //[Ignore("Ignore to save execution time")]
        public void TestSendTestFRTimeoutSlave()
        {
            ApplicationLayerParameters clientParameters = new ApplicationLayerParameters();
            APCIParameters clientApciParamters = new APCIParameters();
            ApplicationLayerParameters serverParameters = new ApplicationLayerParameters();
            APCIParameters serverApciParamters = new APCIParameters();

            serverApciParamters.T3 = 1;

            Server server = new Server(serverApciParamters, serverParameters);
            int port = GetPort();
            server.SetLocalPort(port);

            server.Start();

            Connection connection = new Connection("127.0.0.1", port, clientApciParamters, clientParameters);

            connection.DebugOutput = true;
            connection.SetReceivedRawMessageHandler(testSendTestFRTimeoutSlaveRawMessageHandler, null);

            connection.Connect();

            Assert.AreEqual(1, connection.GetStatistics().SentMsgCounter); /* STARTDT */

            while (connection.GetStatistics().RcvdMsgCounter < 1)
                Thread.Sleep(1);

            Assert.AreEqual(1, connection.GetStatistics().RcvdMsgCounter); /* STARTDT_CON */

            Thread.Sleep(6000);


            // Connection is closed. SendASDU should fail
            try
            {
                ASDU asdu = new ASDU(clientParameters, CauseOfTransmission.SPONTANEOUS, false, false, 0, 1, false);
                asdu.AddInformationObject(new SinglePointInformation(100, false, new QualityDescriptor()));

                connection.SendASDU(asdu);
            }
            catch (ConnectionException)
            {
            }


            while (connection.IsRunning == true)
                Thread.Sleep(10);

            connection.Close();
            server.Stop();

            //    Assert.AreEqual (5, connection.GetStatistics ().RcvdMsgCounter); /* STARTDT_CON + ASDU + TESTFR_CON */

            //    Assert.AreEqual (0, connection.GetStatistics ().RcvdTestFrConCounter);
        }


        [Test()]
        public void TestEncodeDecodeSetpointCommandNormalized()
        {
            Server server = new Server();
            int port = GetPort();

            server.SetLocalPort(port);

            float recvValue = 0f;
            float sendValue = 1.0f;
            bool hasReceived = false;

            server.SetASDUHandler(delegate (object parameter, IMasterConnection con, ASDU asdu)
            {

                if (asdu.TypeId == TypeID.C_SE_NA_1)
                {
                    SetpointCommandNormalized spn = (SetpointCommandNormalized)asdu.GetElement(0);

                    recvValue = spn.NormalizedValue;
                    hasReceived = true;
                }

                return true;
            }, null);
            server.Start();

            Connection connection = new Connection("127.0.0.1", port);
            connection.Connect();

            ASDU newAsdu = new ASDU(server.GetApplicationLayerParameters(), CauseOfTransmission.ACTIVATION, false, false, 0, 1, false);
            newAsdu.AddInformationObject(new SetpointCommandNormalized(100, sendValue, new SetpointCommandQualifier(false, 0)));

            connection.SendASDU(newAsdu);

            while (hasReceived == false)
                Thread.Sleep(50);

            connection.Close();
            server.Stop();

            Assert.AreEqual(sendValue, recvValue, 0.001);
        }

        [Test()]
        public void TestEncodeDecodePrivateInformationObject()
        {
            Server server = new Server();
            int port = GetPort();

            server.SetLocalPort(port);

            server.DebugOutput = true;

            int recvValue = 0;
            int sendValue = 12345;
            bool hasReceived = false;

            PrivateInformationObjectTypes privateObjects = new PrivateInformationObjectTypes();
            privateObjects.AddPrivateInformationObjectType((TypeID)41, new TestInteger32Object());

            server.SetASDUHandler(delegate (object parameter, IMasterConnection con, ASDU asdu)
            {

                if (asdu.TypeId == (TypeID)41)
                {

                    TestInteger32Object spn = (TestInteger32Object)asdu.GetElement(0, privateObjects);

                    recvValue = spn.Value;
                    hasReceived = true;
                }

                return true;
            }, null);

            server.Start();

            Connection connection = new Connection("127.0.0.1", port);
            connection.Connect();

            ASDU newAsdu = new ASDU(server.GetApplicationLayerParameters(), CauseOfTransmission.ACTIVATION, false, false, 0, 1, false);

            newAsdu.AddInformationObject(new TestInteger32Object(100, sendValue));

            connection.SendASDU(newAsdu);

            while (hasReceived == false)
                Thread.Sleep(50);

            connection.Close();
            server.Stop();

            Assert.AreEqual(sendValue, recvValue);

        }

        [Test()]
        public void TestDoubleCommand()
        {
            DoubleCommand dc = new DoubleCommand(10001, 2, false, 12);

            Assert.AreEqual(10001, dc.ObjectAddress);
            Assert.AreEqual(2, dc.State);
            Assert.AreEqual(false, dc.Select);
            Assert.AreEqual(12, dc.QU);

            dc = new DoubleCommand(10001, 2, false, 3);

            Assert.AreEqual(10001, dc.ObjectAddress);
            Assert.AreEqual(2, dc.State);
            Assert.AreEqual(false, dc.Select);
            Assert.AreEqual(3, dc.QU);

        }

        [Test()]
        public void TestDoubleCommandWithCP56Time2a()
        {
            DateTime dateTime = DateTime.UtcNow;

            CP56Time2a time = new CP56Time2a(dateTime);

            DoubleCommandWithCP56Time2a dc = new DoubleCommandWithCP56Time2a(10001, 2, false, 12, time);

            Assert.AreEqual(10001, dc.ObjectAddress);
            Assert.AreEqual(2, dc.State);
            Assert.AreEqual(false, dc.Select);
            Assert.AreEqual(12, dc.QU);
            Assert.AreEqual(time.Year, dc.Timestamp.Year);
            Assert.AreEqual(time.Month, dc.Timestamp.Month);
            Assert.AreEqual(time.DayOfMonth, dc.Timestamp.DayOfMonth);
            Assert.AreEqual(time.Minute, dc.Timestamp.Minute);
            Assert.AreEqual(time.Second, dc.Timestamp.Second);
            Assert.AreEqual(time.Millisecond, dc.Timestamp.Millisecond);

            dc = new DoubleCommandWithCP56Time2a(10001, 2, false, 3, time);

            Assert.AreEqual(10001, dc.ObjectAddress);
            Assert.AreEqual(2, dc.State);
            Assert.AreEqual(false, dc.Select);
            Assert.AreEqual(3, dc.QU);
            Assert.AreEqual(time.Year, dc.Timestamp.Year);
            Assert.AreEqual(time.Month, dc.Timestamp.Month);
            Assert.AreEqual(time.DayOfMonth, dc.Timestamp.DayOfMonth);
            Assert.AreEqual(time.Minute, dc.Timestamp.Minute);
            Assert.AreEqual(time.Second, dc.Timestamp.Second);
            Assert.AreEqual(time.Millisecond, dc.Timestamp.Millisecond);
        }



        [Test()]
        public void TestStepCommandValue()
        {
            StepCommand scv = new StepCommand(10001, StepCommandValue.INVALID_0, false, 10);

            Assert.AreEqual(10001, scv.ObjectAddress);
            Assert.AreEqual(StepCommandValue.INVALID_0, scv.State);
            Assert.AreEqual(false, scv.Select);
            Assert.AreEqual(10, scv.QU);

            scv = new StepCommand(10002, StepCommandValue.HIGHER, true, 3);

            Assert.AreEqual(10002, scv.ObjectAddress);
            Assert.AreEqual(StepCommandValue.HIGHER, scv.State);
            Assert.AreEqual(true, scv.Select);
            Assert.AreEqual(3, scv.QU);

        }

        [Test()]
        public void TestStepCommandValueWithCP56Time2a()
        {
            DateTime dateTime = DateTime.UtcNow;

            CP56Time2a time = new CP56Time2a(dateTime);

            StepCommandWithCP56Time2a scv = new StepCommandWithCP56Time2a(10001, StepCommandValue.INVALID_0, false, 12, time);

            Assert.AreEqual(10001, scv.ObjectAddress);
            Assert.AreEqual(StepCommandValue.INVALID_0, scv.State);
            Assert.AreEqual(false, scv.Select);
            Assert.AreEqual(12, scv.QU);
            Assert.AreEqual(time.Year, scv.Timestamp.Year);
            Assert.AreEqual(time.Month, scv.Timestamp.Month);
            Assert.AreEqual(time.DayOfMonth, scv.Timestamp.DayOfMonth);
            Assert.AreEqual(time.Minute, scv.Timestamp.Minute);
            Assert.AreEqual(time.Second, scv.Timestamp.Second);
            Assert.AreEqual(time.Millisecond, scv.Timestamp.Millisecond);

            scv = new StepCommandWithCP56Time2a(10002, StepCommandValue.HIGHER, true, 3, time);

            Assert.AreEqual(10002, scv.ObjectAddress);
            Assert.AreEqual(StepCommandValue.HIGHER, scv.State);
            Assert.AreEqual(true, scv.Select);
            Assert.AreEqual(3, scv.QU);
            Assert.AreEqual(time.Year, scv.Timestamp.Year);
            Assert.AreEqual(time.Month, scv.Timestamp.Month);
            Assert.AreEqual(time.DayOfMonth, scv.Timestamp.DayOfMonth);
            Assert.AreEqual(time.Minute, scv.Timestamp.Minute);
            Assert.AreEqual(time.Second, scv.Timestamp.Second);
            Assert.AreEqual(time.Millisecond, scv.Timestamp.Millisecond);
        }


        [Test()]
        public void TestSingleCommand()
        {
            SingleCommand sc = new SingleCommand(10002, true, false, 12);

            Assert.AreEqual(10002, sc.ObjectAddress);
            Assert.AreEqual(true, sc.State);
            Assert.AreEqual(false, sc.Select);
            Assert.AreEqual(12, sc.QU);

            sc = new SingleCommand(10002, false, true, 3);

            Assert.AreEqual(10002, sc.ObjectAddress);
            Assert.AreEqual(false, sc.State);
            Assert.AreEqual(true, sc.Select);
            Assert.AreEqual(3, sc.QU);

            sc.QU = 17;

            Assert.AreEqual(17, sc.QU);
            Assert.AreEqual(false, sc.State);
            Assert.AreEqual(true, sc.Select);
        }

        [Test()]
        public void TestSingleCommandWithCP56Time2a()
        {
            DateTime dateTime = DateTime.UtcNow;

            CP56Time2a time = new CP56Time2a(dateTime);

            SingleCommandWithCP56Time2a sc = new SingleCommandWithCP56Time2a(10002, true, false, 12, time);

            Assert.AreEqual(10002, sc.ObjectAddress);
            Assert.AreEqual(true, sc.State);
            Assert.AreEqual(false, sc.Select);
            Assert.AreEqual(12, sc.QU);

            sc = new SingleCommandWithCP56Time2a(10002, false, true, 3, time);

            Assert.AreEqual(10002, sc.ObjectAddress);
            Assert.AreEqual(false, sc.State);
            Assert.AreEqual(true, sc.Select);
            Assert.AreEqual(3, sc.QU);
            Assert.AreEqual(time.Year, sc.Timestamp.Year);
            Assert.AreEqual(time.Month, sc.Timestamp.Month);
            Assert.AreEqual(time.DayOfMonth, sc.Timestamp.DayOfMonth);
            Assert.AreEqual(time.Minute, sc.Timestamp.Minute);
            Assert.AreEqual(time.Second, sc.Timestamp.Second);
            Assert.AreEqual(time.Millisecond, sc.Timestamp.Millisecond);

            sc.QU = 17;

            Assert.AreEqual(17, sc.QU);
            Assert.AreEqual(false, sc.State);
            Assert.AreEqual(true, sc.Select);
            Assert.AreEqual(time.Year, sc.Timestamp.Year);
            Assert.AreEqual(time.Month, sc.Timestamp.Month);
            Assert.AreEqual(time.DayOfMonth, sc.Timestamp.DayOfMonth);
            Assert.AreEqual(time.Minute, sc.Timestamp.Minute);
            Assert.AreEqual(time.Second, sc.Timestamp.Second);
            Assert.AreEqual(time.Millisecond, sc.Timestamp.Millisecond);
        }

        [Test()]
        public void TestSinglePointInformationClientServer()
        {
            SinglePointInformation spi = new SinglePointInformation(101, true, new QualityDescriptor());

            ASDU newAsdu = new ASDU(new ApplicationLayerParameters(), CauseOfTransmission.PERIODIC,
                false, false, 0, 1, false);

            newAsdu.AddInformationObject(spi);

            Server server = new Server();
            int port = GetPort();

            server.SetLocalPort(port);

            bool hasReceived = false;

            server.SetASDUHandler(delegate (object parameter, IMasterConnection con, ASDU asdu)
            {

                if (asdu.TypeId == TypeID.M_SP_NA_1)
                {

                    SinglePointInformation spn = (SinglePointInformation)asdu.GetElement(0);

                    Assert.AreEqual(spi.Value, spn.Value);
                    hasReceived = true;
                }

                return true;
            }, null);

            server.Start();

            Connection connection = new Connection("127.0.0.1", port);
            connection.Connect();

            connection.SendASDU(newAsdu);

            while (hasReceived == false)
                Thread.Sleep(50);

            connection.Close();
            server.Stop();
        }

        [Test()]
        public void TestIntegratedTotals()
        {
            byte[] buffer = new byte[257];

            BufferFrame bf = new BufferFrame(buffer, 0);

            ApplicationLayerParameters alParameters = new ApplicationLayerParameters();

            BinaryCounterReading bcr = new BinaryCounterReading();

            IntegratedTotals it = new IntegratedTotals(101, bcr);

            it.Encode(bf, alParameters, true);
            Assert.AreEqual(5, bf.GetMsgSize());

            bf.ResetFrame();

            it.Encode(bf, alParameters, false);

            Assert.AreEqual(alParameters.SizeOfIOA + it.GetEncodedSize(), bf.GetMsgSize());
            Assert.AreEqual(8, bf.GetMsgSize());

            IntegratedTotals it2 = new IntegratedTotals(alParameters, buffer, 0, false);

            Assert.AreEqual(101, it2.ObjectAddress);
            Assert.AreEqual(0, it2.BCR.Value);

        }

        [Test()]
        public void TestIntegratedTotalsWithCp24Time2a()
        {
            byte[] buffer = new byte[257];

            BufferFrame bf = new BufferFrame(buffer, 0);

            ApplicationLayerParameters alParameters = new ApplicationLayerParameters();

            CP24Time2a time = new CP24Time2a(45, 23, 538);

            BinaryCounterReading bcr = new BinaryCounterReading();

            IntegratedTotalsWithCP24Time2a it = new IntegratedTotalsWithCP24Time2a(101, bcr, time);

            it.Encode(bf, alParameters, true);
            Assert.AreEqual(8, bf.GetMsgSize());

            bf.ResetFrame();

            it.Encode(bf, alParameters, false);

            Assert.AreEqual(alParameters.SizeOfIOA + it.GetEncodedSize(), bf.GetMsgSize());
            Assert.AreEqual(11, bf.GetMsgSize());

            IntegratedTotalsWithCP24Time2a it2 = new IntegratedTotalsWithCP24Time2a(alParameters, buffer, 0, false);

            Assert.AreEqual(101, it2.ObjectAddress);
            Assert.AreEqual(0, it2.BCR.Value);

            Assert.AreEqual(45, it2.Timestamp.Minute);
            Assert.AreEqual(23, it2.Timestamp.Second);
            Assert.AreEqual(538, it2.Timestamp.Millisecond);
        }

        [Test()]
        public void TestIntegratedTotalsWithCp56Time2a()
        {
            byte[] buffer = new byte[257];

            BufferFrame bf = new BufferFrame(buffer, 0);

            ApplicationLayerParameters alParameters = new ApplicationLayerParameters();

            DateTime dateTime = DateTime.UtcNow;

            CP56Time2a time = new CP56Time2a(dateTime);

            BinaryCounterReading bcr = new BinaryCounterReading();

            IntegratedTotalsWithCP56Time2a it = new IntegratedTotalsWithCP56Time2a(101, bcr, time);

            it.Encode(bf, alParameters, true);
            Assert.AreEqual(12, bf.GetMsgSize());

            bf.ResetFrame();

            it.Encode(bf, alParameters, false);

            Assert.AreEqual(alParameters.SizeOfIOA + it.GetEncodedSize(), bf.GetMsgSize());
            Assert.AreEqual(15, bf.GetMsgSize());

            IntegratedTotalsWithCP56Time2a it2 = new IntegratedTotalsWithCP56Time2a(alParameters, buffer, 0, false);

            Assert.AreEqual(101, it2.ObjectAddress);
            Assert.AreEqual(0, it2.BCR.Value);

            Assert.AreEqual(time.Year, it2.Timestamp.Year);
            Assert.AreEqual(time.Month, it2.Timestamp.Month);
            Assert.AreEqual(time.DayOfMonth, it2.Timestamp.DayOfMonth);
            Assert.AreEqual(time.Minute, it2.Timestamp.Minute);
            Assert.AreEqual(time.Second, it2.Timestamp.Second);
            Assert.AreEqual(time.Millisecond, it2.Timestamp.Millisecond);

        }


        [Test()]
        public void TestSinglePointInformation()
        {
            byte[] buffer = new byte[257];

            BufferFrame bf = new BufferFrame(buffer, 0);

            ApplicationLayerParameters alParameters = new ApplicationLayerParameters();

            SinglePointInformation spi = new SinglePointInformation(101, true, new QualityDescriptor());

            spi.Encode(bf, alParameters, true);
            Assert.AreEqual(1, bf.GetMsgSize());

            bf.ResetFrame();

            spi.Encode(bf, alParameters, false);

            Assert.AreEqual(alParameters.SizeOfIOA + spi.GetEncodedSize(), bf.GetMsgSize());
            Assert.AreEqual(4, bf.GetMsgSize());

            SinglePointInformation spi2 = new SinglePointInformation(alParameters, buffer, 0, false);

            Assert.AreEqual(101, spi2.ObjectAddress);
            Assert.AreEqual(true, spi2.Value);
        }

        [Test()]
        public void TestSinglePointInformationWithCp24Time2a()
        {
            byte[] buffer = new byte[257];

            BufferFrame bf = new BufferFrame(buffer, 0);

            ApplicationLayerParameters alParameters = new ApplicationLayerParameters();

            CP24Time2a time = new CP24Time2a(45, 23, 538);

            SinglePointWithCP24Time2a spi = new SinglePointWithCP24Time2a(102, false, new QualityDescriptor(), time);

            spi.Encode(bf, alParameters, false);

            Assert.AreEqual(alParameters.SizeOfIOA + spi.GetEncodedSize(), bf.GetMsgSize());
            Assert.AreEqual(7, bf.GetMsgSize());

            SinglePointWithCP24Time2a spi2 = new SinglePointWithCP24Time2a(alParameters, buffer, 0, false);

            Assert.AreEqual(102, spi2.ObjectAddress);
            Assert.AreEqual(false, spi2.Value);
            Assert.AreEqual(45, spi2.Timestamp.Minute);
            Assert.AreEqual(23, spi2.Timestamp.Second);
            Assert.AreEqual(538, spi2.Timestamp.Millisecond);
        }

        [Test()]
        public void TestSinglePointInformationWithCP56Time2a()
        {
            byte[] buffer = new byte[257];

            BufferFrame bf = new BufferFrame(buffer, 0);

            ApplicationLayerParameters alParameters = new ApplicationLayerParameters();

            DateTime dateTime = DateTime.UtcNow;

            CP56Time2a time = new CP56Time2a(dateTime);

            SinglePointWithCP56Time2a spi = new SinglePointWithCP56Time2a(103, true, new QualityDescriptor(), time);

            spi.Encode(bf, alParameters, false);

            Assert.AreEqual(alParameters.SizeOfIOA + spi.GetEncodedSize(), bf.GetMsgSize());
            Assert.AreEqual(11, bf.GetMsgSize());

            SinglePointWithCP56Time2a spi2 = new SinglePointWithCP56Time2a(alParameters, buffer, 0, false);

            Assert.AreEqual(103, spi2.ObjectAddress);
            Assert.AreEqual(true, spi2.Value);

            Assert.AreEqual(time.Year, spi2.Timestamp.Year);
            Assert.AreEqual(time.Month, spi2.Timestamp.Month);
            Assert.AreEqual(time.DayOfMonth, spi2.Timestamp.DayOfMonth);
            Assert.AreEqual(time.Minute, spi2.Timestamp.Minute);
            Assert.AreEqual(time.Second, spi2.Timestamp.Second);
            Assert.AreEqual(time.Millisecond, spi2.Timestamp.Millisecond);
        }

        [Test()]
        public void TestDoublePointInformation()
        {
            byte[] buffer = new byte[257];

            BufferFrame bf = new BufferFrame(buffer, 0);

            ApplicationLayerParameters alParameters = new ApplicationLayerParameters();

            DoublePointInformation dpi = new DoublePointInformation(101, DoublePointValue.OFF, new QualityDescriptor());

            dpi.Encode(bf, alParameters, true);
            Assert.AreEqual(1, bf.GetMsgSize());

            bf.ResetFrame();

            dpi.Encode(bf, alParameters, false);

            Assert.AreEqual(alParameters.SizeOfIOA + dpi.GetEncodedSize(), bf.GetMsgSize());
            Assert.AreEqual(4, bf.GetMsgSize());

            DoublePointInformation dpi2 = new DoublePointInformation(alParameters, buffer, 0, false);

            Assert.AreEqual(101, dpi2.ObjectAddress);
            Assert.AreEqual(DoublePointValue.OFF, dpi2.Value);
        }

        [Test()]
        public void TestDoublePointInformationWithCP24Time2a()
        {
            byte[] buffer = new byte[257];

            BufferFrame bf = new BufferFrame(buffer, 0);

            ApplicationLayerParameters alParameters = new ApplicationLayerParameters();

            CP24Time2a time = new CP24Time2a(45, 23, 538);

            DoublePointWithCP24Time2a dpi = new DoublePointWithCP24Time2a(101, DoublePointValue.ON, new QualityDescriptor(), time);

            dpi.Encode(bf, alParameters, true);
            Assert.AreEqual(4, bf.GetMsgSize());

            bf.ResetFrame();

            dpi.Encode(bf, alParameters, false);

            Assert.AreEqual(alParameters.SizeOfIOA + dpi.GetEncodedSize(), bf.GetMsgSize());
            Assert.AreEqual(7, bf.GetMsgSize());

            DoublePointWithCP24Time2a dpi2 = new DoublePointWithCP24Time2a(alParameters, buffer, 0, false);

            Assert.AreEqual(101, dpi2.ObjectAddress);
            Assert.AreEqual(DoublePointValue.ON, dpi2.Value);
            Assert.AreEqual(45, dpi2.Timestamp.Minute);
            Assert.AreEqual(23, dpi2.Timestamp.Second);
            Assert.AreEqual(538, dpi2.Timestamp.Millisecond);
        }

        [Test()]
        public void TestMeasuredValueShortWithCP24Time2a()
        {
            byte[] buffer = new byte[257];

            BufferFrame bf = new BufferFrame(buffer, 0);

            ApplicationLayerParameters alParameters = new ApplicationLayerParameters();

            CP24Time2a time = new CP24Time2a(45, 23, 538);

            MeasuredValueShortWithCP24Time2a mvs = new MeasuredValueShortWithCP24Time2a(101, 0, new QualityDescriptor(), time);

            mvs.Encode(bf, alParameters, true);
            Assert.AreEqual(8, bf.GetMsgSize());

            bf.ResetFrame();

            mvs.Encode(bf, alParameters, false);

            Assert.AreEqual(alParameters.SizeOfIOA + mvs.GetEncodedSize(), bf.GetMsgSize());
            Assert.AreEqual(11, bf.GetMsgSize());

            MeasuredValueShortWithCP24Time2a mvs2 = new MeasuredValueShortWithCP24Time2a(alParameters, buffer, 0, false);

            Assert.AreEqual(101, mvs2.ObjectAddress);
            Assert.AreEqual(0, mvs2.Value);
            Assert.AreEqual(45, mvs2.Timestamp.Minute);
            Assert.AreEqual(23, mvs2.Timestamp.Second);
            Assert.AreEqual(538, mvs2.Timestamp.Millisecond);
        }

        [Test()]
        public void TestMeasuredValueShortWithCP56Time2a()
        {
            byte[] buffer = new byte[257];

            BufferFrame bf = new BufferFrame(buffer, 0);

            ApplicationLayerParameters alParameters = new ApplicationLayerParameters();

            DateTime dateTime = DateTime.UtcNow;

            CP56Time2a time = new CP56Time2a(dateTime);

            MeasuredValueShortWithCP56Time2a mvs = new MeasuredValueShortWithCP56Time2a(101, 0, new QualityDescriptor(), time);

            mvs.Encode(bf, alParameters, true);
            Assert.AreEqual(12, bf.GetMsgSize());

            bf.ResetFrame();

            mvs.Encode(bf, alParameters, false);

            Assert.AreEqual(alParameters.SizeOfIOA + mvs.GetEncodedSize(), bf.GetMsgSize());
            Assert.AreEqual(15, bf.GetMsgSize());

            MeasuredValueShortWithCP56Time2a mvs2 = new MeasuredValueShortWithCP56Time2a(alParameters, buffer, 0, false);

            Assert.AreEqual(101, mvs2.ObjectAddress);
            Assert.AreEqual(0, mvs2.Value);
            Assert.AreEqual(time.Year, mvs2.Timestamp.Year);
            Assert.AreEqual(time.Month, mvs2.Timestamp.Month);
            Assert.AreEqual(time.DayOfMonth, mvs2.Timestamp.DayOfMonth);
            Assert.AreEqual(time.Minute, mvs2.Timestamp.Minute);
            Assert.AreEqual(time.Second, mvs2.Timestamp.Second);
            Assert.AreEqual(time.Millisecond, mvs2.Timestamp.Millisecond);
        }


        [Test()]
        public void TestMeasuredValueNormalizedWithCP24Time2a()
        {
            byte[] buffer = new byte[257];

            BufferFrame bf = new BufferFrame(buffer, 0);

            ApplicationLayerParameters alParameters = new ApplicationLayerParameters();

            CP24Time2a time = new CP24Time2a(45, 23, 538);

            MeasuredValueNormalizedWithCP24Time2a mvn = new MeasuredValueNormalizedWithCP24Time2a(201, 0.5f, new QualityDescriptor(), time);

            mvn.Encode(bf, alParameters, true);
            Assert.AreEqual(6, bf.GetMsgSize());

            bf.ResetFrame();

            mvn.Encode(bf, alParameters, false);

            Assert.AreEqual(alParameters.SizeOfIOA + mvn.GetEncodedSize(), bf.GetMsgSize());
            Assert.AreEqual(9, bf.GetMsgSize());

            MeasuredValueNormalizedWithCP24Time2a mvn2 = new MeasuredValueNormalizedWithCP24Time2a(alParameters, buffer, 0, false);

            Assert.AreEqual(201, mvn2.ObjectAddress);
            Assert.AreEqual(0.5f, mvn2.NormalizedValue, 0.001);
            Assert.AreEqual(45, mvn2.Timestamp.Minute);
            Assert.AreEqual(23, mvn2.Timestamp.Second);
            Assert.AreEqual(538, mvn2.Timestamp.Millisecond);
        }

        [Test()]
        public void TestMeasuredValueNormalizedWithCP56Time2a()
        {
            byte[] buffer = new byte[257];

            BufferFrame bf = new BufferFrame(buffer, 0);

            ApplicationLayerParameters alParameters = new ApplicationLayerParameters();

            DateTime dateTime = DateTime.UtcNow;

            CP56Time2a time = new CP56Time2a(dateTime);

            MeasuredValueNormalizedWithCP56Time2a mvn = new MeasuredValueNormalizedWithCP56Time2a(201, 0.5f, new QualityDescriptor(), time);

            mvn.Encode(bf, alParameters, true);
            Assert.AreEqual(10, bf.GetMsgSize());

            bf.ResetFrame();

            mvn.Encode(bf, alParameters, false);

            Assert.AreEqual(alParameters.SizeOfIOA + mvn.GetEncodedSize(), bf.GetMsgSize());
            Assert.AreEqual(13, bf.GetMsgSize());

            MeasuredValueNormalizedWithCP56Time2a mvn2 = new MeasuredValueNormalizedWithCP56Time2a(alParameters, buffer, 0, false);

            Assert.AreEqual(201, mvn2.ObjectAddress);
            Assert.AreEqual(0.5f, mvn2.NormalizedValue, 0.001);
            Assert.AreEqual(time.Year, mvn2.Timestamp.Year);
            Assert.AreEqual(time.Month, mvn2.Timestamp.Month);
            Assert.AreEqual(time.DayOfMonth, mvn2.Timestamp.DayOfMonth);
            Assert.AreEqual(time.Minute, mvn2.Timestamp.Minute);
            Assert.AreEqual(time.Second, mvn2.Timestamp.Second);
            Assert.AreEqual(time.Millisecond, mvn2.Timestamp.Millisecond);
        }


        [Test()]
        public void TestMeasuredValueScaledWithCP24Time2a()
        {
            byte[] buffer = new byte[257];

            BufferFrame bf = new BufferFrame(buffer, 0);

            ApplicationLayerParameters alParameters = new ApplicationLayerParameters();

            CP24Time2a time = new CP24Time2a(45, 23, 538);

            MeasuredValueScaledWithCP24Time2a mvs = new MeasuredValueScaledWithCP24Time2a(101, 0, new QualityDescriptor(), time);

            mvs.Encode(bf, alParameters, true);
            Assert.AreEqual(6, bf.GetMsgSize());

            bf.ResetFrame();

            mvs.Encode(bf, alParameters, false);

            Assert.AreEqual(alParameters.SizeOfIOA + mvs.GetEncodedSize(), bf.GetMsgSize());
            Assert.AreEqual(9, bf.GetMsgSize());

            MeasuredValueScaledWithCP24Time2a mvs2 = new MeasuredValueScaledWithCP24Time2a(alParameters, buffer, 0, false);

            Assert.AreEqual(101, mvs2.ObjectAddress);
            Assert.AreEqual(0, mvs2.ScaledValue.Value);
            Assert.AreEqual(45, mvs2.Timestamp.Minute);
            Assert.AreEqual(23, mvs2.Timestamp.Second);
            Assert.AreEqual(538, mvs2.Timestamp.Millisecond);
        }

        [Test()]
        public void TestMeasuredValueScaledWithCP56Time2a()
        {
            byte[] buffer = new byte[257];

            BufferFrame bf = new BufferFrame(buffer, 0);

            ApplicationLayerParameters alParameters = new ApplicationLayerParameters();

            DateTime dateTime = DateTime.UtcNow;

            CP56Time2a time = new CP56Time2a(dateTime);

            MeasuredValueScaledWithCP56Time2a mvs = new MeasuredValueScaledWithCP56Time2a(101, 0, new QualityDescriptor(), time);

            mvs.Encode(bf, alParameters, true);
            Assert.AreEqual(10, bf.GetMsgSize());

            bf.ResetFrame();

            mvs.Encode(bf, alParameters, false);

            Assert.AreEqual(alParameters.SizeOfIOA + mvs.GetEncodedSize(), bf.GetMsgSize());
            Assert.AreEqual(13, bf.GetMsgSize());

            MeasuredValueScaledWithCP56Time2a mvs2 = new MeasuredValueScaledWithCP56Time2a(alParameters, buffer, 0, false);

            Assert.AreEqual(101, mvs2.ObjectAddress);
            Assert.AreEqual(0, mvs2.ScaledValue.Value);
            Assert.AreEqual(time.Year, mvs2.Timestamp.Year);
            Assert.AreEqual(time.Month, mvs2.Timestamp.Month);
            Assert.AreEqual(time.DayOfMonth, mvs2.Timestamp.DayOfMonth);
            Assert.AreEqual(time.Minute, mvs2.Timestamp.Minute);
            Assert.AreEqual(time.Second, mvs2.Timestamp.Second);
            Assert.AreEqual(time.Millisecond, mvs2.Timestamp.Millisecond);
        }

        [Test()]
        public void TestDoublePointInformationWithCP56Time2a()
        {
            byte[] buffer = new byte[257];

            BufferFrame bf = new BufferFrame(buffer, 0);

            ApplicationLayerParameters alParameters = new ApplicationLayerParameters();

            DateTime dateTime = DateTime.UtcNow;

            CP56Time2a time = new CP56Time2a(dateTime);

            DoublePointWithCP56Time2a dpi = new DoublePointWithCP56Time2a(101, DoublePointValue.INTERMEDIATE, new QualityDescriptor(), time);

            dpi.Encode(bf, alParameters, true);
            Assert.AreEqual(8, bf.GetMsgSize());

            bf.ResetFrame();

            dpi.Encode(bf, alParameters, false);

            Assert.AreEqual(alParameters.SizeOfIOA + dpi.GetEncodedSize(), bf.GetMsgSize());
            Assert.AreEqual(11, bf.GetMsgSize());

            DoublePointWithCP56Time2a dpi2 = new DoublePointWithCP56Time2a(alParameters, buffer, 0, false);

            Assert.AreEqual(101, dpi2.ObjectAddress);
            Assert.AreEqual(DoublePointValue.INTERMEDIATE, dpi2.Value);
            Assert.AreEqual(time.Year, dpi2.Timestamp.Year);
            Assert.AreEqual(time.Month, dpi2.Timestamp.Month);
            Assert.AreEqual(time.DayOfMonth, dpi2.Timestamp.DayOfMonth);
            Assert.AreEqual(time.Minute, dpi2.Timestamp.Minute);
            Assert.AreEqual(time.Second, dpi2.Timestamp.Second);
            Assert.AreEqual(time.Millisecond, dpi2.Timestamp.Millisecond);
        }

        [Test()]
        public void TestCP56Time2a()
        {
            CP56Time2a time = new CP56Time2a();

            Assert.AreEqual(time.Year, 0);

            time.Year = 2017;

            Assert.AreEqual(time.Year, 17);

            time.Year = 1980;

            Assert.AreEqual(time.Year, 80);
        }

        [Test()]
        public void TestMeasuredValueWithoutQuality()
        {
            byte[] buffer = new byte[257];

            BufferFrame bf = new BufferFrame(buffer, 0);

            ApplicationLayerParameters alParameters = new ApplicationLayerParameters();

            MeasuredValueNormalizedWithoutQuality mvn = new MeasuredValueNormalizedWithoutQuality(201, 0.5f);

            mvn.Encode(bf, alParameters, true);
            Assert.AreEqual(2, bf.GetMsgSize());

            bf.ResetFrame();

            mvn.Encode(bf, alParameters, false);
            Assert.AreEqual(alParameters.SizeOfIOA + mvn.GetEncodedSize(), bf.GetMsgSize());

            MeasuredValueNormalizedWithoutQuality mvn2 = new MeasuredValueNormalizedWithoutQuality(alParameters, buffer, 0, false);

            Assert.AreEqual(201, mvn2.ObjectAddress);
            Assert.AreEqual(0.5f, mvn2.NormalizedValue, 0.001);
        }

        [Test()]
        public void TestMeasuredValueNormalized()
        {
            byte[] buffer = new byte[257];

            BufferFrame bf = new BufferFrame(buffer, 0);

            ApplicationLayerParameters alParameters = new ApplicationLayerParameters();

            MeasuredValueNormalized mvn = new MeasuredValueNormalized(201, 0.5f, new QualityDescriptor());

            mvn.Encode(bf, alParameters, true);
            Assert.AreEqual(3, bf.GetMsgSize());

            bf.ResetFrame();

            mvn.Encode(bf, alParameters, false);
            Assert.AreEqual(alParameters.SizeOfIOA + mvn.GetEncodedSize(), bf.GetMsgSize());

            MeasuredValueNormalized mvn2 = new MeasuredValueNormalized(alParameters, buffer, 0, false);

            Assert.AreEqual(201, mvn2.ObjectAddress);
            Assert.AreEqual(0.5f, mvn2.NormalizedValue, 0.001);
        }


        [Test()]
        public void TestMeasuredValueShort()
        {
            byte[] buffer = new byte[257];

            BufferFrame bf = new BufferFrame(buffer, 0);

            ApplicationLayerParameters alParameters = new ApplicationLayerParameters();

            MeasuredValueShort mvs = new MeasuredValueShort(201, 0.5f, new QualityDescriptor());

            mvs.Encode(bf, alParameters, true);
            Assert.AreEqual(5, bf.GetMsgSize());

            bf.ResetFrame();

            mvs.Encode(bf, alParameters, false);
            Assert.AreEqual(alParameters.SizeOfIOA + mvs.GetEncodedSize(), bf.GetMsgSize());

            MeasuredValueShort mvs2 = new MeasuredValueShort(alParameters, buffer, 0, false);

            Assert.AreEqual(201, mvs2.ObjectAddress);
            Assert.AreEqual(0.5f, mvs2.Value, 0.001);
        }


        [Test()]
        public void TestMeasuredValueScaled()
        {
            byte[] buffer = new byte[257];

            BufferFrame bf = new BufferFrame(buffer, 0);

            ApplicationLayerParameters alParameters = new ApplicationLayerParameters();

            MeasuredValueScaled mvs = new MeasuredValueScaled(201, 0, new QualityDescriptor());

            mvs.Encode(bf, alParameters, true);
            Assert.AreEqual(3, bf.GetMsgSize());

            bf.ResetFrame();

            mvs.Encode(bf, alParameters, false);
            Assert.AreEqual(alParameters.SizeOfIOA + mvs.GetEncodedSize(), bf.GetMsgSize());

            MeasuredValueScaled mvs2 = new MeasuredValueScaled(alParameters, buffer, 0, false);

            Assert.AreEqual(201, mvs2.ObjectAddress);
            Assert.AreEqual(0, mvs2.ScaledValue.Value);

        }


        public class SimpleFile : TransparentFile
        {
            public SimpleFile(int ca, int ioa, NameOfFile nof)
                : base(ca, ioa, nof)
            {
            }

            public bool transferComplete = false;
            public bool success = false;

            public override void TransferComplete(bool success)
            {
                Console.WriteLine("Transfer complete: " + success.ToString());
                transferComplete = true;
                this.success = success;
            }
        }

        public class Receiver : IFileReceiver
        {
            public bool finishedCalled = false;

            public byte[] recvBuffer = new byte[10000];
            public int recvdBytes = 0;
            public byte lastSection = 0;

            public void Finished(FileErrorCode result)
            {
                Console.WriteLine("File download finished - code: " + result.ToString());
                finishedCalled = true;
            }


            public void SegmentReceived(byte sectionName, int offset, int size, byte[] data)
            {
                lastSection = sectionName;
                Array.Copy(data, 0, recvBuffer, recvdBytes, size);
                recvdBytes += size;
                Console.WriteLine("File segment - sectionName: {0} offset: {1} size: {2}", sectionName, offset, size);
                for (int i = 0; i < size; i++)
                {
                    Console.Write(" " + data[i]);
                }
                Console.WriteLine();
            }
        }

        [Test()]
        public void TestFileUploadSingleSection()
        {
            Server server = new Server();
            int port = GetPort();

            server.SetLocalPort(port);
            server.Start();

            SimpleFile file = new SimpleFile(1, 30000, NameOfFile.TRANSPARENT_FILE);

            byte[] fileData = new byte[100];

            for (int i = 0; i < 100; i++)
                fileData[i] = (byte)(i);

            file.AddSection(fileData);

            server.GetAvailableFiles().AddFile(file);

            Thread.Sleep(2000);

            Connection con = new Connection("127.0.0.1", port);
            con.Connect();

            Thread.Sleep(2000);

            Receiver receiver = new Receiver();

            Assert.IsTrue(con.IsRunning);
            Assert.IsTrue(server.IsRunning());

            try
            {
                con.GetFile(1, 30000, NameOfFile.TRANSPARENT_FILE, receiver, 30000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Test] GetFile threw an exception: {ex.Message}");
                Assert.Fail("GetFile failed!");
            }

            Thread.Sleep(30000);
            Assert.IsTrue(receiver.finishedCalled);
            Assert.AreEqual(100, receiver.recvdBytes);
            Assert.AreEqual(1, receiver.lastSection);

            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(receiver.recvBuffer[i], i);
            }

            con.Close();

            server.Stop();
        }

        [Test()]
        public void TestFileUploadMultipleSections()
        {
            Server server = new Server();
            int port = GetPort();

            server.SetLocalPort(port);
            server.Start();

            SimpleFile file = new SimpleFile(1, 30000, NameOfFile.TRANSPARENT_FILE);

            byte[] fileData = new byte[100];

            for (int i = 0; i < 100; i++)
                fileData[i] = (byte)(i);

            byte[] fileData2 = new byte[100];

            for (int i = 0; i < 100; i++)
                fileData2[i] = (byte)(100 + i);

            file.AddSection(fileData);
            file.AddSection(fileData2);

            server.GetAvailableFiles().AddFile(file);

            Connection con = new Connection("127.0.0.1", port);
            con.Connect();


            Receiver receiver = new Receiver();

            con.GetFile(1, 30000, NameOfFile.TRANSPARENT_FILE, receiver);

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

        [Test()]
        public void TestFileUploadMultipleSectionsFreeFileName()
        {
            Server server = new Server();
            int port = GetPort();
            server.SetLocalPort(port);
            server.Start();

            SimpleFile file = new SimpleFile(1, 30000, (NameOfFile)12);

            byte[] fileData = new byte[100];

            for (int i = 0; i < 100; i++)
                fileData[i] = (byte)(i);

            byte[] fileData2 = new byte[100];

            for (int i = 0; i < 100; i++)
                fileData2[i] = (byte)(100 + i);

            file.AddSection(fileData);
            file.AddSection(fileData2);

            server.GetAvailableFiles().AddFile(file);

            Connection con = new Connection("127.0.0.1", port);
            con.Connect();


            Receiver receiver = new Receiver();

            con.GetFile(1, 30000, (NameOfFile)12, receiver);

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

        [Test()]
        public void TestFileUploadMultipleSegments()
        {
            Server server = new Server();
            int port = GetPort();
            server.SetLocalPort(port);
            server.Start();

            SimpleFile file = new SimpleFile(1, 30000, NameOfFile.TRANSPARENT_FILE);

            byte[] fileData = new byte[1000];

            for (int i = 0; i < 1000; i++)
                fileData[i] = (byte)(i);

            file.AddSection(fileData);

            server.GetAvailableFiles().AddFile(file);

            Connection con = new Connection("127.0.0.1", port);
            con.Connect();


            Receiver receiver = new Receiver();

            con.GetFile(1, 30000, NameOfFile.TRANSPARENT_FILE, receiver);

            Thread.Sleep(3000);
            Assert.IsTrue(receiver.finishedCalled);
            Assert.AreEqual(1000, receiver.recvdBytes);
            Assert.AreEqual(1, receiver.lastSection);
            Assert.IsTrue(file.transferComplete);
            Assert.IsTrue(file.success);

            for (int i = 0; i < 1000; i++)
            {
                Assert.AreEqual(receiver.recvBuffer[i], (byte)i);
            }

            con.Close();

            server.Stop();
        }


        [Test()]
        public void TestFileDownloadSingleSection()
        {
            Server server = new Server();
            int port = GetPort();
            server.SetLocalPort(port);
            server.DebugOutput = true;

            Receiver receiver = new Receiver();


            server.SetFileReadyHandler((object parameter, int ca, int ioa, NameOfFile nof, int lengthOfFile) =>
            {
                return receiver;
            }, null);
            server.Start();

            SimpleFile file = new SimpleFile(1, 30000, NameOfFile.TRANSPARENT_FILE);

            byte[] fileData = new byte[100];

            for (int i = 0; i < 100; i++)
                fileData[i] = (byte)(i);

            file.AddSection(fileData);
            server.GetAvailableFiles().AddFile(file);

            Connection con = new Connection("127.0.0.1", port);
            con.Connect();
            Thread.Sleep(2000);

            try
            {
                con.SendFile(1, 30000, NameOfFile.TRANSPARENT_FILE, file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Test] SendFile threw an exception: {ex.Message}");
                Assert.Fail("SendFile failed!");
            }


            Thread.Sleep(10000);

            Assert.IsTrue(receiver.finishedCalled);
            Assert.AreEqual(100, receiver.recvdBytes);
            Assert.AreEqual(1, receiver.lastSection);

            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(receiver.recvBuffer[i], i);
            }

            con.Close();
            Thread.Sleep(2000);
            server.Stop();
        }

        [Test()]
        public void TestFileDownloadMultipleSegments()
        {
            Server server = new Server();
            int port = GetPort();
            server.SetLocalPort(port);

            Receiver receiver = new Receiver();

            server.DebugOutput = true;

            server.SetFileReadyHandler(delegate (object parameter, int ca, int ioa, NameOfFile nof, int lengthOfFile)
            {
                return receiver;
            }, null);

            server.Start();

            SimpleFile file = new SimpleFile(1, 30000, NameOfFile.TRANSPARENT_FILE);

            byte[] fileData = new byte[1000];

            for (int i = 0; i < 1000; i++)
                fileData[i] = (byte)(i);

            file.AddSection(fileData);

            server.GetAvailableFiles().AddFile(file);

            Connection con = new Connection("127.0.0.1", port);
            con.Connect();
            Thread.Sleep(2000);

            try
            {
                con.SendFile(1, 30000, NameOfFile.TRANSPARENT_FILE, file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Test] SendFile threw an exception: {ex.Message}");
                Assert.Fail("SendFile failed!");
            }

            Thread.Sleep(10000);

            Assert.IsTrue(receiver.finishedCalled);
            Assert.AreEqual(1000, receiver.recvdBytes);
            Assert.AreEqual(1, receiver.lastSection);
            Assert.IsTrue(file.transferComplete);
            Assert.IsTrue(file.success);

            for (int i = 0; i < 1000; i++)
            {
                Assert.AreEqual(receiver.recvBuffer[i], (byte)i);
            }

            con.Close();

            server.Stop();
        }

        [Test()]
        public void TestFileDownloadMultipleSegmentsMultipleSections()
        {
            Server server = new Server();
            int port = GetPort();
            server.SetLocalPort(port);
            Receiver receiver = new Receiver();

            server.SetFileReadyHandler(delegate (object parameter, int ca, int ioa, NameOfFile nof, int lengthOfFile)
            {
                return receiver;
            }, null);

            server.Start();

            SimpleFile file = new SimpleFile(1, 30000, NameOfFile.TRANSPARENT_FILE);

            byte[] fileData = new byte[100];

            for (int i = 0; i < 100; i++)
                fileData[i] = (byte)(i);

            byte[] fileData2 = new byte[100];

            for (int i = 0; i < 100; i++)
                fileData2[i] = (byte)(100 + i);

            file.AddSection(fileData);
            file.AddSection(fileData2);

            server.GetAvailableFiles().AddFile(file);

            Connection con = new Connection("127.0.0.1", port);
            con.Connect();
            Thread.Sleep(2000);


            con.SendFile(1, 30000, NameOfFile.TRANSPARENT_FILE, file);

            Thread.Sleep(10000);
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

        [Test()]
        public void TestFileDownloadSlaveRejectsFile()
        {
            Server server = new Server();
            int port = GetPort();
            server.SetLocalPort(port);

            Receiver receiver = new Receiver();

            server.SetFileReadyHandler(delegate (object parameter, int ca, int ioa, NameOfFile nof, int lengthOfFile)
            {
                return null;
            }, null);

            server.Start();

            SimpleFile file = new SimpleFile(1, 30000, NameOfFile.TRANSPARENT_FILE);

            byte[] fileData = new byte[100];

            for (int i = 0; i < 100; i++)
                fileData[i] = (byte)(i);

            file.AddSection(fileData);

            server.GetAvailableFiles().AddFile(file);

            Connection con = new Connection("127.0.0.1", port);
            con.Connect();
            Thread.Sleep(2000);

            try
            {
                con.SendFile(1, 30000, NameOfFile.TRANSPARENT_FILE, file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Test] SendFile threw an exception: {ex.Message}");
                Assert.Fail("SendFile failed!");
            }

            Thread.Sleep(10000);

            Assert.IsFalse(receiver.finishedCalled);
            Assert.AreEqual(0, receiver.recvdBytes);
            Assert.AreEqual(0, receiver.lastSection);

            con.Close();

            server.Stop();
        }

        [Test()]
        public void TestInformationObjectCopyConstructors()
        {
            SinglePointInformation si = new SinglePointInformation(101, true, QualityDescriptor.VALID());

            SinglePointInformation copySi = new SinglePointInformation(si);

            Assert.AreEqual(copySi.ObjectAddress, 101);
            Assert.AreEqual(copySi.Value, true);
            Assert.AreEqual(copySi.Quality.Invalid, false);

            CP56Time2a time = new CP56Time2a(DateTime.Now);

            SinglePointWithCP56Time2a siWithTs = new SinglePointWithCP56Time2a(102, false, QualityDescriptor.INVALID(), time);

            copySi = new SinglePointInformation(siWithTs);

            Assert.AreEqual(copySi.ObjectAddress, 102);
            Assert.AreEqual(copySi.Value, false);
            Assert.AreEqual(copySi.Quality.Invalid, true);

            copySi = new SinglePointWithCP56Time2a(siWithTs);

            Assert.AreEqual(copySi.ObjectAddress, 102);
            Assert.AreEqual(copySi.Value, false);
            Assert.AreEqual(copySi.Quality.Invalid, true);

            Assert.AreNotSame(((SinglePointWithCP56Time2a)copySi).Timestamp, time);
            Assert.AreEqual(((SinglePointWithCP56Time2a)copySi).Timestamp, time);

            PackedSinglePointWithSCD packedScd = new PackedSinglePointWithSCD(103, new StatusAndStatusChangeDetection(), QualityDescriptor.INVALID());

            packedScd.SCD.CD(1, true);
            packedScd.SCD.CD(7, true);

            PackedSinglePointWithSCD packedScdCopy = new PackedSinglePointWithSCD(packedScd);

            Assert.AreEqual(packedScdCopy.ObjectAddress, 103);
            Assert.AreEqual(packedScdCopy.SCD.CD(0), false);
            Assert.AreEqual(packedScdCopy.SCD.CD(1), true);
            Assert.AreEqual(packedScdCopy.SCD.CD(7), true);
            Assert.AreEqual(packedScdCopy.QDS.Invalid, true);

            BinaryCounterReading bcr = new BinaryCounterReading();
            bcr.Value = 1234;
            bcr.Invalid = true;
            bcr.SequenceNumber = 2;

            IntegratedTotals integratedTotalsOriginal = new IntegratedTotals(104, bcr);

            IntegratedTotals integratedTotalsCopy = new IntegratedTotals(integratedTotalsOriginal);

            Assert.AreEqual(integratedTotalsCopy.ObjectAddress, 104);
            Assert.AreNotSame(integratedTotalsOriginal.BCR, integratedTotalsCopy.BCR);
            Assert.AreEqual(integratedTotalsOriginal.BCR.Value, integratedTotalsCopy.BCR.Value);
            Assert.AreEqual(integratedTotalsOriginal.BCR.Invalid, integratedTotalsCopy.BCR.Invalid);
            Assert.AreEqual(integratedTotalsOriginal.BCR.SequenceNumber, integratedTotalsCopy.BCR.SequenceNumber);

            IntegratedTotalsWithCP56Time2a integratedTotalsWithCP56Copy = new IntegratedTotalsWithCP56Time2a(integratedTotalsOriginal);

            Assert.AreEqual(integratedTotalsWithCP56Copy.ObjectAddress, 104);
            Assert.AreNotSame(integratedTotalsOriginal.BCR, integratedTotalsWithCP56Copy.BCR);
            Assert.AreEqual(integratedTotalsOriginal.BCR.Value, integratedTotalsWithCP56Copy.BCR.Value);
            Assert.AreEqual(integratedTotalsOriginal.BCR.Invalid, integratedTotalsWithCP56Copy.BCR.Invalid);
            Assert.AreEqual(integratedTotalsOriginal.BCR.SequenceNumber, integratedTotalsWithCP56Copy.BCR.SequenceNumber);
        }

        [Test()]
        public void TestSingleRedundancyGroup()
        {
            bool running = true;

            Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
            {
                e.Cancel = true;
                running = false;
            };

            // specify application layer parameters (CS 101 and CS 104)
            var alParams = new ApplicationLayerParameters();

            // specify APCI parameters (only CS 104)
            var apciParameters = new APCIParameters();

            Server server = new Server(apciParameters, alParams);

            server.DebugOutput = true;

            server.ServerMode = ServerMode.SINGLE_REDUNDANCY_GROUP;

            server.MaxQueueSize = 10;
            server.MaxOpenConnections = 6;

            server.EnqueueMode = EnqueueMode.REMOVE_OLDEST;
            server.SetLocalPort(GetPort());
            server.Start();

            int waitTime = 1000;

            int enqueuedMessage = 0;
            int maxLoop = server.MaxQueueSize + 3;
            int loopCount = 0;
            while (running && server.IsRunning())
            {
                Thread.Sleep(100);

                if (waitTime > 0)
                    waitTime -= 100;
                else
                {
                    ASDU newAsdu = new ASDU
                (server.GetApplicationLayerParameters(), CauseOfTransmission.INITIALIZED, false, false, 0, 1, false);

                    newAsdu = new ASDU(server.GetApplicationLayerParameters(), CauseOfTransmission.PERIODIC, false, false, 0, 1, false);

                    newAsdu.AddInformationObject(new MeasuredValueScaled(110, -1, new QualityDescriptor()));
                    server.EnqueueASDU(newAsdu);
                    enqueuedMessage++;

                    int numberOfQueueEntries = server.GetNumberOfQueueEntries();
                    Console.WriteLine($"Number of queue entries: {numberOfQueueEntries}");
                    waitTime = 1000;

                    if (enqueuedMessage >= server.MaxQueueSize)
                        Assert.AreEqual(server.MaxQueueSize, numberOfQueueEntries);
                    else
                        Assert.AreEqual(enqueuedMessage, numberOfQueueEntries);

                    maxLoop++;
                }

                if (loopCount == maxLoop)
                    break;

                loopCount++;
            }

            if (server.IsRunning())
            {
                Console.WriteLine("Stop server");
                server.Stop();
            }
            else
            {
                Console.WriteLine("Server stopped");
            }
        }

        [Test()]
        public void TestMultipleRedundancyGroup()
        {
            bool running = true;
            int port = GetPort();

            Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
            {
                e.Cancel = true;
                running = false;
            };

            // specify application layer parameters (CS 101 and CS 104)
            var alParams = new ApplicationLayerParameters();

            // specify APCI parameters (only CS 104)
            var apciParameters = new APCIParameters();

            Server server = new Server(apciParameters, alParams);

            server.DebugOutput = true;

            server.ServerMode = ServerMode.MULTIPLE_REDUNDANCY_GROUPS;

            server.MaxQueueSize = 10;
            server.MaxOpenConnections = 6;

            RedundancyGroup redGroup = new RedundancyGroup("red-group");
            redGroup.AddAllowedClient("127.0.0.1");

            server.AddRedundancyGroup(redGroup);

            server.EnqueueMode = EnqueueMode.REMOVE_OLDEST;
            server.SetLocalPort(port);

            server.Start();

            int waitTime = 1000;
            int enqueuedMessage = 0;
            int maxLoop = server.MaxQueueSize + 3;
            while (running && server.IsRunning())
            {
                Thread.Sleep(100);

                if (waitTime > 0)
                    waitTime -= 100;
                else
                {
                    ASDU newAsdu = new ASDU
                        (server.GetApplicationLayerParameters(), CauseOfTransmission.INITIALIZED, false, false, 0, 1, false);

                    newAsdu = new ASDU(server.GetApplicationLayerParameters(), CauseOfTransmission.PERIODIC, false, false, 0, 1, false);

                    newAsdu.AddInformationObject(new MeasuredValueScaled(110, -1, new QualityDescriptor()));

                    server.EnqueueASDU(newAsdu);
                    enqueuedMessage++;

                    int numberOfQueueEntries = server.GetNumberOfQueueEntries(redGroup);
                    Console.WriteLine($"Number of queue entries: {numberOfQueueEntries}");
                    waitTime = 1000;
                    if (enqueuedMessage >= server.MaxQueueSize)
                        Assert.AreEqual(server.MaxQueueSize, numberOfQueueEntries);
                    else
                        Assert.AreEqual(enqueuedMessage, numberOfQueueEntries);

                    maxLoop++;
                }
            }

            if (server.IsRunning())
            {
                Console.WriteLine("Stop server");
                server.Stop();
            }
            else
            {
                Console.WriteLine("Server stopped");
            }
        }

        [Test()]
        public void TestConnectionIsRedundancyGroup()
        {
            bool running = true;

            Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
            {
                e.Cancel = true;
                running = false;
            };

            // specify application layer parameters (CS 101 and CS 104)
            var alParams = new ApplicationLayerParameters();

            // specify APCI parameters (only CS 104)
            var apciParameters = new APCIParameters();

            Server server = new Server(apciParameters, alParams);

            server.DebugOutput = true;

            server.ServerMode = ServerMode.CONNECTION_IS_REDUNDANCY_GROUP;

            server.MaxQueueSize = 10;
            server.MaxOpenConnections = 6;

            server.EnqueueMode = EnqueueMode.REMOVE_OLDEST;

            server.Start();

            int waitTime = 1000;

            int enqueuedMessage = 0;
            int maxLoop = server.MaxQueueSize + 3;
            int loopCount = 0;
            while (running && server.IsRunning())
            {
                Thread.Sleep(100);

                if (waitTime > 0)
                    waitTime -= 100;
                else
                {
                    ASDU newAsdu = new ASDU
                (server.GetApplicationLayerParameters(), CauseOfTransmission.INITIALIZED, false, false, 0, 1, false);

                    newAsdu = new ASDU(server.GetApplicationLayerParameters(), CauseOfTransmission.PERIODIC, false, false, 0, 1, false);

                    newAsdu.AddInformationObject(new MeasuredValueScaled(110, -1, new QualityDescriptor()));
                    server.EnqueueASDU(newAsdu);
                    enqueuedMessage++;

                    int numberOfQueueEntries = server.GetNumberOfQueueEntries();
                    Console.WriteLine($"Number of queue entries: {numberOfQueueEntries}");
                    waitTime = 1000;

                    if (enqueuedMessage == server.MaxQueueSize)
                        Assert.AreEqual(server.MaxQueueSize, numberOfQueueEntries);
                    else
                        Assert.AreEqual(enqueuedMessage, numberOfQueueEntries);

                    maxLoop++;
                }

                if (loopCount == maxLoop)
                    break;

                loopCount++;
            }

            if (server.IsRunning())
            {
                Console.WriteLine("Stop server");
                server.Stop();
            }
            else
            {
                Console.WriteLine("Server stopped");
            }
        }

        [Test()]
        public void TestSingleEventType()
        {
            SingleEvent singleEvent = new SingleEvent();

            EventState eventState = singleEvent.State;

            Assert.AreEqual(EventState.INDETERMINATE_0, eventState);

            QualityDescriptorP qdp = singleEvent.QDP;

            Assert.AreEqual(0, qdp.EncodedValue);
        }

        [Test()]
        public void TestScaledNormalizedConversion()
        {
            const float NORMALIZED_VALUE_MAX = 32767.0f / 32768.0f;

            Assert.AreEqual(32767, new ScaledValue().ConvertNormalizedValueToScaled(NORMALIZED_VALUE_MAX));

            Assert.AreEqual(32767, new ScaledValue().ConvertNormalizedValueToScaled(1.0f));

            Assert.AreEqual(32767, new ScaledValue().ConvertNormalizedValueToScaled(2.0f));

            Assert.AreEqual(-32768, new ScaledValue().ConvertNormalizedValueToScaled(-1.0f));

            Assert.AreEqual(-32768, new ScaledValue().ConvertNormalizedValueToScaled(-2.0f));

            Assert.AreEqual(0, new ScaledValue().ConvertNormalizedValueToScaled(0.0f));

            Assert.AreEqual(0, new ScaledValue().ConvertNormalizedValueToScaled(-0.0f));

            float normalizedUnit = (1f - NORMALIZED_VALUE_MAX);

            /*Allow a small tolerance (0.0001f) in floating-point assertions.*/

            Assert.AreEqual(NORMALIZED_VALUE_MAX - normalizedUnit, new ScaledValue(32766).GetNormalizedValue(), 0.0001f);

            Assert.AreEqual(NORMALIZED_VALUE_MAX, new ScaledValue(32767).GetNormalizedValue(), 0.0001f);

            Assert.AreEqual(NORMALIZED_VALUE_MAX, new ScaledValue(32768).GetNormalizedValue(), 0.0001f);

            Assert.AreEqual(NORMALIZED_VALUE_MAX, new ScaledValue(99999).GetNormalizedValue(), 0.0001f);

            Assert.AreEqual(-1.0f + (normalizedUnit * 2f), new ScaledValue(-32766).GetNormalizedValue(), 0.0001f);

            Assert.AreEqual(-1.0f + normalizedUnit, new ScaledValue(-32767).GetNormalizedValue(), 0.0001f);

            Assert.AreEqual(-1.0f, new ScaledValue(-32768).GetNormalizedValue());

            Assert.AreEqual(-1.0f, new ScaledValue(-32769).GetNormalizedValue());

            Assert.AreEqual(0.0f, new ScaledValue(0).GetNormalizedValue());
        }

    }
}

