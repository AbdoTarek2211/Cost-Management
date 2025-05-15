using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

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
	public int ClientId { get; set; } // Added ClientID
	public string ClientName { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime DueDate { get; set; }
	public string Status { get; set; }
	public string Region { get; set; } // Added for tax calculation
	public decimal Discount { get; set; } // Added for discounts
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
	public decimal GrandTotal => DiscountKind == DiscountType.Percentage ? (Subtotal + Tax) * (1 - Discount / 100) : Subtotal + Tax - Discount;
	public decimal TotalPaid => Payments.Sum(p => p.Amount);
	public decimal TotalDue => GrandTotal - TotalPaid;

	private decimal CalculateTax(decimal amount, string countryCode)
	{
		return countryCode?.ToUpper() switch
		{
			// Gulf Cooperation Council (GCC) countries
			"SA" => amount * 0.15m, // Saudi Arabia - 15% VAT
			"AE" => amount * 0.05m, // UAE - 5% VAT
			// North Africa
			"EG" => amount * 0.14m, // Egypt - 14% VAT
			"MA" => amount * 0.20m, // Morocco - 20% VAT
			"TN" => amount * 0.19m, // Tunisia - 19% VAT
			"LY" => amount * 0.15m, // Libya - 15% VAT
			// Levant
			"JO" => amount * 0.16m, // Jordan - 16% VAT
			"IQ" => amount * 0.15m, // Iraq - 15% VAT
			"PS" => amount * 0.16m, // Palestine - 16% VAT
			_ => amount * 0.05m
		};
	}
}

public class InvoiceStatusTracker
{
	public string TrackStatus(Invoice invoice)
	{
		// Calculate days remaining until due date
		var daysRemaining = (invoice.DueDate - DateTime.Now).Days;
		// Determine status based on payment and due date
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
		// Keep as Draft if no payments and explicitly set to Draft
		}
		else if (invoice.Payments.Any())
		{
			// If payments exist but status wasn't set, mark as Partial
			invoice.Status = "Partial";
		}
		else
		{
			invoice.Status = "Sent";
		}

		return invoice.Status;
	}

	public string GetStatusSummary(Invoice invoice)
	{
		var status = TrackStatus(invoice);
		var summary = new StringBuilder();
		summary.AppendLine($"Invoice #{invoice.Id} Status: {status}");
		summary.AppendLine($"Amount Due: {invoice.TotalDue:C}");
		if (status == "Overdue")
		{
			var daysOverdue = (DateTime.Now - invoice.DueDate).Days;
			summary.AppendLine($"This invoice is {daysOverdue} days overdue.");
		}
		else if (status == "Partial")
		{
			summary.AppendLine($"Partially paid: {invoice.TotalPaid:C} of {invoice.GrandTotal:C}");
		}

		return summary.ToString();
	}
}

public class PaymentHistoryLogger
{
	private readonly List<Payment> _paymentHistory;
	public PaymentHistoryLogger(List<Payment> paymentHistory)
	{
		_paymentHistory = paymentHistory;
	}

	public void LogPayment(Payment payment)
	{
		// Validate payment
		if (payment == null)
			throw new ArgumentNullException(nameof(payment));
		if (payment.Amount <= 0)
			throw new ArgumentException("Payment amount must be positive");
		if (string.IsNullOrWhiteSpace(payment.Method))
			throw new ArgumentException("Payment method is required");
		_paymentHistory.Add(payment);
	}

	public IEnumerable<Payment> GetPaymentHistory(int invoiceId)
	{
		return _paymentHistory.Where(p => p.InvoiceId == invoiceId).OrderBy(p => p.PaidAt);
	}

	public string GetPaymentHistoryReport(int invoiceId)
	{
		var payments = GetPaymentHistory(invoiceId).ToList();
		if (!payments.Any())
			return "No payments found for this invoice.";
		var report = new StringBuilder();
		report.AppendLine($"Payment History for Invoice #{invoiceId}");
		report.AppendLine("----------------------------------------");
		foreach (var payment in payments)
		{
			report.AppendLine($"[{payment.PaidAt}] ${payment.Amount:F2} via {payment.Method}");
		}

		report.AppendLine("----------------------------------------");
		report.AppendLine($"Total Paid: ${payments.Sum(p => p.Amount):F2}");
		return report.ToString();
	}

	public decimal GetTotalPaid(int invoiceId)
	{
		return _paymentHistory.Where(p => p.InvoiceId == invoiceId).Sum(p => p.Amount);
	}
}

public static class Validator
{
    public static bool ValidateInvoice(Invoice invoice)
    {
        if (invoice.Items == null || !invoice.Items.Any())
        {
            Console.WriteLine("Invoice must have at least one item");
            return false;
        }
        
        if (invoice.DiscountKind == Invoice.DiscountType.Percentage && 
           (invoice.Discount < 0 || invoice.Discount > 100))
        {
            Console.WriteLine("Percentage discount must be between 0-100");
            return false;
        }
        
        return true;
    }
}

public class InvoiceSummaryReport
{
	private readonly List<Invoice> _invoices;
	public InvoiceSummaryReport(List<Invoice> invoices)
	{
		_invoices = invoices;
	}

	public Report GenerateStatusReport(string statusFilter = null)
	{
		var filteredInvoices = _invoices.AsEnumerable();
		if (!string.IsNullOrEmpty(statusFilter))
		{
			filteredInvoices = filteredInvoices.Where(i => i.Status.Equals(statusFilter, StringComparison.OrdinalIgnoreCase));
		}

		return CreateReport(title: $"Invoice Status Report {(statusFilter != null ? $"({statusFilter})" : "")}", columns: new[] { "ID", "Client", "Created", "Due Date", "Amount Due", "Status" }, rows: filteredInvoices.OrderBy(i => i.Status).ThenBy(i => i.DueDate).Select(i => new object[] { i.Id, i.ClientName, i.CreatedAt.ToShortDateString(), i.DueDate.ToShortDateString(), i.TotalDue, i.Status }));
	}

	public Report GenerateClientReport(string clientFilter = null)
	{
		var filteredInvoices = _invoices.AsEnumerable();
		if (!string.IsNullOrEmpty(clientFilter))
		{
			filteredInvoices = filteredInvoices.Where(i => i.ClientName.Contains(clientFilter, StringComparison.OrdinalIgnoreCase));
		}

		return CreateReport(title: $"Client Invoice Report {(clientFilter != null ? $"({clientFilter})" : "")}", columns: new[] { "ID", "Client", "Created", "Total", "Paid", "Due", "Status" }, rows: filteredInvoices.OrderBy(i => i.ClientName).ThenBy(i => i.DueDate).Select(i => new object[] { i.Id, i.ClientName, i.CreatedAt.ToShortDateString(), i.GrandTotal, i.TotalPaid, i.TotalDue, i.Status }));
	}

	public Report GenerateDateRangeReport(DateTime? startDate, DateTime? endDate)
	{
		var filteredInvoices = _invoices.AsEnumerable();
		if (startDate.HasValue)
		{
			filteredInvoices = filteredInvoices.Where(i => i.CreatedAt >= startDate.Value);
		}

		if (endDate.HasValue)
		{
			filteredInvoices = filteredInvoices.Where(i => i.CreatedAt <= endDate.Value);
		}

		var dateRangeTitle = $"{(startDate.HasValue ? startDate.Value.ToShortDateString() : "Start")} to {(endDate.HasValue ? endDate.Value.ToShortDateString() : "End")}";
		return CreateReport(title: $"Date Range Invoice Report ({dateRangeTitle})", columns: new[] { "ID", "Client", "Created", "Due Date", "Total", "Status" }, rows: filteredInvoices.OrderBy(i => i.CreatedAt).Select(i => new object[] { i.Id, i.ClientName, i.CreatedAt.ToShortDateString(), i.DueDate.ToShortDateString(), i.GrandTotal, i.Status }));
	}

	public Report GenerateSummaryStatistics()
	{
		var statusGroups = _invoices.GroupBy(i => i.Status).Select(g => new { Status = g.Key, Count = g.Count(), TotalAmount = g.Sum(i => i.GrandTotal), TotalDue = g.Sum(i => i.TotalDue) }).OrderBy(g => g.Status);
		var clientGroups = _invoices.GroupBy(i => i.ClientName).Select(g => new { Client = g.Key, Count = g.Count(), TotalAmount = g.Sum(i => i.GrandTotal), TotalDue = g.Sum(i => i.TotalDue) }).OrderBy(g => g.Client);
		var sb = new StringBuilder();
		sb.AppendLine("=== Invoice Statistics ===");
		sb.AppendLine("\nBy Status:");
		foreach (var group in statusGroups)
		{
			sb.AppendLine($"{group.Status}: {group.Count} invoices, Total: {group.TotalAmount:C}, Due: {group.TotalDue:C}");
		}

		sb.AppendLine("\nBy Client:");
		foreach (var group in clientGroups)
		{
			sb.AppendLine($"{group.Client}: {group.Count} invoices, Total: {group.TotalAmount:C}, Due: {group.TotalDue:C}");
		}

		return new Report
		{
			Title = "Invoice Summary Statistics",
			Content = sb.ToString(),
			ReportType = ReportType.TextSummary
		};
	}

	private Report CreateReport(string title, string[] columns, IEnumerable<object[]> rows)
	{
		var sb = new StringBuilder();
		// Header
		sb.AppendLine(title);
		sb.AppendLine(new string ('=', title.Length));
		sb.AppendLine(string.Join(" | ", columns));
		sb.AppendLine(new string ('-', columns.Sum(c => c.Length) + (columns.Length - 1) * 3));
		// Rows
		foreach (var row in rows)
		{
			sb.AppendLine(string.Join(" | ", row.Select((v, i) => v is decimal ? ((decimal)v).ToString("C") : v is DateTime ? ((DateTime)v).ToShortDateString() : v.ToString())));
		}

		// Summary
		sb.AppendLine(new string ('=', title.Length));
		sb.AppendLine($"Total Invoices: {rows.Count()}");
		return new Report
		{
			Title = title,
			Content = sb.ToString(),
			ReportType = ReportType.Tabular
		};
	}
}

public class Report
{
	public string Title { get; set; }
	public string Content { get; set; }
	public ReportType ReportType { get; set; }

	public void Display()
	{
		Console.WriteLine("\n=== REPORT ===");
		Console.WriteLine(Title);
		Console.WriteLine(Content);
	}

	public void SaveToFile(string filePath)
	{
		File.WriteAllText(filePath, $"{Title}\n\n{Content}");
		Console.WriteLine($"Report saved to {filePath}");
	}
}

public enum ReportType
{
	Tabular,
	TextSummary,
	Graphical
}

public class PdfReceiptGenerator
{
    private IContainer CellStyle(IContainer container) => container
        .BorderBottom(1)
        .BorderColor(Colors.Grey.Lighten2)
        .PaddingVertical(5);
	public void GenerateAndSaveReceipt(Invoice invoice, Payment payment, string filePath)
	{
		QuestPDF.Settings.License = LicenseType.Community;
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
					// Invoice Items
					column.Item().PaddingTop(10).Text("ITEMS").SemiBold();
					column.Item().Table(table =>
					{
						table.ColumnsDefinition(columns =>
						{
							columns.ConstantColumn(10, Unit.Millimetre); // #
							columns.RelativeColumn(3); // Description
							columns.ConstantColumn(2, Unit.Centimetre); // Qty
							columns.ConstantColumn(3, Unit.Centimetre); // Unit Price
							columns.ConstantColumn(3, Unit.Centimetre); // Amount
						});
						// Header
						table.Header(header =>
						{
							header.Cell().Text("#");
							header.Cell().Text("Description");
							header.Cell().AlignRight().Text("Qty");
							header.Cell().AlignRight().Text("Unit Price");
							header.Cell().AlignRight().Text("Amount");
							header.Cell().ColumnSpan(5).PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
						});
						// Items
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
					// Summary
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
					// Footer
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
		}).GeneratePdf(filePath);
	}
}

class Program
{
	static List<Cost> Costs = new();
	static List<Invoice> Invoices = new();
	static List<Payment> Payments = new();
	static int invoiceIdCounter = 1;
	static int paymentIdCounter = 1;
	static InvoiceStatusTracker StatusTracker = new InvoiceStatusTracker();
	static PaymentHistoryLogger PaymentLogger = new PaymentHistoryLogger(Payments);
	static InvoiceSummaryReport ReportGenerator = new InvoiceSummaryReport(Invoices);
	static void Main(string[] args)
	{
		LogSampleData();
		while (true)
		{
			Console.WriteLine("\n--- Local Cost Manager Menu ---");
			Console.WriteLine("1. Log a new cost entry");
			Console.WriteLine("2. Create a new invoice");
			Console.WriteLine("3. Edit an invoice");
			Console.WriteLine("4. Log a payment");
			Console.WriteLine("5. Show all invoices");
			Console.WriteLine("6. Show invoice by ID");
			Console.WriteLine("7. Generate PDF receipt");
			Console.WriteLine("8. Show due reminders");
			Console.WriteLine("9. Generate reports");
			Console.WriteLine("10. Exit");
			Console.Write("Select an option: ");
			var choice = Console.ReadLine();
			switch (choice)
			{
				case "1":
					LogNewCost();
					break;
				case "2":
					CreateInvoice();
					break;
				case "3":
					Console.Write("Invoice ID to edit: ");
					EditInvoice(int.Parse(Console.ReadLine()));
					break;
				case "4":
					Console.Write("Invoice ID for payment: ");
					LogPayment(int.Parse(Console.ReadLine()));
					break;
				case "5":
					foreach (var inv in Invoices)
						ShowInvoiceSummary(inv);
					break;
				case "6":
					Console.Write("Enter Invoice ID: ");
					ShowInvoiceSummaryById(int.Parse(Console.ReadLine()));
					break;
				case "7":
					Console.Write("Enter Invoice ID to generate PDF receipt: ");
					GeneratePdfReceipt(int.Parse(Console.ReadLine()));
					break;
				case "8":
					SendDueReminders();
					break;
				case "9":
					ShowReportMenu();
					break;
				case "10":
					return;
				default:
					Console.WriteLine("Invalid option.");
					break;
			}
		}
	}

	static DateTime? ParseDate(string input)
	{
		if (string.IsNullOrWhiteSpace(input))
			return null;
		return DateTime.Parse(input);
	}

	static void ShowReportMenu()
	{
		while (true)
		{
			Console.WriteLine("\n--- Report Generation Menu ---");
			Console.WriteLine("1. Status Report");
			Console.WriteLine("2. Client Report");
			Console.WriteLine("3. Date Range Report");
			Console.WriteLine("4. Summary Statistics");
			Console.WriteLine("5. Back to Main Menu");
			Console.Write("Select report type: ");
			var choice = Console.ReadLine();
			Report report = null;
			switch (choice)
			{
				case "1":
					Console.Write("Enter status to filter (or leave empty for all): ");
					var statusFilter = Console.ReadLine();
					report = ReportGenerator.GenerateStatusReport(statusFilter);
					break;
				case "2":
					Console.Write("Enter client name to filter (or leave empty for all): ");
					var clientFilter = Console.ReadLine();
					report = ReportGenerator.GenerateClientReport(clientFilter);
					break;
				case "3":
					Console.Write("Enter start date (yyyy-mm-dd or empty): ");
					var startDate = ParseDate(Console.ReadLine());
					Console.Write("Enter end date (yyyy-mm-dd or empty): ");
					var endDate = ParseDate(Console.ReadLine());
					report = ReportGenerator.GenerateDateRangeReport(startDate, endDate);
					break;
				case "4":
					report = ReportGenerator.GenerateSummaryStatistics();
					break;
				case "5":
					return;
				default:
					Console.WriteLine("Invalid option.");
					continue;
			}

			if (report != null)
			{
				report.Display();
				Console.Write("Save to file? (y/n): ");
				if (Console.ReadLine().ToLower() == "y")
				{
					Console.Write("Enter file path: ");
					var path = Console.ReadLine();
					report.SaveToFile(path);
				}
			}
		}
	}

	static void LogSampleData()
	{
		// Sample costs
		Costs.Add(new Cost { Id = 1, Description = "Office supplies", Amount = 125.50m, Date = DateTime.Now.AddDays(-10), Category = "Office" });
		// Sample invoice
		var invoice = new Invoice
		{
			Id = invoiceIdCounter++,
			ClientId = 1001,
			ClientName = "Sample Client",
			Region = "PS",
			Discount = 10.00m,
			DiscountKind = Invoice.DiscountType.Percentage,
			CreatedAt = DateTime.Now,
			DueDate = DateTime.Now.AddDays(30),
			Status = "Sent",
			Items = new List<InvoiceItem>
			{
				new InvoiceItem
				{
					Name = "Website Design",
					UnitPrice = 500,
					Quantity = 1
				},
				new InvoiceItem
				{
					Name = "Hosting (1 year)",
					UnitPrice = 120,
					Quantity = 1
				}
			}
		};
		Invoices.Add(invoice);
		// Sample payment
		var payment = new Payment 
        { 
            Id = paymentIdCounter++, 
            InvoiceId = invoice.Id, 
            Amount = 300, 
            PaidAt = DateTime.Now.AddDays(-5), 
            Method = "Bank Transfer" 
        };
        Payments.Add(payment);
        invoice.Payments.Add(payment);
	}

	static void LogNewCost()
	{
		Console.WriteLine("\n--- Log New Cost ---");
		var cost = new Cost();
		Console.Write("Description: ");
		cost.Description = Console.ReadLine();
		Console.Write("Amount: ");
		cost.Amount = decimal.Parse(Console.ReadLine());
		cost.Date = DateTime.Now;
		Console.Write("Category: ");
		cost.Category = Console.ReadLine();
		cost.Id = Costs.Count + 1;
		Costs.Add(cost);
		Console.WriteLine("Cost logged successfully!");
	}

	static void CreateInvoice()
	{
		Console.WriteLine("\n--- Create New Invoice ---");
		var invoice = new Invoice();
		Console.Write("Client ID: ");
		invoice.ClientId = int.Parse(Console.ReadLine());
		Console.Write("Client Name: ");
		invoice.ClientName = Console.ReadLine();
		Console.Write("Region Code (e.g., CA, NY): ");
		invoice.Region = Console.ReadLine();
		Console.Write("Discount Type (1-Fixed, 2-Percentage): ");
		invoice.DiscountKind = Console.ReadLine() == "2" ? Invoice.DiscountType.Percentage : Invoice.DiscountType.Fixed;
		Console.Write($"Discount Amount ({(invoice.DiscountKind == Invoice.DiscountType.Percentage ? "%" : "fixed")}): ");
		var discountInput = Console.ReadLine();
		if (decimal.TryParse(discountInput, out var discount))
		{
			if (invoice.DiscountKind == Invoice.DiscountType.Percentage && (discount < 0 || discount > 100))
			{
				Console.WriteLine("Percentage discount must be between 0-100");
				return;
			}

			invoice.Discount = discount;
		}

		Console.Write("Due in how many days? ");
		invoice.DueDate = DateTime.Now.AddDays(int.Parse(Console.ReadLine()));
		invoice.CreatedAt = DateTime.Now;
		invoice.Status = "Draft";
		while (true)
		{
			var item = new InvoiceItem();
			Console.Write("Item Name (or 'done' to finish): ");
			var name = Console.ReadLine();
			if (name.ToLower() == "done")
				break;
			item.Name = name;
			Console.Write("Unit Price: ");
			item.UnitPrice = decimal.Parse(Console.ReadLine());
			Console.Write("Quantity: ");
			item.Quantity = int.Parse(Console.ReadLine());
			invoice.Items.Add(item);
		}
		if (!Validator.ValidateInvoice(invoice))
		{
    		Console.WriteLine("Invoice validation failed. Not saving.");
    		return;
		}	
		invoice.Id = invoiceIdCounter++;
		Invoices.Add(invoice);
		Console.WriteLine($"Invoice #{invoice.Id} created successfully!");
		Console.WriteLine($"Grand Total: {invoice.GrandTotal:C} (Subtotal: {invoice.Subtotal:C}, Tax: {invoice.Tax:C}, Discount: {invoice.Discount:C})");
	}

	static void EditInvoice(int invoiceId)
	{
		var invoice = Invoices.FirstOrDefault(i => i.Id == invoiceId);
		if (invoice == null)
		{
			Console.WriteLine("Invoice not found.");
			return;
		}

		Console.WriteLine("\n--- Edit Invoice ---");
		Console.WriteLine($"Current Client: {invoice.ClientName}");
		Console.Write("New Client Name (or enter to keep): ");
		var newName = Console.ReadLine();
		if (!string.IsNullOrEmpty(newName))
			invoice.ClientName = newName;
		Console.WriteLine($"Current Due Date: {invoice.DueDate.ToShortDateString()}");
		Console.Write("Due in how many days? (or enter to keep): ");
		var dueDays = Console.ReadLine();
		if (!string.IsNullOrEmpty(dueDays))
			invoice.DueDate = DateTime.Now.AddDays(int.Parse(dueDays));
		Console.WriteLine("\nCurrent Items:");
		foreach (var item in invoice.Items)
		{
			Console.WriteLine($"{item.Quantity} x {item.Name} @ {item.UnitPrice:C}");
		}

		Console.WriteLine("\n1. Add item\n2. Remove item\n3. Continue");
		var choice = Console.ReadLine();
		if (choice == "1")
		{
			var item = new InvoiceItem();
			Console.Write("Item Name: ");
			item.Name = Console.ReadLine();
			Console.Write("Unit Price: ");
			item.UnitPrice = decimal.Parse(Console.ReadLine());
			Console.Write("Quantity: ");
			item.Quantity = int.Parse(Console.ReadLine());
			invoice.Items.Add(item);
		}
		else if (choice == "2")
		{
			Console.Write("Enter item name to remove: ");
			var itemName = Console.ReadLine();
			var itemToRemove = invoice.Items.FirstOrDefault(i => i.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase));
			if (itemToRemove != null)
				invoice.Items.Remove(itemToRemove);
		}
		if (!Validator.ValidateInvoice(invoice))
		{
    		Console.WriteLine("Invoice validation failed. Changes not saved.");
    		return;
		}
		Console.WriteLine("Invoice updated successfully!");
	}

	static void LogPayment(int invoiceId)
	{
		var invoice = Invoices.FirstOrDefault(i => i.Id == invoiceId);
		if (invoice == null)
		{
			Console.WriteLine("Invoice not found.");
			return;
		}

		Console.WriteLine("\n--- Log Payment ---");
		Console.WriteLine(StatusTracker.GetStatusSummary(invoice));
		// Get payment amount
		decimal amount;
		while (true)
		{
			Console.Write("Payment Amount: ");
			if (!decimal.TryParse(Console.ReadLine(), out amount) || amount <= 0)
			{
				Console.WriteLine("Invalid amount. Please enter a positive number.");
				continue;
			}

			break;
		}

		// Get payment method
		Console.Write("Payment Method: ");
		var method = Console.ReadLine();
		try
		{
			var payment = new Payment
			{
				Id = paymentIdCounter++,
				InvoiceId = invoiceId,
				Amount = amount,
				PaidAt = DateTime.Now,
				Method = method
			};
			PaymentLogger.LogPayment(payment);
			invoice.Payments.Add(payment);
			// Update status using tracker
			StatusTracker.TrackStatus(invoice);
			Console.WriteLine("\nPayment logged successfully!");
			Console.WriteLine(PaymentLogger.GetPaymentHistoryReport(invoiceId));
			Console.WriteLine(StatusTracker.GetStatusSummary(invoice));
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error logging payment: {ex.Message}");
		}
	}

	static void SendDueReminders()
	{
		Console.WriteLine("\n--- Due Reminders ---");
		var dueSoon = Invoices.Where(i => i.DueDate <= DateTime.Now.AddDays(7) && i.Status != "Paid").ToList();
		if (!dueSoon.Any())
		{
			Console.WriteLine("No invoices due soon.");
			return;
		}

		foreach (var invoice in dueSoon)
		{
			Console.WriteLine($"Invoice #{invoice.Id} for {invoice.ClientName} due on {invoice.DueDate.ToShortDateString()} - Amount Due: {invoice.TotalDue:C}");
			if (invoice.DueDate < DateTime.Now && invoice.Status != "Overdue")
			{
				invoice.Status = "Overdue";
				Console.WriteLine("Marked as overdue!");
			}
		}
	}

	static void ShowInvoiceSummary(Invoice invoice)
	{
		Console.WriteLine($"\n--- Invoice #{invoice.Id} ---");
		Console.WriteLine($"Client ID: {invoice.ClientId}");
		Console.WriteLine($"Client: {invoice.ClientName}");
		Console.WriteLine($"Region: {invoice.Region}");
		Console.WriteLine($"Status: {invoice.Status}");
		Console.WriteLine($"Created: {invoice.CreatedAt.ToShortDateString()}");
		Console.WriteLine($"Due: {invoice.DueDate.ToShortDateString()}");
		Console.WriteLine($"Subtotal: {invoice.Subtotal:C}");
		Console.WriteLine($"Tax ({invoice.Region}): {invoice.Tax:C}");
		Console.WriteLine($"Discount: {invoice.Discount:C}");
		Console.WriteLine($"Grand Total: {invoice.GrandTotal:C}");
		Console.WriteLine($"Total Paid: {invoice.TotalPaid:C}");
		Console.WriteLine($"Total Due: {invoice.TotalDue:C}");
		if (invoice.Payments.Any())
		{
			Console.WriteLine("\nPayment History:");
			foreach (var payment in invoice.Payments)
			{
				Console.WriteLine($"- {payment.PaidAt.ToShortDateString()}: {payment.Amount:C} via {payment.Method}");
			}
		}
	}

	static void ShowInvoiceSummaryById(int invoiceId)
	{
		var invoice = Invoices.FirstOrDefault(i => i.Id == invoiceId);
		if (invoice != null)
			ShowInvoiceSummary(invoice);
		else
			Console.WriteLine("Invoice not found.");
	}

	static void GeneratePdfReceipt(int invoiceId)
	{
		var invoice = Invoices.FirstOrDefault(i => i.Id == invoiceId);
		if (invoice == null)
		{
			Console.WriteLine("Invoice not found.");
			return;
		}

		if (!invoice.Payments.Any())
		{
			Console.WriteLine("No payments found for this invoice.");
			return;
		}
		var payment = invoice.Payments.OrderByDescending(p => p.PaidAt).First();
		var defaultFileName = $"Receipt_{invoice.Id}_{payment.PaidAt:yyyyMMdd}.pdf";
		var downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
		var defaultFilePath = Path.Combine(downloadsPath, defaultFileName);
		Console.Write($"Enter PDF file path (or press Enter for default: {defaultFilePath}): ");
		var filePath = Console.ReadLine();
		if (string.IsNullOrWhiteSpace(filePath))
		{
			filePath = defaultFilePath;
		}

		try
		{
			new PdfReceiptGenerator().GenerateAndSaveReceipt(invoice, payment, filePath);
			Console.WriteLine($"PDF receipt successfully saved to: {Path.GetFullPath(filePath)}");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error generating PDF: {ex.Message}");
		}
	}
}