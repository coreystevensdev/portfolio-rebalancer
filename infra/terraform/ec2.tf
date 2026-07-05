resource "aws_security_group" "ec2" {
  name        = "portfolio-ec2-sg"
  description = "Allow HTTP and SSH"
  vpc_id      = data.aws_vpc.default.id

  ingress {
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  ingress {
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  ingress {
    from_port   = 22
    to_port     = 22
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

resource "aws_ecr_repository" "api" {
  name                 = "portfolio-rebalancer"
  image_tag_mutability = "MUTABLE"

  image_scanning_configuration {
    scan_on_push = true
  }
}

resource "aws_iam_role" "ec2" {
  name = "portfolio-rebalancer-ec2-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action    = "sts:AssumeRole"
      Effect    = "Allow"
      Principal = { Service = "ec2.amazonaws.com" }
    }]
  })
}

resource "aws_iam_role_policy_attachment" "ecr_pull" {
  role       = aws_iam_role.ec2.name
  policy_arn = "arn:aws:iam::aws:policy/AmazonEC2ContainerRegistryReadOnly"
}

resource "aws_iam_instance_profile" "ec2" {
  name = "portfolio-rebalancer-ec2-profile"
  role = aws_iam_role.ec2.name
}

locals {
  postgres_conn = "Host=${aws_db_instance.postgres.address};Port=5432;Database=portfolio_rebalancer;Username=portfolio;Password=${var.db_password};SSL Mode=Require"
}

resource "aws_instance" "api" {
  ami                    = data.aws_ami.amazon_linux.id
  instance_type          = "t2.micro"  # Free tier eligible (12 months).
  iam_instance_profile   = aws_iam_instance_profile.ec2.name
  vpc_security_group_ids = [aws_security_group.ec2.id]

  user_data = base64encode(<<-EOF
    #!/bin/bash
    yum install -y docker
    systemctl enable docker
    systemctl start docker

    aws ecr get-login-password --region ${var.aws_region} | \
      docker login --username AWS --password-stdin ${aws_ecr_repository.api.repository_url}

    docker run -d \
      --restart=always \
      -p 80:8080 \
      -e "ConnectionStrings__Postgres=${local.postgres_conn}" \
      -e "JWT_SECRET=${var.jwt_secret}" \
      -e "ASPNETCORE_ENVIRONMENT=Production" \
      ${var.ecr_image_uri}
    EOF
  )

  tags = { Name = "portfolio-rebalancer-api" }

  lifecycle {
    ignore_changes = [user_data, ami]
  }
}
