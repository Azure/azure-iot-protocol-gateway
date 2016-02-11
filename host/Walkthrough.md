# Samples Walkthrough

Samples consist of the following projects:

- **Gateway.Samples.Common** contains classes and resources used by both Cloud and Console samples. Here you can find bootstrapper and samples of additional handlers.
- **Gateway.Samples.Console** showcases a console application host for Azure IoT Gateway.
- **Gateway.Samples.Cloud.Host** serves as a host for cloud sample's entry point. It wires up the Bootstrapper to run in Azure Cloud Service.
- **Gateway.Samples.Cloud** is a cloud project for packaging and deploying Gateway.Samples.Cloud.Host to Azure.

## Components

### Bootstrapper

Bootstrapper showcases a configuration and lifecycle control for Azure IoT Gateway.
