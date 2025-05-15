using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

// Models
public class Cost
{
    public int Id { get; set; }
    public string Description { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string Category { get; set; }
}

public class InvoiceItem
{
    public string Name { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

public class Payment
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; }
    public string Method { get; set; }
}

public class Invoice
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string ClientName { get; set; }
    public string ClientEmail { get; set; }
    public string ClientPhone { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime DueDate { get; set; }
    public string Status { get; set; }
    public string Region { get; set; }
    public decimal Discount { get; set; }
    public List<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
    public List<Payment> Payments { get; set; } = new List<Payment>();
    public decimal Subtotal => Items.Sum(item => item.UnitPrice * item.Quantity);
    public decimal Tax => CalculateTax(Subtotal, Region);

    public enum DiscountType
    {
        Fixed,
        Percentage
    }

    public DiscountType DiscountKind { get; set; }
    public decimal GrandTotal => DiscountKind == DiscountType.Percentage ? 
        (Subtotal + Tax) * (1 - Discount / 100) : 
        Subtotal + Tax - Discount;
    public decimal TotalPaid => Payments.Sum(p => p.Amount);
    public decimal TotalDue => GrandTotal - TotalPaid;

    private decimal CalculateTax(decimal amount, string countryCode)
    {
        return countryCode?.ToUpper() switch
        {
            "SA" => amount * 0.15m,
            "AE" => amount * 0.05m,
            "EG" => amount * 0.14m,
            "MA" => amount * 0.20m,
            "TN" => amount * 0.19m,
            "LY" => amount * 0.15m,
            "JO" => amount * 0.16m,
            "IQ" => amount * 0.15m,
            "PS" => amount * 0.16m,
            _ => amount * 0.05m
        };
    }
}

// DTOs
public class CreateCostRequest
{
    public string Description { get; set; }
    public decimal Amount { get; set; }
    public string Category { get; set; }
}

public class CostResponse
{
    public int Id { get; set; }
    public string Description { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string Category { get; set; }
}

public class CreateInvoiceRequest
{
    public int ClientId { get; set; }
    public string ClientName { get; set; }
    public string ClientEmail { get; set; }
    public string ClientPhone { get; set; }
    public string Region { get; set; }
    public Invoice.DiscountType DiscountKind { get; set; }
    public decimal Discount { get; set; }
    public int DueInDays { get; set; }
    public List<InvoiceItemRequest> Items { get; set; }
}

public class UpdateInvoiceRequest
{
    public string Region { get; set; }
    public Invoice.DiscountType? DiscountKind { get; set; }
    public decimal? Discount { get; set; }
    public int? DueInDays { get; set; }
    public List<InvoiceItemRequest> Items { get; set; }
}

public class InvoiceItemRequest
{
    public string Name { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

public class InvoiceResponse
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string ClientName { get; set; }
    public string ClientEmail { get; set; }
    public string ClientPhone { get; set; }
    public string Region { get; set; }
    public string Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime DueDate { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Discount { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalDue { get; set; }
    public List<InvoiceItemResponse> Items { get; set; }
    public List<PaymentResponse> Payments { get; set; }
}

public class InvoiceItemResponse
{
    public string Name { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

public class CreatePaymentRequest
{
    public decimal Amount { get; set; }
    public string Method { get; set; }
}

public class PaymentResponse
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; }
    public string Method { get; set; }
}

public class DueReminderResponse
{
    public int InvoiceId { get; set; }
    public DateTime DueDate { get; set; }
    public decimal TotalDue { get; set; }
    public string Status { get; set; }
    public string ClientName { get; set; }
    public string ClientEmail { get; set; }
    public string ClientPhone { get; set; }
}

public enum ReportType
{
    Tabular,
    Graphical
}

public class ReportResponse
{
    public string Title { get; set; }
    public ReportType Type { get; set; } = ReportType.Tabular;
    public List<string> Columns { get; set; }
    public List<Dictionary<string, object>> Rows { get; set; }
}

// Services
public class InvoiceStatusTracker
{
    public string TrackStatus(Invoice invoice)
    {
        var daysRemaining = (invoice.DueDate - DateTime.Now).Days;
        
        if (invoice.TotalDue <= 0)
        {
            invoice.Status = "Paid";
        }
        else if (invoice.Payments.Any() && invoice.TotalDue < invoice.GrandTotal)
        {
            invoice.Status = "Partial";
        }
        else if (DateTime.Now > invoice.DueDate && invoice.TotalDue > 0)
        {
            invoice.Status = "Overdue";
        }
        else if (invoice.Status == "Draft" && !invoice.Payments.Any())
        {
            // Keep as Draft
        }
        else if (invoice.Payments.Any())
        {
            invoice.Status = "Partial";
        }
        else
        {
            invoice.Status = "Sent";
        }

        return invoice.Status;
    }
}

public class InvoiceSummaryReport
{
    private readonly DataRepository _repository;
    
    public InvoiceSummaryReport(DataRepository repository)
    {
        _repository = repository;
    }

    public ReportResponse GenerateStatusReport(string statusFilter = null)
    {
        var filteredInvoices = _repository.Invoices.AsEnumerable();
        if (!string.IsNullOrEmpty(statusFilter))
        {
            filteredInvoices = filteredInvoices.Where(i => i.Status.Equals(statusFilter, StringComparison.OrdinalIgnoreCase));
        }

        return CreateReport(
            title: $"Invoice Status Report {(statusFilter != null ? $"({statusFilter})" : "")}",
            columns: new[] { "ID", "Client", "Created", "Due Date", "Amount Due", "Status" },
            rows: filteredInvoices.OrderBy(i => i.Status).ThenBy(i => i.DueDate)
                .Select(i => new object[] { i.Id, i.ClientName, i.CreatedAt.ToShortDateString(), i.DueDate.ToShortDateString(), i.TotalDue, i.Status })
        );
    }

    private ReportResponse CreateReport(string title, string[] columns, IEnumerable<object[]> rows)
    {
        var columnNames = columns.ToList();
        var rowData = rows.Select(row => 
        {
            var dict = new Dictionary<string, object>();
            for (int i = 0; i < columnNames.Count; i++)
            {
                dict[columnNames[i]] = row[i];
            }
            return dict;
        }).ToList();

        return new ReportResponse
        {
            Title = title,
            Type = ReportType.Tabular,
            Columns = columnNames,
            Rows = rowData
        };
    }
}

public class PdfReceiptGenerator
{
    private IContainer CellStyle(IContainer container) => container
        .BorderBottom(1)
        .BorderColor(Colors.Grey.Lighten2)
        .PaddingVertical(5);

    public byte[] GenerateReceipt(Invoice invoice, Payment payment)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        
        var stream = new MemoryStream();
        
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(12));
                page.Header().AlignCenter().Text("PAYMENT RECEIPT").SemiBold().FontSize(18).FontColor(Colors.Blue.Medium);
                
                page.Content().PaddingVertical(1, Unit.Centimetre).Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text($"Receipt #: {payment.Id}");
                            col.Item().Text($"Date: {payment.PaidAt:yyyy-MM-dd HH:mm}");
                            col.Item().Text($"Payment Method: {payment.Method}");
                        });
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text($"Invoice #: {invoice.Id}");
                            col.Item().Text($"Client: {invoice.ClientName}");
                        });
                    });
                    
                    column.Item().PaddingTop(15).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    
                    column.Item().PaddingTop(10).Text("ITEMS").SemiBold();
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(10, Unit.Millimetre);
                            columns.RelativeColumn(3);
                            columns.ConstantColumn(2, Unit.Centimetre);
                            columns.ConstantColumn(3, Unit.Centimetre);
                            columns.ConstantColumn(3, Unit.Centimetre);
                        });
                        
                        table.Header(header =>
                        {
                            header.Cell().Text("#");
                            header.Cell().Text("Description");
                            header.Cell().AlignRight().Text("Qty");
                            header.Cell().AlignRight().Text("Unit Price");
                            header.Cell().AlignRight().Text("Amount");
                            header.Cell().ColumnSpan(5).PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                        });
                        
                        for (int i = 0; i < invoice.Items.Count; i++)
                        {
                            var item = invoice.Items[i];
                            var number = i + 1;
                            table.Cell().Element(CellStyle).Text(number.ToString());
                            table.Cell().Element(CellStyle).Text(item.Name);
                            table.Cell().Element(CellStyle).AlignRight().Text(item.Quantity.ToString());
                            table.Cell().Element(CellStyle).AlignRight().Text(item.UnitPrice.ToString("C"));
                            table.Cell().Element(CellStyle).AlignRight().Text((item.UnitPrice * item.Quantity).ToString("C"));
                        }
                    });
                    
                    column.Item().AlignRight().PaddingTop(10).Column(summary =>
                    {
                        summary.Item().Row(row =>
                        {
                            row.RelativeItem();
                            row.ConstantItem(100).Text("Subtotal:").SemiBold();
                            row.ConstantItem(100).AlignRight().Text(invoice.Subtotal.ToString("C"));
                        });
                        summary.Item().Row(row =>
                        {
                            row.RelativeItem();
                            row.ConstantItem(100).Text($"Tax ({invoice.Region} {invoice.Tax / invoice.Subtotal:P0}):").SemiBold();
                            row.ConstantItem(100).AlignRight().Text(invoice.Tax.ToString("C"));
                        });
                        summary.Item().Row(row =>
                        {
                            row.RelativeItem();
                            row.ConstantItem(100).Text("Total:").SemiBold();
                            row.ConstantItem(100).AlignRight().Text(invoice.GrandTotal.ToString("C"));
                        });
                        summary.Item().Row(row =>
                        {
                            row.RelativeItem();
                            row.ConstantItem(100).Text("Amount Paid:").SemiBold();
                            row.ConstantItem(100).AlignRight().Text(payment.Amount.ToString("C"));
                        });
                        summary.Item().Row(row =>
                        {
                            row.RelativeItem();
                            row.ConstantItem(100).Text("Balance Due:").SemiBold();
                            row.ConstantItem(100).AlignRight().Text((invoice.GrandTotal - payment.Amount).ToString("C"));
                        });
                    });
                    
                    column.Item().PaddingTop(20).AlignCenter().Text("Thank you for your business!");
                });
                
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf(stream);
        
        return stream.ToArray();
    }
}

// Repository
public class DataRepository
{
    public List<Cost> Costs { get; } = new List<Cost>();
    public List<Invoice> Invoices { get; } = new List<Invoice>();
    public List<Payment> Payments { get; } = new List<Payment>();
    
    private int _invoiceIdCounter = 1;
    private int _paymentIdCounter = 1;
    private int _costIdCounter = 1;
    
    public DataRepository()
    {
        // Initialize with sample data
        Costs.Add(new Cost { Id = _costIdCounter++, Description = "Office supplies", Amount = 125.50m, Date = DateTime.Now.AddDays(-10), Category = "Office" });
        
        var invoice = new Invoice
        {
            Id = _invoiceIdCounter++,
            ClientId = 1001,
            ClientName = "Sample Client",
            ClientEmail = "client@example.com",
            ClientPhone = "+1234567890",
            Region = "PS",
            Discount = 10.00m,
            DiscountKind = Invoice.DiscountType.Percentage,
            CreatedAt = DateTime.Now,
            DueDate = DateTime.Now.AddDays(30),
            Status = "Sent",
            Items = new List<InvoiceItem>
            {
                new InvoiceItem { Name = "Website Design", UnitPrice = 500, Quantity = 1 },
                new InvoiceItem { Name = "Hosting (1 year)", UnitPrice = 120, Quantity = 1 }
            }
        };
        Invoices.Add(invoice);
        
        var payment = new Payment 
        { 
            Id = _paymentIdCounter++, 
            InvoiceId = invoice.Id, 
            Amount = 300, 
            PaidAt = DateTime.Now.AddDays(-5), 
            Method = "Bank Transfer" 
        };
        Payments.Add(payment);
        invoice.Payments.Add(payment);
    }
    
    public int GetNextInvoiceId() => _invoiceIdCounter++;
    public int GetNextPaymentId() => _paymentIdCounter++;
    public int GetNextCostId() => _costIdCounter++;
}

// Controllers
[ApiController]
[Route("api/[controller]")]
public class CostsController : ControllerBase
{
    private readonly DataRepository _repository;
    
    public CostsController(DataRepository repository)
    {
        _repository = repository;
    }
    
    [HttpGet]
    public ActionResult<IEnumerable<CostResponse>> GetAll()
    {
        return Ok(_repository.Costs.Select(c => new CostResponse
        {
            Id = c.Id,
            Description = c.Description,
            Amount = c.Amount,
            Date = c.Date,
            Category = c.Category
        }));
    }
    
    [HttpGet("{id}")]
    public ActionResult<CostResponse> GetById(int id)
    {
        var cost = _repository.Costs.FirstOrDefault(c => c.Id == id);
        if (cost == null) return NotFound();
        
        return Ok(new CostResponse
        {
            Id = cost.Id,
            Description = cost.Description,
            Amount = cost.Amount,
            Date = cost.Date,
            Category = cost.Category
        });
    }
    
    [HttpPost]
    public ActionResult<CostResponse> Create([FromBody] CreateCostRequest request)
    {
        var cost = new Cost
        {
            Id = _repository.GetNextCostId(),
            Description = request.Description,
            Amount = request.Amount,
            Date = DateTime.Now,
            Category = request.Category
        };
        
        _repository.Costs.Add(cost);
        
        return CreatedAtAction(nameof(GetById), new { id = cost.Id }, new CostResponse
        {
            Id = cost.Id,
            Description = cost.Description,
            Amount = cost.Amount,
            Date = cost.Date,
            Category = cost.Category
        });
    }
}

[ApiController]
[Route("api/[controller]")]
public class InvoicesController : ControllerBase
{
    private readonly DataRepository _repository;
    private readonly InvoiceStatusTracker _statusTracker;
    private readonly InvoiceSummaryReport _reportGenerator;
    
    public InvoicesController(DataRepository repository, InvoiceStatusTracker statusTracker, InvoiceSummaryReport reportGenerator)
    {
        _repository = repository;
        _statusTracker = statusTracker;
        _reportGenerator = reportGenerator;
    }
    
    [HttpGet]
    public ActionResult<IEnumerable<InvoiceResponse>> GetAll()
    {
        return Ok(_repository.Invoices.Select(i => MapToResponse(i)));
    }
    
    [HttpGet("{id}")]
    public ActionResult<InvoiceResponse> GetById(int id)
    {
        var invoice = _repository.Invoices.FirstOrDefault(i => i.Id == id);
        if (invoice == null) return NotFound();
        
        return Ok(MapToResponse(invoice));
    }
    
    [HttpPost]
    public ActionResult<InvoiceResponse> Create([FromBody] CreateInvoiceRequest request)
    {
        var invoice = new Invoice
        {
            Id = _repository.GetNextInvoiceId(),
            ClientId = request.ClientId,
            ClientName = request.ClientName,
            ClientEmail = request.ClientEmail,
            ClientPhone = request.ClientPhone,
            Region = request.Region,
            DiscountKind = request.DiscountKind,
            Discount = request.Discount,
            CreatedAt = DateTime.Now,
            DueDate = DateTime.Now.AddDays(request.DueInDays),
            Status = "Draft",
            Items = request.Items.Select(i => new InvoiceItem
            {
                Name = i.Name,
                UnitPrice = i.UnitPrice,
                Quantity = i.Quantity
            }).ToList()
        };
        
        _repository.Invoices.Add(invoice);
        
        return CreatedAtAction(nameof(GetById), new { id = invoice.Id }, MapToResponse(invoice));
    }
    
    [HttpPut("{id}")]
    public ActionResult<InvoiceResponse> Update(int id, [FromBody] UpdateInvoiceRequest request)
    {
        var invoice = _repository.Invoices.FirstOrDefault(i => i.Id == id);
        if (invoice == null) return NotFound();

        invoice.Region = request.Region ?? invoice.Region;
        invoice.DiscountKind = request.DiscountKind ?? invoice.DiscountKind;
        invoice.Discount = request.Discount ?? invoice.Discount;
        invoice.DueDate = request.DueInDays.HasValue 
            ? DateTime.Now.AddDays(request.DueInDays.Value) 
            : invoice.DueDate;

        if (request.Items != null)
        {
            invoice.Items = request.Items.Select(i => new InvoiceItem
            {
                Name = i.Name,
                UnitPrice = i.UnitPrice,
                Quantity = i.Quantity
            }).ToList();
        }

        return Ok(MapToResponse(invoice));
    }
    
    [HttpGet("due-reminders")]
    public ActionResult<IEnumerable<DueReminderResponse>> GetDueReminders([FromQuery] int? daysUntilDue = 7)
    {
        var cutoffDate = DateTime.Now.AddDays(daysUntilDue.Value);
        var reminders = _repository.Invoices
            .Where(i => i.DueDate <= cutoffDate && i.Status != "Paid")
            .Select(i => new DueReminderResponse
            {
                InvoiceId = i.Id,
                DueDate = i.DueDate,
                TotalDue = i.TotalDue,
                Status = i.Status,
                ClientName = i.ClientName,
                ClientEmail = i.ClientEmail,
                ClientPhone = i.ClientPhone
            }).ToList();

        return Ok(reminders);
    }
    
    [HttpGet("report/status")]
    public ActionResult<ReportResponse> GetStatusReport([FromQuery] string status)
    {
        return Ok(_reportGenerator.GenerateStatusReport(status));
    }
    
    private InvoiceResponse MapToResponse(Invoice invoice)
    {
        return new InvoiceResponse
        {
            Id = invoice.Id,
            ClientId = invoice.ClientId,
            ClientName = invoice.ClientName,
            ClientEmail = invoice.ClientEmail,
            ClientPhone = invoice.ClientPhone,
            Region = invoice.Region,
            Status = invoice.Status,
            CreatedAt = invoice.CreatedAt,
            DueDate = invoice.DueDate,
            Subtotal = invoice.Subtotal,
            Tax = invoice.Tax,
            Discount = invoice.Discount,
            GrandTotal = invoice.GrandTotal,
            TotalPaid = invoice.TotalPaid,
            TotalDue = invoice.TotalDue,
            Items = invoice.Items.Select(i => new InvoiceItemResponse
            {
                Name = i.Name,
                UnitPrice = i.UnitPrice,
                Quantity = i.Quantity
            }).ToList(),
            Payments = invoice.Payments.Select(p => new PaymentResponse
            {
                Id = p.Id,
                InvoiceId = p.InvoiceId,
                Amount = p.Amount,
                PaidAt = p.PaidAt,
                Method = p.Method
            }).ToList()
        };
    }
}

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly DataRepository _repository;
    private readonly InvoiceStatusTracker _statusTracker;
    private readonly PdfReceiptGenerator _pdfGenerator;
    
    public PaymentsController(DataRepository repository, InvoiceStatusTracker statusTracker, PdfReceiptGenerator pdfGenerator)
    {
        _repository = repository;
        _statusTracker = statusTracker;
        _pdfGenerator = pdfGenerator;
    }
    
    [HttpGet("invoice/{invoiceId}")]
    public ActionResult<IEnumerable<PaymentResponse>> GetByInvoice(int invoiceId)
    {
        var payments = _repository.Payments.Where(p => p.InvoiceId == invoiceId);
        return Ok(payments.Select(p => new PaymentResponse
        {
            Id = p.Id,
            InvoiceId = p.InvoiceId,
            Amount = p.Amount,
            PaidAt = p.PaidAt,
            Method = p.Method
        }));
    }
    
    [HttpPost("invoice/{invoiceId}")]
    public ActionResult<PaymentResponse> Create(int invoiceId, [FromBody] CreatePaymentRequest request)
    {
        var invoice = _repository.Invoices.FirstOrDefault(i => i.Id == invoiceId);
        if (invoice == null) return NotFound("Invoice not found");
        
        var payment = new Payment
        {
            Id = _repository.GetNextPaymentId(),
            InvoiceId = invoiceId,
            Amount = request.Amount,
            PaidAt = DateTime.Now,
            Method = request.Method
        };
        
        _repository.Payments.Add(payment);
        invoice.Payments.Add(payment);
        
        _statusTracker.TrackStatus(invoice);
        
        return CreatedAtAction(nameof(GetByInvoice), new { invoiceId = invoiceId }, new PaymentResponse
        {
            Id = payment.Id,
            InvoiceId = payment.InvoiceId,
            Amount = payment.Amount,
            PaidAt = payment.PaidAt,
            Method = payment.Method
        });
    }
    
    [HttpGet("{paymentId}/receipt")]
    public IActionResult GenerateReceipt(int paymentId)
    {
        var payment = _repository.Payments.FirstOrDefault(p => p.Id == paymentId);
        if (payment == null) return NotFound("Payment not found");
        
        var invoice = _repository.Invoices.FirstOrDefault(i => i.Id == payment.InvoiceId);
        if (invoice == null) return NotFound("Invoice not found");
        
        var pdfBytes = _pdfGenerator.GenerateReceipt(invoice, payment);
        return File(pdfBytes, "application/pdf", $"Receipt_{invoice.Id}_{payment.PaidAt:yyyyMMdd}.pdf");
    }
}

// Startup
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers();
        
        // Register services
        services.AddSingleton<DataRepository>();
        services.AddSingleton<InvoiceStatusTracker>();
        services.AddSingleton<InvoiceSummaryReport>();
        services.AddSingleton<PdfReceiptGenerator>();
    }
    
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}

public class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }
    
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            });
}