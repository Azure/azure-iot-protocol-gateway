# Deploying protocol gateway to Kubernetes

## Create secrets in k8s cluster

- `pg-tls`. Must contain TLS certificate (`certificate`) in PFX format along with the password (`password`).
- `pg-sessions`. Must contain Azure Storage connection string for blob storage (`sessions-blob-conn`) and (`qos2-table-conn`).
- `pg-hub`. Must contain Azure IoT Hub connection string (`conn`).

For convenience, use you can create secrets folder under /host/k8s/secrets and run ./host/k8s/create-secrets.ps1 to provision them in current cluster and namespace.
