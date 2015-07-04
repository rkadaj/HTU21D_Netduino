namespace HTU21D_Netduino
{
    using System.Threading;

    using Microsoft.SPOT;
    using Microsoft.SPOT.Hardware;

    public class Htu21D
    {
        private readonly I2CDevice i2CDevice;

        private MeasurementResolution measurementResolution;

        private const int DefaultTimeoutMilliseconds = 1000;

        // From HTU21D datasheet (page 11)
        private const byte HumidityCommandCodeNoHold = 0xF5;

        private const byte TemperatureCommandCodeNoHold = 0xF3;

        private const byte HumidityCommandCodeHold = 0xE5;

        private const byte TemperatureCommandCodeHold = 0xE3;

        private const byte ReadUserRegisterCode = 0xE7;

        private const byte WriteUserRegisterCode = 0XE6;

        //This is the 0x0131 polynomial shifted to farthest left of three bytes
        private const int ShiftedDivisor = 0x988000;

        public Htu21D()
        {
            this.i2CDevice = new I2CDevice(GetConfiguration());
        }

        public void SetResolution(MeasurementResolution resolution)
        {
            this.measurementResolution = resolution;
            this.SetResolution(this.measurementResolution.Resolution);
        }

        public float ReadTemperature()
        {
            this.Write(
                new[] { this.measurementResolution.TemperatureCommandCode },
                this.measurementResolution.TemperatureMeasurementTimeout);

            var readResult = this.ReadResults(this.measurementResolution.TemperatureMeasurementTimeout);
            byte mostSignificantByte = readResult[0];
            byte leastSignificantByte = readResult[1];
            byte checksum = readResult[2];

            // From the documentation: Measured data are transferred in two byte packages,
            // i.e. in frames of 8-bit length where the most significant bit (MSB) is 
            // transferred first (left aligned). Each byte is followed by an acknowledge 
            // bit. The two status bits, the last bits of LSB, must be set to ‘0’ before 
            // calculating physical values. (page 15)
            var rawTemperatureReading = ((uint)mostSignificantByte << 8) | leastSignificantByte;

            if (this.CalculateCrc((ushort)rawTemperatureReading, checksum) != 0)
            {
                return 999;
            }

            rawTemperatureReading &= 0XFFFC;

            // Conversion performed is from the datasheet (page 15)
            var relativeTemperature = rawTemperatureReading / 65536f;
            relativeTemperature = -46.85f + (175.72f * relativeTemperature);

            return relativeTemperature;
        }

        public float ReadHumidity()
        {
            this.Write(
                new[] { this.measurementResolution.HumidityCommandCode },
                this.measurementResolution.HumidityMeasurementTimeout);

            var readResult = this.ReadResults(this.measurementResolution.HumidityMeasurementTimeout);

            byte mostSignificantByte = readResult[0];
            byte leastSignificantByte = readResult[1];
            byte checksum = readResult[2];

            // From the documentation: Measured data are transferred in two byte packages,
            // i.e. in frames of 8-bit length where the most significant bit (MSB) is 
            // transferred first (left aligned). Each byte is followed by an acknowledge 
            // bit. The two status bits, the last bits of LSB, must be set to ‘0’ before 
            // calculating physical values. (page 15)
            var rawHumidityReading = ((uint)mostSignificantByte << 8) | leastSignificantByte;

            if (this.CalculateCrc((ushort)rawHumidityReading, checksum) != 0)
            {
                return 999;
            }

            rawHumidityReading &= 0xFFFC; // Zero out the status bits

            // Conversion performed is from the datasheet (page 15)
            var relativeHumidity = rawHumidityReading / 65536f;
            relativeHumidity = -6 + (125 * relativeHumidity);

            return relativeHumidity;
        }

        private static I2CDevice.Configuration GetConfiguration()
        {
            const byte I2CAddress = 0x40; // From eagle file
            const int DefaultClockRate = 400;

            return new I2CDevice.Configuration(I2CAddress, DefaultClockRate);
        }

        private void SetResolution(byte resolution)
        {
            var userRegister = this.ReadUserRegister();

            // Remove resolution bits (0x7E = 011111110)
            userRegister &= 0x7E;

            // Ensure only resolution bits remain (0x81 = 10000001)
            resolution &= 0x81;

            // Add the wanted resolution to the userRegister
            userRegister |= resolution;

            this.WriteToRegister(WriteUserRegisterCode, userRegister);
        }

        private void WriteToRegister(byte registerCode, byte value)
        {
            this.Write(new []{ registerCode, value }, DefaultTimeoutMilliseconds);
        }

        private byte ReadUserRegister()
        {
            this.Write(new[] { ReadUserRegisterCode }, DefaultTimeoutMilliseconds);

            var readBuffer = new byte[1];
            this.Read(readBuffer, DefaultTimeoutMilliseconds);
            var userRegister = readBuffer[0];

            return userRegister;
        }

        private void Write(byte[] writeBuffer, int commandTimeoutMilliseconds)
        {
            I2CDevice.I2CTransaction i2CWriteTransaction = I2CDevice.CreateWriteTransaction(writeBuffer);

            var writeResult = this.i2CDevice.Execute(new[] { i2CWriteTransaction }, commandTimeoutMilliseconds);

            if (writeResult == 0)
            {
                Debug.Print("Error writting I2C Transaction");
            }

            Thread.Sleep(commandTimeoutMilliseconds);
        }

        private byte[] ReadResults(int timeoutMilliseconds)
        {
            byte[] readBuffer = new byte[3];
            this.Read(readBuffer, timeoutMilliseconds);
            return readBuffer;
        }

        private void Read(byte[] readBuffer, int timeoutInMilliseconds)
        {
            I2CDevice.I2CTransaction i2CReadTransaction = I2CDevice.CreateReadTransaction(readBuffer);

            var readResult = this.i2CDevice.Execute(new[] { i2CReadTransaction }, timeoutInMilliseconds);

            if (readResult != readBuffer.Length)
            {
                Debug.Print("Error reading I2C Transaction");
            }
        }


        // See CRC Calculation on Page 14 of the documentation
        private byte CalculateCrc(ushort sensorMessage, byte sensorChecksum)
        {
            //Pad with 8 bits and add in the checksum value
            uint remainder = (uint)sensorMessage << 8;
            remainder |= sensorChecksum;

            var divisor = (uint)ShiftedDivisor;

            // Operate on only 16 positions of max 24. The remaining 8 are our remainder 
            // and should be zero when we're done.
            for (var i = 0; i < 16; i++)
            {
                // Verifies if anything other than 0 is returned. In this instance it would
                // be a 1 bit anywhere along the first 24 bits.
                if ((remainder & (uint)1 << (23 - i)) > 0)
                {
                    remainder ^= divisor;
                }

                // Repeated 16 times until the divisor reaches the right-hand end of the input
                divisor >>= 1;
            }

            return (byte)remainder;
        }

        public sealed class MeasurementResolution
        {
            // Timouts from documentation (page 3 & 5)
            // Resolution from documentation (page 13)
            public static readonly MeasurementResolution Rh12BitTemp14Bit = new MeasurementResolution(0x00, 50, 16);

            public static readonly MeasurementResolution Rh8BitTemp12Bit = new MeasurementResolution(0x01, 13, 3);

            public static readonly MeasurementResolution Rh10BitTemp13Bit = new MeasurementResolution(0x80, 25, 5);

            public static readonly MeasurementResolution Rh11BitTemp11Bit = new MeasurementResolution(0x81, 7, 8);

            private MeasurementResolution(
                byte resolution,
                int temperatureMeasurementTimeout,
                int humidityMeasurementTimeout)
            {
                this.Resolution = resolution;
                this.TemperatureCommandCode = TemperatureCommandCodeNoHold;
                this.HumidityCommandCode = HumidityCommandCodeNoHold;
                this.TemperatureMeasurementTimeout = temperatureMeasurementTimeout;
                this.HumidityMeasurementTimeout = humidityMeasurementTimeout;
            }

            public byte Resolution { get; private set; }

            public byte TemperatureCommandCode { get; private set; }

            public byte HumidityCommandCode { get; private set; }

            public int TemperatureMeasurementTimeout { get; private set; }

            public int HumidityMeasurementTimeout { get; private set; }
        }
    }
}