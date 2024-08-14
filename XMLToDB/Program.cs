using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
public class User
{
    public int UserID { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
public class Product
{
    public int ProductID { get; set; }
    public string ProductName { get; set; }
    public decimal Price { get; set; }
    public string Description { get; set; }
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
public class Order
{
    public int OrderID { get; set; }
    public int UserID { get; set; }
    public User User { get; set; }
    public DateTime Date { get; set; }
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
public class OrderItem
{
    public int OrderItemID { get; set; }
    public int OrderID { get; set; }
    public Order Order { get; set; }
    public int ProductID { get; set; }
    public Product Product { get; set; }
    public int Quantity { get; set; }
}
public class OrdersContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer("Your_SqlServer");
        //    .LogTo(Console.WriteLine, LogLevel.Information);
    }
}
public class XmlParser
{
    public static List<Order> ParseOrders(string xmlFilePath, OrdersContext context)
    {
        XDocument xdoc = XDocument.Load(xmlFilePath);
        var orders = new List<Order>();
        using (var transaction = context.Database.BeginTransaction())
        {
            try
            {
                context.Database.ExecuteSqlRaw("SET IDENTITY_INSERT Orders ON");
                orders = xdoc.Descendants("order").Select(o =>
                {
                    var userElement = o.Element("user");
                    var userEmail = (string)userElement.Element("email");
                    var user = context.Users.FirstOrDefault(u => u.Email == userEmail);
                    if (user == null)
                    {
                        user = new User
                        {
                            Name = (string)userElement.Element("fio"),
                            Email = userEmail,
                            Password = (string)userElement.Element("password") ?? "default_password"
                        };
                        context.Users.Add(user);
                        context.SaveChanges();
                    }
                    var products = new List<OrderItem>();
                    foreach (var productElement in o.Elements("product"))
                    {
                        var productName = (string)productElement.Element("name");
                        var existingProduct = context.Products.FirstOrDefault(p => p.ProductName == productName);
                        if (existingProduct == null)
                        {
                            existingProduct = new Product
                            {
                                ProductName = productName,
                                Price = (decimal)productElement.Element("price"),
                                Description = (string)productElement.Element("description") ?? "DefaultProductDescription"
                            };
                            context.Products.Add(existingProduct);
                            context.SaveChanges();
                        }
                        products.Add(new OrderItem
                        {
                            ProductID = existingProduct.ProductID,
                            Quantity = (int)productElement.Element("quantity")
                        });
                    }
                    var order = new Order
                    {
                        OrderID = (int)o.Element("no"), // Use the OrderID from XML
                        Date = DateTime.Parse((string)o.Element("reg_date")),
                        UserID = user.UserID,
                        OrderItems = products
                    };
                    context.Orders.Add(order);
                    context.SaveChanges();
                    return order;
                }).ToList();
                context.Database.ExecuteSqlRaw("SET IDENTITY_INSERT Orders OFF");
                transaction.Commit();
            }
            catch (Exception ex)
            {
                context.Database.ExecuteSqlRaw("SET IDENTITY_INSERT Orders OFF");
                transaction.Rollback();
                Console.WriteLine($"Произошла ошибка: {ex.Message}");
            }
        }
        return orders;
    }
}
class Program
{
    static void Main(string[] args)
    {
        string xmlFilePath = "order.xml";
        try
        {
            using (var context = new OrdersContext())
            {
                var orders = XmlParser.ParseOrders(xmlFilePath, context);
                Console.WriteLine("Заказы успешно добавлены в базу данных.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Произошла ошибка: {ex.Message}");
        }
    }
}
