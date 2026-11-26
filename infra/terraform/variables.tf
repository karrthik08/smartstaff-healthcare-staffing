variable "aws_region" {
  description = "AWS region for the demonstration or production stack."
  type        = string
  default     = "us-east-1"
}

variable "project_name" {
  type    = string
  default = "smartstaff"
}

variable "environment" {
  type    = string
  default = "demo"
}

variable "vpc_cidr" {
  type    = string
  default = "10.40.0.0/16"
}

variable "acm_certificate_arn" {
  description = "Existing ACM certificate in this region for the load balancer HTTPS listener. Do not create a certificate in this configuration."
  type        = string
}

variable "db_engine_version" {
  description = "RDS PostgreSQL engine version that the selected region actually offers. Confirm availability before apply. Local development uses PostgreSQL 18."
  type        = string
  default     = "16"
}

variable "db_instance_class" {
  type    = string
  default = "db.t4g.micro"
}

variable "db_allocated_storage" {
  type    = number
  default = 20
}

variable "db_multi_az" {
  type    = bool
  default = false
}

variable "db_deletion_protection" {
  type    = bool
  default = true
}

variable "fargate_cpu" {
  type    = number
  default = 256
}

variable "fargate_memory" {
  type    = number
  default = 512
}

variable "desired_count" {
  type    = number
  default = 1
}

variable "enable_nat_gateway" {
  description = "Private tasks need a NAT gateway or VPC endpoints for outbound AWS APIs. One NAT gateway is the lower-cost default."
  type        = bool
  default     = true
}

variable "log_retention_days" {
  type    = number
  default = 14
}

variable "cloudfront_price_class" {
  type    = string
  default = "PriceClass_100"
}

variable "cors_origins" {
  description = "Comma-separated browser origins allowed to call the API. Required before apply."
  type        = string
}

variable "github_repository" {
  description = "GitHub repository allowed to assume the deployment role, in owner/name form."
  type        = string
}

variable "frontend_domain_name" {
  description = "Optional CloudFront alias. Leave empty until a domain and certificate exist."
  type        = string
  default     = ""
}
