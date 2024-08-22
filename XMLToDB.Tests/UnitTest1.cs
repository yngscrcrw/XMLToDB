using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

public class OrderRepositoryTests
{
    private OrdersContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<OrdersContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OrdersContext(options);
    }

    [Fact]
    public void AddUser_Should_Add_User_To_Database()
    {
        using (var context = CreateInMemoryContext())
        {
            var repository = new OrderRepository(context);
            var user = new User { Name = "John Doe", Email = "john.doe@example.com" };

            repository.AddUser(user);
            repository.SaveChanges();

            var retrievedUser = context.Users.FirstOrDefault(u => u.Email == "john.doe@example.com");
            Assert.NotNull(retrievedUser);
            Assert.Equal("John Doe", retrievedUser.Name);
        }
    }

    [Fact]
    public void GetUserByEmail_Should_Return_Correct_User()
    {
        using (var context = CreateInMemoryContext())
        {
            var repository = new OrderRepository(context);
            var user = new User 
            { 
                Name = "Jane Doe",
                Email = "jane.doe@example.com",
                Password = "Jane123456"
            };

            context.Users.Add(user);
            context.SaveChanges();

            var retrievedUser = repository.GetUserByEmail("jane.doe@example.com");
            Assert.NotNull(retrievedUser);
            Assert.Equal("Jane Doe", retrievedUser.Name);
        }
    }

    [Fact]
    public void AddProduct_Should_Add_Product_To_Database()
    {
        using (var context = CreateInMemoryContext())
        {
            var repository = new OrderRepository(context);
            var product = new Product { ProductName = "Test Product", Price = 9.99m };

            repository.AddProduct(product);
            repository.SaveChanges();

            var retrievedProduct = context.Products.FirstOrDefault(p => p.ProductName == "Test Product");
            Assert.NotNull(retrievedProduct);
            Assert.Equal(9.99m, retrievedProduct.Price);
        }
    }
}

public class XmlOrderParserTests
{
    [Fact]
    public void ParseOrders_Should_Return_Empty_List_For_Invalid_File()
    {
        var parser = new XmlOrderParser();
        var orders = parser.ParseOrders("non_existing_file.xml");

        Assert.Empty(orders);
    }

    [Fact]
    public void ParseOrders_Should_Return_Correct_Number_Of_Orders()
    {
        var parser = new XmlOrderParser();
        var xmlContent = @"
            <orders>
                <order>
                    <no>1</no>
                    <reg_date>2024-08-22</reg_date>
                    <user>
                        <fio>John Doe</fio>
                        <email>john.doe@example.com</email>
                        <password>password123</password>
                    </user>
                    <product>
                        <name>Product1</name>
                        <price>10.00</price>
                        <quantity>1</quantity>
                    </product>
                </order>
            </orders>";

        System.IO.File.WriteAllText("test_orders.xml", xmlContent);

        var orders = parser.ParseOrders("test_orders.xml");

        Assert.Single(orders);
        Assert.Equal("John Doe", orders[0].User.Name);
        Assert.Equal("Product1", orders[0].OrderItems.First().Product.ProductName);

        System.IO.File.Delete("test_orders.xml");
    }
}

public class OrderSaverTests
{
    private OrdersContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<OrdersContext>()
        .UseSqlite("DataSource=:memory:")
        .Options;
        return new OrdersContext(options);
    }

    [Fact]
    public void SaveOrders_Should_Save_Orders_To_Database()
    {
        var options = new DbContextOptionsBuilder<OrdersContext>()
            .UseInMemoryDatabase(databaseName: "Test_SaveOrders")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using (var context = new OrdersContext(options))
        {
            var repository = new OrderRepository(context);
            var orderSaver = new OrderSaver(repository);

            var orders = new List<Order>
        {
            new Order
            {
                User = new User { Name = "John Doe", Email = "john@example.com", Password = "password" },
                OrderItems = new List<OrderItem>
                {
                    new OrderItem
                    {
                        Product = new Product { ProductName = "Product 1", Price = 10.0M, Description = "Test product" },
                        Quantity = 2
                    }
                },
                Date = DateTime.Now
            }
        };

            orderSaver.SaveOrders(orders);

            Assert.Equal(1, context.Orders.Count());
            Assert.Equal(1, context.Users.Count());
            Assert.Equal(1, context.Products.Count());
        }
    }
}
