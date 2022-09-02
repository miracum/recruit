# recruIT Chaos Testing

## Install Prerequisites

```sh
kind create cluster

kubectl create ns recruit

kubectl apply -f https://raw.githubusercontent.com/cloudnative-pg/cloudnative-pg/release-1.18/releases/cnpg-1.18.0.yaml

kubectl wait --for=condition=ready pod \
            -n cnpg-system \
            --selector='app.kubernetes.io/name=cloudnative-pg' \
            --timeout=5m

kubectl apply -f cnpg.yaml -n recruit

helm upgrade --install -n recruit -f recruit-values.yaml --set notify.replicaCount=1 --wait recruit ../../charts/recruit

helm upgrade --install -n recruit -f recruit-values.yaml --set notify.replicaCount=2 --wait recruit ../../charts/recruit

helm upgrade --install -n recruit -f recruit-values.yaml --set notify.replicaCount=3 --wait recruit ../../charts/recruit

helm repo add chaos-mesh https://charts.chaos-mesh.org
MSYS_NO_PATHCONV=1 helm upgrade --install chaos-mesh chaos-mesh/chaos-mesh \
    --create-namespace \
    -n chaos-mesh \
    --set chaosDaemon.runtime=containerd \
    --set chaosDaemon.socketPath='/run/containerd/containerd.sock' \
    --version 2.4.3

kubectl apply -f chaos-mesh-rbac.yaml

helm repo add argo https://argoproj.github.io/argo-helm
helm upgrade --install argo-workflows argo/argo-workflows \
    --create-namespace \
    -n argo-workflows \
    -f argo-workflows-values.yaml

docker build -t ghcr.io/miracum/recruit/chaos-tester:v1 tester/

kind load docker-image ghcr.io/miracum/recruit/chaos-tester:v1

argo submit argo-workflow.yaml -n recruit --watch
```
