# Deploying PG to Kubernetes

## Create secrets
- `pg-tls`. Must contain TLS certificate (`certificate`) in PFX format along with the password (`password`).
- `pg-sessions`. Must contain Azure Storage connection string for blob storage (`sessions-blob-conn`) and (`qos2-table-conn`).
- `pg-hub`. Must contain Azure IoT Hub connection string (`conn`).