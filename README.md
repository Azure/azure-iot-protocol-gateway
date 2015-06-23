# Microsoft Azure IoT Protocol Gateway

Azure IoT Protocol Gateway (Protocol Gateway hereafter) is a set of components enabling communication with Azure IoT Hub over MQTT.

## How to build and run

### Prerequisites
To run Protocol Gateway you would need Azure IoT Hub to connect to and Azure Storage account to store MQTT session state. You can use Azure Storage Emulator for console sample or cloud sample running locally.

To build Protocol Gateway you would need Microsoft Visual Studio 2013+ and Microsoft Azure SDK 2.6+ installed.
To deploy Protocol Gateway you would also need Azure PowerShell installed.

### Running console sample

- Provide IoT Hub connection string in `samples/Gateway.Samples.Console/appSettings.config.user` file (`IoTHub.ConnectionString` setting).
- Run `build.cmd` or build the solution from Visual Studio.
- Run `samples/Gateway.Samples.Console/[Debug or Release]/Gateway.Samples.Console.exe`

### Running end to end test

- Update test configuration file (`test\Gateway.Tests\appSettings.config.user`) providing the following settings:
	- `IoTHub.ConnectionString` - connection string for IoT Hub to connect to during the test. WARNING: test will manipulate messages on a device queue so it is strongly recommended not to use production IoT Hub for that
	- `End2End.DeviceName` - identity of the device to use during the test. Device has to be pre-created.
	- `EventHub.ConnectionString` - connection string for Event Hub to listen for telemetry events going through Protocol Gateway and IoT Hub.
	- `EventHub.Partitions` - comma-separated list of Event Hub partition identities to listen for telemetry events on during the test.
	- If you want to run the test against already running Protocol Gateway, uncomment `End2End.ServerAddress` setting in test configuration file. Otherwise, test will start a new Protocol Gateway in-process on port 8883.
- Run test `EndToEndTests.BasicFunctionalityTest`.

## How to use

- Use provided samples as a starting point. Check [Samples Walkthrough](samples/walkthrough.md) for better description.
- Customize the code to accommodate your scenario, e.g. add compression, encryption, custom authentication provider, change buffer allocation configuration or number of executor threads.
- Cloud sample is bundled with deploy.ps1 script that can be used to deploy Protocol Gateway to Azure as Cloud Service.