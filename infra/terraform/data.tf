resource "random_password" "database" {
  length  = 32
  special = false
}

resource "random_password" "jwt" {
  length  = 48
  special = false
}

resource "aws_secretsmanager_secret" "database" {
  name = "${local.name}/database"
}

resource "aws_secretsmanager_secret_version" "database" {
  secret_id = aws_secretsmanager_secret.database.id
  secret_string = jsonencode({
    username = "smartstaff"
    password = random_password.database.result
    dbname   = "smartstaff"
    host     = aws_db_instance.main.address
    port     = 5432
  })
}

resource "aws_secretsmanager_secret" "jwt" {
  name = "${local.name}/jwt"
}

resource "aws_secretsmanager_secret_version" "jwt" {
  secret_id     = aws_secretsmanager_secret.jwt.id
  secret_string = random_password.jwt.result
}

resource "aws_db_subnet_group" "main" {
  name       = local.name
  subnet_ids = aws_subnet.private[*].id
}

resource "aws_db_instance" "main" {
  identifier                = local.name
  engine                    = "postgres"
  engine_version            = var.db_engine_version
  instance_class            = var.db_instance_class
  allocated_storage         = var.db_allocated_storage
  storage_type              = "gp3"
  storage_encrypted         = true
  db_name                   = "smartstaff"
  username                  = "smartstaff"
  password                  = random_password.database.result
  db_subnet_group_name      = aws_db_subnet_group.main.name
  vpc_security_group_ids    = [aws_security_group.database.id]
  publicly_accessible       = false
  multi_az                  = var.db_multi_az
  backup_retention_period   = 7
  deletion_protection       = var.db_deletion_protection
  skip_final_snapshot       = false
  final_snapshot_identifier = "${local.name}-final"
  copy_tags_to_snapshot     = true
}

resource "aws_s3_bucket" "frontend" {
  bucket_prefix = "${local.name}-web-"
}

resource "aws_s3_bucket_public_access_block" "frontend" {
  bucket                  = aws_s3_bucket.frontend.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_server_side_encryption_configuration" "frontend" {
  bucket = aws_s3_bucket.frontend.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

resource "aws_ecr_repository" "api" {
  name                 = "${local.name}-api"
  image_tag_mutability = "MUTABLE"

  image_scanning_configuration {
    scan_on_push = true
  }

  encryption_configuration {
    encryption_type = "AES256"
  }
}

resource "aws_cloudwatch_log_group" "api" {
  name              = "/ecs/${local.name}"
  retention_in_days = var.log_retention_days
}
