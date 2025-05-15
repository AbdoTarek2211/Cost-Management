# Cost Management

## Overview
This project provides a cost management solution with two implementations: a RESTful API built with ASP.NET Core and a Command-Line Interface (CLI) application written in C#. Both versions enable invoice creation, payment tracking, and PDF receipt generation, with features for cost tracking, invoice status management, and summary reports. The in-memory data repository is used for simplicity, making it suitable for demonstration or learning purposes.

## Features
- **Cost Management**: Create and retrieve cost entries with details like description, amount, date, and category.
- **Invoice Management**: Create, update, and retrieve invoices with support for items, tax calculations based on region, and discounts (fixed or percentage-based).
- **Payment Tracking**: Record payments for invoices and track their status (e.g., Paid, Partial, Overdue).
- **PDF Receipt Generation**: Generate PDF receipts using QuestPDF.
- **Summary Reports**: Generate tabular or text summary reports of invoice statuses, client data, date ranges, and statistics.
- **Due Reminders**: Retrieve invoices due within a specified period.

## Technologies Used
- **ASP.NET Core**: For building the RESTful API.
- **C#**: Primary programming language for both API and CLI.
- **QuestPDF**: For generating PDF receipts.
- **In-Memory Data Storage**: Used for simplicity (can be replaced with a database like SQL Server).

## Prerequisites
- [.NET SDK](https://dotnet.microsoft.com/download) (version 3.1 or later; .NET 6+ recommended)
- A code editor like [Visual Studio](https://visualstudio.microsoft.com/) or [VS Code](https://code.visualstudio.com/)
- [Postman](https://www.postman.com/) or a similar tool for API testing (optional)

## Setup Instructions
### API Setup
1. **Clone the Repository**:
   ```bash
   git clone https://github.com/<your-username>/cost-management-assignment-api.git
   cd cost-management-assignment-api
   ```

2. **Create New WebAPI**:
   Run:
   ```bash
   dotnet new webapi
   ```

3. **Replace The code inside Program.cs with the code of Cost Management Assignment API.cs**

4. **Install QuestPDF**:
   The project uses QuestPDF for PDF generation. Add the package if not already included:
   ```bash
   dotnet add package QuestPDF
   ```

5. **Run the Application**:
   Start the API using:
   ```bash
   dotnet run
   ```
   The API will typically be hosted at `http://localhost:5000` or `https://localhost:5001` (check the terminal output for the exact URL).

### CLI Setup
1. **Clone the Repository** (if not already cloned):
   ```bash
   git clone https://github.com/<your-username>/cost-management-assignment-api.git
   cd cost-management-assignment-api
   ```

2. **Navigate to CLI Project**:
   Ensure you are in the directory containing the CLI file (e.g., `Cost Management Assignment CLI.cs`).

3. **Restore Dependencies**:
   Run:
   ```bash
   dotnet restore
   ```

4. **Install QuestPDF**:
   Add the QuestPDF package if not included:
   ```bash
   dotnet add package QuestPDF
   ```

5. **Run the CLI**:
   Compile and run the CLI using:
   ```bash
   dotnet run --project Cost Management Assignment CLI.cs
   ```
   Follow the interactive menu to use the application.

## API Endpoints
The API exposes the following endpoints. Use a tool like Postman to interact with them.

### Costs
- **GET /api/costs**  
  Retrieve all costs.  
  **Response**: `200 OK` with a list of `CostResponse` objects.

- **GET /api/costs/{id}**  
  Retrieve a specific cost by ID.  
  **Example**: `/api/costs/1`  
  **Response**: `200 OK` with a `CostResponse` object, or `404 Not Found`.

- **POST /api/costs**  
  Create a new cost.  
  **Body** (JSON):
  ```json
  {
    "Description": "New Cost",
    "Amount": 50.00,
    "Category": "Miscellaneous"
  }
  ```
  **Response**: `201 Created` with the new `CostResponse`.

### Invoices
- **GET /api/invoices**  
  Retrieve all invoices.  
  **Response**: `200 OK` with a list of `InvoiceResponse` objects.

- **GET /api/invoices/{id}**  
  Retrieve a specific invoice by ID.  
  **Example**: `/api/invoices/1`  
  **Response**: `200 OK` with an `InvoiceResponse`, or `404 Not Found`.

- **POST /api/invoices**  
  Create a new invoice.  
  **Body** (JSON):
  ```json
  {
    "ClientId": 1002,
    "ClientName": "John Doe",
    "ClientEmail": "john@example.com",
    "ClientPhone": "+1234567890",
    "Region": "SA",
    "DiscountKind": "Percentage",
    "Discount": 5.0,
    "DueInDays": 15,
    "Items": [
      {
        "Name": "Consulting Service",
        "UnitPrice": 200,
        "Quantity": 2
      }
    ]
  }
  ```
  **Response**: `201 Created` with the new `InvoiceResponse`.

- **PUT /api/invoices/{id}**  
  Update an existing invoice.  
  **Example**: `/api/invoices/1`  
  **Body** (JSON):
  ```json
  {
    "Region": "AE",
    "DiscountKind": "Fixed",
    "Discount": 10.0,
    "DueInDays": 20,
    "Items": [
      {
        "Name": "Updated Service",
        "UnitPrice": 150,
        "Quantity": 1
      }
    ]
  }
  ```
  **Response**: `200 OK` with the updated `InvoiceResponse`.

- **GET /api/invoices/due-reminders**  
  Retrieve invoices due within a specified period.  
  **Query Parameter**: `daysUntilDue` (default: 7)  
  **Example**: `/api/invoices/due-reminders?daysUntilDue=10`  
  **Response**: `200 OK` with a list of `DueReminderResponse` objects.

- **GET /api/invoices/report/status**  
  Generate a status report for invoices.  
  **Query Parameter**: `status` (optional)  
  **Example**: `/api/invoices/report/status?status=Overdue`  
  **Response**: `200 OK` with a `ReportResponse`.

### Payments
- **GET /api/payments/invoice/{invoiceId}**  
  Retrieve all payments for a specific invoice.  
  **Example**: `/api/payments/invoice/1`  
  **Response**: `200 OK` with a list of `PaymentResponse` objects.

- **POST /api/payments/invoice/{invoiceId}**  
  Record a new payment for an invoice.  
  **Example**: `/api/payments/invoice/1`  
  **Body** (JSON):
  ```json
  {
    "Amount": 100.00,
    "Method": "Credit Card"
  }
  ```
  **Response**: `201 Created` with the new `PaymentResponse`.

- **GET /api/payments/{paymentId}/receipt**  
  Generate a PDF receipt for a payment.  
  **Example**: `/api/payments/1/receipt`  
  **Response**: `200 OK` with a PDF file, or `404 Not Found`.

## CLI Usage
The CLI provides an interactive menu-driven interface for managing costs and invoices. Run the CLI and select options from the menu.

### Menu Options
1. **Log a new cost entry**: Add a new cost with description, amount, and category.
2. **Create a new invoice**: Create an invoice with client details, items, region, discount, and due date.
3. **Edit an invoice**: Modify an existing invoice's client name, due date, or items.
4. **Log a payment**: Record a payment for an invoice and update its status.
5. **Show all invoices**: Display summaries of all invoices.
6. **Show invoice by ID**: Display a specific invoice's details.
7. **Generate PDF receipt**: Create a PDF receipt for an invoice's latest payment.
8. **Show due reminders**: List invoices due within 7 days or marked as overdue.
9. **Generate reports**: Access various report types (status, client, date range, statistics).
10. **Exit**: Quit the application.

### Report Sub-Menu
- **Status Report**: Filter invoices by status.
- **Client Report**: Filter invoices by client name.
- **Date Range Report**: Filter invoices by a date range.
- **Summary Statistics**: Show aggregated data by status and client.
- **Back to Main Menu**: Return to the main menu.

### Example Interaction
```
--- Local Cost Manager Menu ---
1. Log a new cost entry
2. Create a new invoice
3. Edit an invoice
4. Log a payment
5. Show all invoices
6. Show invoice by ID
7. Generate PDF receipt
8. Show due reminders
9. Generate reports
10. Exit
Select an option: 2

--- Create New Invoice ---
Client ID: 1002
Client Name: Jane Doe
Region Code (e.g., CA, NY): SA
Discount Type (1-Fixed, 2-Percentage): 2
Discount Amount (%): 5
Due in how many days? 15
Item Name (or 'done' to finish): Consulting
Unit Price: 200
Quantity: 2
Item Name (or 'done' to finish): done
Invoice #1 created successfully!
Grand Total: $403.00 (Subtotal: $400.00, Tax: $56.00, Discount: $53.00)
```

## Project Structure
- **Models**: Core data models (`Cost`, `Invoice`, `Payment`, `InvoiceItem`).
- **Services**: Business logic (`InvoiceStatusTracker`, `PaymentHistoryLogger`, `InvoiceSummaryReport`, `PdfReceiptGenerator`).
- **Utilities**: Validation logic (`Validator`).
- **CLI**: Main program logic in `Program` class.

## Notes
- The project uses an in-memory repository for simplicity. For production, consider replacing it with a database.
- The `Startup` class (API) is compatible with .NET Core 3.1. For .NET 6+, refactor to the minimal hosting model.
- Ensure the QuestPDF Community License meets your usage requirements.
