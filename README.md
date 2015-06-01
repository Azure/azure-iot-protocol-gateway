# Microsoft Azure IoT Hub Gateway

Azure IoT Hub Gateway is a set of components enabling additional communication endpoints and providing extension points for MQTT-based network communication with IoT Hub.

## How to build and run

### Prerequisites
To run Azure IoT Gateway you would need Azure IoT Hub to connect to and Azure Storage account to store MQTT session state. You can use Azure Storage Emulator for console sample or cloud sample running locally.

To build Azure IoT Gateway you would need Microsoft Visual Studio 2013+ and Microsoft Azure SDK 2.6+ installed.

### Steps to run console sample

- Provide IoT Hub connection string in samples/Gateway.Samples.Console/appSettings.user.config file (`IoTHub.ConnectionString` setting).
- Run build.cmd or build the solution from Visual Studio.
- Run samples/Gateway.Samples.Console/[Debug or Release]/Gateway.Samples.Console.exe

## How to use

- Use provided samples as a starting point. Check [Samples Walkthrough](https://github.com/azure/azure-iot-gateway/samples/walkthrough.md) for better description.
- Customize the code to accommodate your scenario, e.g. add compression, encryption, custom authentication provider, change buffer allocation configuration or number of executor threads.
- Cloud sample is accompanied by deploy.cmd script. You can alter it to target your deployment scenario.