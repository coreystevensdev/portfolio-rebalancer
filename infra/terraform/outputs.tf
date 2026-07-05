output "api_public_ip" {
  description = "EC2 public IP for the API"
  value       = aws_instance.api.public_ip
}

output "ecr_repository_url" {
  description = "ECR repository URL for pushing images"
  value       = aws_ecr_repository.api.repository_url
}

output "rds_endpoint" {
  description = "RDS PostgreSQL endpoint"
  value       = aws_db_instance.postgres.address
}
