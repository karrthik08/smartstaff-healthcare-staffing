# Cost estimate

These are formulas, not a bill. Public AWS prices were not retrieved for this document, so no monthly dollar total is claimed. Check the current prices before apply:

- https://aws.amazon.com/vpc/pricing/
- https://aws.amazon.com/elasticloadbalancing/pricing/
- https://aws.amazon.com/fargate/pricing/
- https://aws.amazon.com/rds/postgresql/pricing/
- https://aws.amazon.com/cloudfront/pricing/
- https://aws.amazon.com/cloudwatch/pricing/
- https://aws.amazon.com/secrets-manager/pricing/
- https://aws.amazon.com/ecr/pricing/
- https://aws.amazon.com/s3/pricing/

## Demonstration formula

```
monthly estimate =
  nat_gateway_hourly_price * hours * nat_gateway_count
  + nat_data_processed_gb * nat_data_price
  + alb_hourly_price * hours
  + alb_lcu_hours * lcu_price
  + fargate_vcpu_hour_price * (fargate_cpu / 1024) * hours * desired_count
  + fargate_gb_hour_price * (fargate_memory / 1024) * hours * desired_count
  + rds_instance_hour_price * hours
  + rds_storage_gb_month_price * allocated_storage
  + rds_backup_gb_month_price * backup_gb_beyond_free_allocation
  + cloudfront_request_price * requests
  + cloudfront_data_out_price * gb_out
  + secrets_manager_secret_month_price * 2
  + cloudwatch_ingestion_gb_price * log_gb
  + ecr_storage_gb_month_price * image_gb
  + s3_storage_gb_month_price * frontend_gb
```

The default demonstration uses one NAT gateway, one Application Load Balancer, one 0.25 vCPU / 512 MB Fargate task, one single-AZ `db.t4g.micro` database, 20 GB of storage, CloudFront price class 100, two secrets, and 14 days of logs.

## What dominates the bill

A NAT gateway is billed for every hour it exists and for data that passes through it, even when the application is idle. A load balancer is also billed hourly. Those two are usually larger than a tiny Fargate task or a small frontend bucket. RDS is the next steady cost. CloudFront and CloudWatch stay small until traffic or log volume grows. A second NAT gateway, Multi-AZ RDS, or a larger Fargate size changes the formula directly.

Turning `enable_nat_gateway` off removes the NAT terms, but private tasks then need another way to reach ECR, Secrets Manager, and CloudWatch. Interface VPC endpoints add their own hourly and data charges and are not included in this configuration.
