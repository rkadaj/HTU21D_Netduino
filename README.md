## HTU21D - Relative Humidity & Temperature Sensor ##

I purchased one from Sparkfun which has a build-in 4.7k pull up resistor. https://www.sparkfun.com/products/12064. They have a C++ library linked at the bottom of the product page in order to interface with the sensor using an Arduino but since I wanted to use this with my Netduino I thought I would write one myself. I used Andro72's library (https://github.com/Andro72/HTU21Dnetduino) as a starting point. I also referenced the C++ library written by Sparkfun to see their implementation as well (https://github.com/sparkfun/SparkFun_HTU21D_Breakout_Arduino_Library/tree/V_1.1.0)

# Usage #
Download (or copy and paste) the Htu21D.cs file into your own project Netduino project.

**Constructors**

There is a parameter-less constructor and a constructor that takes a parameter of type Htu21D.MeasurementResolution. The measurement resolution is used to switch between the various resolutions indicated in the HTU21D documentation. The lower resolutions have a faster data fetch delay.

**Public Methods**

`SetResolution(Htu21D.MeasurementResolution)`: Sets the resolution to the provided resolution using the Htu21D.MeasurementResolution class.

`ReadTemperature()`: The fetch timeout for the temperature is based on the resolution max timeout value in the documentation.

`ReadHumidity()`: The fetch timeout for the humidity is based on the resolution max timeout value in the documentation.

The fetching of both the temperature and humidity runs through a cyclic redundancy check (CRC) using the checksum value provided by the sensor in order to verify the accuracy of the transferred data. **If the data check fails, a value of 999 will be returned.**