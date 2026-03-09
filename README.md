SportsStore – Enhanced ASP.NET Core Application
Overview

SportsStore is an ASP.NET Core web application for managing and purchasing sports products.
This project was enhanced with several improvements including structured logging, Stripe payment integration, and a CI pipeline.

The main objectives of these enhancements were to improve application observability, introduce secure payment processing, and automate build and test validation.

Upgrade Steps

The original project was upgraded with the following features.

Structured Logging with Serilog

Serilog was integrated into the application to provide structured logging. Logging configuration is managed through the application configuration files.

The following packages were added:

Serilog.AspNetCore

Serilog.Settings.Configuration

Serilog.Sinks.Console

Serilog.Sinks.File

Serilog.Sinks.Seq

Serilog.Enrichers.Environment

Logging captures important events such as:

Application startup

Checkout and payment flow

Order creation

Exceptions and errors

HTTP request information

Logs are written to multiple destinations:

Console output

Rolling log files

Seq log server

Additional log enrichment includes machine name, environment name, and correlation IDs to trace requests across the application.

Stripe Payment Integration

Stripe Checkout was integrated using the official Stripe .NET SDK.

The Stripe package used:

Stripe.net

A dedicated payment service was created to separate payment logic from the rest of the application:

IPaymentService

StripePaymentService

The checkout process works as follows:

The user submits checkout details.

The application creates a Stripe Checkout Session.

The user is redirected to the Stripe hosted payment page.

After payment, Stripe redirects the user back to the application.

The application verifies the payment status.

The order is saved only after payment confirmation.

The system handles:

Successful payments

Cancelled payments

Failed payments

Order Model Enhancements

The Order model was extended to store Stripe payment confirmation details.

The following fields were added:

StripeCheckoutSessionId

StripePaymentIntentId

StripePaymentStatus

PaymentConfirmedAtUtc

These fields ensure that payment confirmation is stored together with each order.

Secure Stripe Configuration

Stripe API keys are not stored in the repository.

For local development, Stripe keys are stored using User Secrets.

Example setup:

dotnet user-secrets init
dotnet user-secrets set "Stripe:SecretKey" "sk_test_your_key"
dotnet user-secrets set "Stripe:PublishableKey" "pk_test_your_key"

For production environments, environment variables should be used instead of user secrets.

Logging Setup

Logging is configured using Serilog through the application configuration files.

Logs are written to:

Console

Rolling file logs stored in the Logs directory

Seq server

Seq provides a web interface that allows developers to search and filter structured log events.

Default local Seq address:

http://localhost:5341

Example logged events include:

Checkout submitted

Stripe session created

Payment confirmed

Order created

Payment failures

Structured properties recorded in logs include values such as OrderId, SessionId, PaymentIntentId, CartLineCount, and CorrelationId.

How to Run Locally
Prerequisites

Install the following software:

.NET SDK 9.0

SQL Server LocalDB

Stripe account (test mode)

Seq (optional for log visualization)

1. Clone the Repository

git clone <repository-url>
cd <repository-folder>

2. Restore Dependencies

dotnet restore

3. Configure Stripe Test Keys

Set the Stripe API keys using user secrets:

dotnet user-secrets init
dotnet user-secrets set "Stripe:SecretKey" "sk_test_your_key"
dotnet user-secrets set "Stripe:PublishableKey" "pk_test_your_key"

4. Apply Database Migrations

dotnet ef database update

If Entity Framework tools are not installed:

dotnet tool install --global dotnet-ef

5. Run the Application

dotnet run --project SportsStore

After running the application, open the URL displayed in the terminal or browser window.

6. Test Stripe Payment

Use the Stripe test card number:

4242 4242 4242 4242

Use any future expiry date and any three-digit CVC code.

Continuous Integration

A GitHub Actions workflow was added to automate build and test validation.

Workflow file location:

.github/workflows/dotnet-ci.yml

The pipeline performs the following steps:

Restore project dependencies

Build the solution

Run unit tests

Upload test result artifacts

The pipeline runs automatically on:

Pull requests targeting the main branch

Pushes to the main branch

If tests fail, the workflow fails automatically.

Summary

This project demonstrates the integration of structured logging, secure payment processing, and automated CI validation in an ASP.NET Core application.

Key improvements include:

Structured logging with Serilog

Payment processing with Stripe Checkout

Secure configuration of API keys

Automated CI pipeline using GitHub Actions

These enhancements improve application reliability, maintainability, and observability.
