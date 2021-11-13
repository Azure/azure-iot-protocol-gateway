#takes secrets from secrets
kubectl create secret generic pg-tls --from-file=certificate=$PSScriptRoot/secrets/tls-cert.pfx --from-file=password=$PSScriptRoot/secrets/tls-cert.pwd
kubectl create secret generic pg-sessions --from-file=sessions-blob-conn=$PSScriptRoot/secrets/sessions-blob-conn.txt  --from-file=sessions-blob-conn=$PSScriptRoot/secrets/qos2-table-conn.txt
kubectl create secret generic pg-hub --from-file=conn=$PSScriptRoot/secrets/iothub-conn.txt