using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public interface IOrderRepository
{
    User GetUserByEmail(string email);
    void AddUser(User user);
    Product GetProductByName(string name);
    void AddProduct(Product product);
    void AddOrder(Order order);
    void SaveChanges();
    OrdersContext Context { get; }
}

public interface IXmlOrderParser
{
    List<Order> ParseOrders(string xmlFilePath);
}

public interface IOrderSaver
{
    void SaveOrders(List<Order> orders);
}

public class User
{
    public int UserID { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string Password { get; set; } = "default_password";
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}

public class Product
{
    public int ProductID { get; set; }
    public string ProductName { get; set; }
    public decimal Price { get; set; }
    public string Description { get; set; } = "default_description";
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

public class OrderRepository : IOrderRepository
{
    private readonly OrdersContext _context;
    public OrdersContext Context => _context;

    public OrderRepository(OrdersContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public User GetUserByEmail(string email)
    {
        return _context.Users.FirstOrDefault(u => u.Email == email);
    }

    public void AddUser(User user)
    {
        _context.Users.Add(user);
    }

    public Product GetProductByName(string name)
    {
        return _context.Products.FirstOrDefault(p => p.ProductName == name);
    }

    public void AddProduct(Product product)
    {
        _context.Products.Add(product);
    }

    public void AddOrder(Order order)
    {
        _context.Orders.Add(order);
    }

    public void SaveChanges()
    {
        _context.SaveChanges();
    }
}

public class OrdersContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }

    public OrdersContext(DbContextOptions<OrdersContext> options) : base(options) { }

    public OrdersContext() : base(new DbContextOptionsBuilder<OrdersContext>()
    .UseSqlServer("sql_server")
    .Options)
    { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>()
            .Property(p => p.Price)
            .HasColumnType("decimal(18, 2)");
    }
}

public class XmlOrderParser : IXmlOrderParser
{
    public List<Order> ParseOrders(string xmlFilePath)
    {
        var orders = new List<Order>();

        if (!System.IO.File.Exists(xmlFilePath))
        {
            Console.WriteLine($"Файл {xmlFilePath} не найден.");
            return orders;
        }

        XDocument xdoc;
        try
        {
            xdoc = XDocument.Load(xmlFilePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при загрузке XML: {ex.Message}");
            return orders;
        }

        if (xdoc.Root == null || !xdoc.Root.Elements("order").Any())
        {
            Console.WriteLine("XML не содержит элементов заказов.");
            return orders;
        }

        foreach (var orderElement in xdoc.Descendants("order"))
        {
            var order = CreateOrderFromXml(orderElement);
            if (order != null)
            {
                orders.Add(order);
            }
        }

        return orders;
    }

    private Order CreateOrderFromXml(XElement orderElement)
    {
        if (orderElement == null) return null;

        var userElement = orderElement.Element("user");
        var orderItemsElements = orderElement.Elements("product");

        if (userElement == null || !orderItemsElements.Any()) return null;

        var user = new User
        {
            Name = (string)userElement.Element("fio"),
            Email = (string)userElement.Element("email"),
            Password = (string)userElement.Element("password") ?? "default_password"
        };

        var orderItems = orderItemsElements.Select(pe => new OrderItem
        {
            Product = new Product
            {
                ProductName = (string)pe.Element("name"),
                Price = (decimal)pe.Element("price"),
                Description = (string)pe.Element("description") ?? "DefaultProductDescription"
            },
            Quantity = (int)pe.Element("quantity")
        }).ToList();

        return new Order
        {
            OrderID = (int)orderElement.Element("no"),
            Date = DateTime.Parse((string)orderElement.Element("reg_date")),
            User = user,
            OrderItems = orderItems
        };
    }
}

public class OrderSaver : IOrderSaver
{
    private readonly IOrderRepository _orderRepository;

    public OrderSaver(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public void SaveOrders(List<Order> orders)
    {
        using (var transaction = _orderRepository.Context.Database.BeginTransaction())
        {
            try
            {
                if (_orderRepository.Context.Database.IsRelational())
                {
                    _orderRepository.Context.Database.ExecuteSqlRaw("SET IDENTITY_INSERT Orders ON");
                }

                foreach (var order in orders)
                {
                    var user = _orderRepository.GetUserByEmail(order.User.Email);

                    if (user == null)
                    {
                        _orderRepository.AddUser(order.User);
                        _orderRepository.SaveChanges();
                        user = order.User;
                    }
                    else
                    {
                        order.UserID = user.UserID;
                    }

                    foreach (var orderItem in order.OrderItems)
                    {
                        var product = _orderRepository.GetProductByName(orderItem.Product.ProductName);

                        if (product == null)
                        {
                            _orderRepository.AddProduct(orderItem.Product);
                            _orderRepository.SaveChanges();
                            product = orderItem.Product;
                        }
                        orderItem.ProductID = product.ProductID;
                    }
                    _orderRepository.AddOrder(order);
                }
                _orderRepository.SaveChanges();
                if (_orderRepository.Context.Database.IsRelational())
                {
                    _orderRepository.Context.Database.ExecuteSqlRaw("SET IDENTITY_INSERT Orders OFF");
                }
                transaction.Commit();
            }
            catch (Exception ex)
            {
                if (_orderRepository.Context.Database.IsRelational())
                {
                    _orderRepository.Context.Database.ExecuteSqlRaw("SET IDENTITY_INSERT Orders OFF");
                }
                transaction.Rollback();
                Console.WriteLine($"Произошла ошибка при сохранении данных: {ex.Message}");
            }
        }
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
                var orderRepository = new OrderRepository(context);
                var parser = new XmlOrderParser();
                var saver = new OrderSaver(orderRepository);
                var orders = parser.ParseOrders(xmlFilePath);

                saver.SaveOrders(orders);

                Console.WriteLine("Заказы успешно добавлены в базу данных.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Произошла ошибка: {ex.Message}");
        }
    }
}