variable "aws_region" {
  description = "AWS region"
  type        = string
  default     = "us-east-1"
}

variable "db_password" {
  description = "RDS PostgreSQL master password"
  type        = string
  sensitive   = true
}

variable "jwt_secret" {
  description = "HS256 signing secret (32+ chars)"
  type        = string
  sensitive   = true
}

variable "ecr_image_uri" {
  description = "ECR image URI for the API container"
  type        = string
}
