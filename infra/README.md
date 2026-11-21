# Infrastructure, a path not taken

**This is not how portfolio-rebalancer is deployed.** The live service runs as a
container named `rebalancer` on a shared `t3.small`, started by `docker run` over
SSM and bound to `127.0.0.1:8084`, behind Caddy. `.github/workflows/deploy.yml`
is the real path.

The Terraform here stands up a dedicated instance publishing `-p 80:8080` with
the connection string and JWT secret interpolated into `user_data`. Nothing
deploys that way. Only the ECR repository and the GitHub OIDC role are consumed
by the live workflow, and applying the rest bills an instance the project does
not use.

Kept because the Terraform is real and reviewable, not as a runbook to follow.


AWS deployment using Terraform. Uses EC2 t2.micro + RDS db.t3.micro, both free-tier eligible for 12 months on a new AWS account.

## Prerequisites

- AWS account with free-tier eligibility
- Terraform 1.9+ (`tfenv install 1.9.8`)
- AWS CLI configured (`aws configure`)
- Docker + ECR access

## First-time setup

### 1. Create the S3 backend bucket

```bash
aws s3api create-bucket \
  --bucket coreystevensdev-tfstate \
  --region us-east-1
aws s3api put-bucket-versioning \
  --bucket coreystevensdev-tfstate \
  --versioning-configuration Status=Enabled
```

### 2. Create OIDC trust for GitHub Actions

```bash
aws iam create-open-id-connect-provider \
  --url https://token.actions.githubusercontent.com \
  --client-id-list sts.amazonaws.com \
  --thumbprint-list 6938fd4d98bab03faadb97b34396831e3780aea1
```

Then create an IAM role that trusts the OIDC provider and set `AWS_ROLE_ARN` as a GitHub Actions secret. The workflow reads that exact name, and a secret called `AWS_DEPLOY_ROLE_ARN` leaves `role-to-assume` empty, which fails OIDC with nothing obvious to point at.

### 3. Create terraform.tfvars

```hcl
db_password   = "strong-password-here"
jwt_secret    = "your-32-plus-character-jwt-secret"
ecr_image_uri = "123456789.dkr.ecr.us-east-1.amazonaws.com/portfolio-rebalancer:latest"
```

### 4. Apply

```bash
cd infra/terraform
terraform init
terraform plan
terraform apply
```

Terraform outputs the EC2 public IP and ECR URL.

### 5. Push the first image

```bash
aws ecr get-login-password --region us-east-1 | \
  docker login --username AWS --password-stdin <ecr-url>

docker build -t <ecr-url>:latest .
docker push <ecr-url>:latest
```

### 6. Add GitHub secrets

Set these in the repo's Settings > Secrets > Actions:
- `AWS_ROLE_ARN`
- `EC2_INSTANCE_ID`
- `ECR_REPO`

`POSTGRES_CONN_STRING` and `JWT_SECRET` are not GitHub secrets. The deploy reads
them from SSM Parameter Store at `/rebalancer/postgres-conn` and
`/rebalancer/jwt-secret`, on the box, so they never enter a workflow log. No
Terraform in this directory creates those parameters.

After that, every push to `main` triggers the deploy workflow automatically.
