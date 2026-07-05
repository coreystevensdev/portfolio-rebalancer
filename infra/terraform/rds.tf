resource "aws_security_group" "rds" {
  name        = "portfolio-rds-sg"
  description = "Allow PostgreSQL access from EC2"
  vpc_id      = data.aws_vpc.default.id

  ingress {
    from_port       = 5432
    to_port         = 5432
    protocol        = "tcp"
    security_groups = [aws_security_group.ec2.id]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

resource "aws_db_subnet_group" "main" {
  name       = "portfolio-db-subnet"
  subnet_ids = data.aws_subnets.default.ids
}

resource "aws_db_instance" "postgres" {
  identifier             = "portfolio-rebalancer-db"
  engine                 = "postgres"
  engine_version         = "16"
  instance_class         = "db.t3.micro"  # Free tier eligible (12 months).
  allocated_storage      = 20
  max_allocated_storage  = 100
  storage_encrypted      = true
  db_name                = "portfolio_rebalancer"
  username               = "portfolio"
  password               = var.db_password
  db_subnet_group_name   = aws_db_subnet_group.main.name
  vpc_security_group_ids = [aws_security_group.rds.id]
  publicly_accessible    = false
  skip_final_snapshot    = true
  deletion_protection    = false

  tags = { Name = "portfolio-rebalancer-db" }
}
