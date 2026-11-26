output "vpc_id" {
  value = aws_vpc.main.id
}

output "api_repository_url" {
  value = aws_ecr_repository.api.repository_url
}

output "load_balancer_dns_name" {
  value = aws_lb.api.dns_name
}

output "cloudfront_domain_name" {
  value = aws_cloudfront_distribution.frontend.domain_name
}

output "frontend_bucket" {
  value = aws_s3_bucket.frontend.bucket
}

output "deploy_role_arn" {
  value = aws_iam_role.deploy.arn
}

output "database_endpoint" {
  value = aws_db_instance.main.address
}
