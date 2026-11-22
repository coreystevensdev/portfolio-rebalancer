# Helm chart, a path not taken

**This is not how portfolio-rebalancer is deployed.** The live service is a
container started by `docker run` over SSM on a shared `t3.small`, bound to
`127.0.0.1:8084` behind Caddy, with a `--memory=288m` ceiling and one replica.
`.github/workflows/deploy.yml` is the real path.

The chart describes a Kubernetes deployment that has never been applied, and
three values say so plainly once you look:

- `image.repository` is `ghcr.io/coreystevensdev/portfolio-rebalancer`. Nothing
  publishes there; CI pushes to ECR.
- `ingress.hosts[0].host` is `portfolio-rebalancer.example.com`, a placeholder.
- `autoscaling` is on with `minReplicas: 2` and `maxReplicas: 5`. The real
  deployment is one container with a 288MB ceiling, so the HPA block describes
  roughly ten times the footprint that exists.

`helm lint` runs in CI, which keeps the templates valid. Valid is not applied,
and a passing lint job next to a chart nothing deploys is the thing most likely
to read as a live cluster.

Kept rather than deleted. The chart is real work and `templates/secret.yaml` is
the most useful file in it: it renders nothing on purpose and explains why,
that Helm-managed secrets with plaintext values are an anti-pattern, with the
`kubectl create secret` line to use instead. That reasoning is worth keeping
whether or not a cluster ever arrives.
