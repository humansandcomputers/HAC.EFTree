using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HAC.EFTree.Tests
{
    /// <summary>
    /// Tests the modes for correct assigning right and left property of <see cref="ITreeEntity"/> type.
    /// </summary>
    /// <example>
    ///                                                   ┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
    ///                                           1 Electronics 26                                                      27 Clothing 28
    ///                ┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━╋━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
    ///         2 SmartPhones 11                    12 Laptops 17                     18 Computers 25
    ///         ┏━━━━━━┻━━━━━━┓                    ┏━━━━━━┻━━━━━━┓                           ┃
    ///    3 Android 4   5 iPhones 10       13 Windows 14   15 MacBooks 16             19 Desktops 24
    ///              ┏━━━━━━━━┻━━━━━━━━┓                                              ┏━━━━━━┻━━━━━━┓
    ///       6 iPhone SE 7     8 iPhone Pro 9                                    20 HP 21      22 Dell 23
    ///
    /// Electronics
    /// ├── SmartPhones
    /// │   ├── Android
    /// │   └── iPhones
    /// │       ├── iPhone SE
    /// │       └── iPhone Pro
    /// ├── Laptops
    /// |   ├── Windows
    /// |   └── MacBooks
    /// ├── Computers
    /// |   └── Desktops
    /// |       ├── HP
    /// |       └── Dell
    /// Clothing
    /// └── ...
    /// </example>
    [TestClass]
    public class DbContextExtensionsTest
    {
        class TestItem : ITreeEntity
        {
            public int Id { get; set; }
            public required string Name { get; set; }
            public long Left { get; set; }
            public long Right { get; set; }
        }
        class TestContext : DbContext
        {
            public DbSet<TestItem> Items { get; set; }
            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseSqlServer(Configuration["ConnectionString"]);
            }
        }

        static IConfigurationRoot Configuration { get; } = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
        TestContext Context { get; set; } = new TestContext();
        Dictionary<string, TestItem> ExampleItems { get; } = new[] { "Electronics", "SmartPhones", "Android", "iPhones", "iPhone SE", "iPhone Pro",
                "Laptops", "Windows", "MacBooks", "Computers", "Desktops", "HP", "Dell", "Clothing", }
                .ToDictionary(name => name, name => new TestItem { Name = name });

        public DbContextExtensionsTest()
        {
            Context.Database.EnsureDeleted();
            Context.Database.EnsureCreated();
        }

        TestItem GetExampleEntity(string name) => Context.Items.First(x => x.Name == name);

        void AssertExample()
        {
            AssertNode(null, "Electronics", "Clothing");
            AssertNode("Electronics", "SmartPhones", "Laptops", "Computers");
            AssertNode("SmartPhones", "Android", "iPhones");
            AssertNode("Laptops", "Windows", "MacBooks");
            AssertNode("Computers", "Desktops");
            AssertNode("iPhones", "iPhone SE", "iPhone Pro");
            AssertNode("Desktops", "HP", "Dell");
        }

        void AssertNode(string? parent, params string[] children)
        {
            if (parent is not null)
            {
                var parentNode = GetExampleEntity(parent);
                var firstChild = GetExampleEntity(children[0]);
                var lastChild = GetExampleEntity(children[^1]);
                Assert.AreEqual(parentNode.Left + 1, firstChild.Left, $"'{firstChild.Name}' is not started after parent node '{parentNode.Name}'");
                Assert.AreEqual(lastChild.Right + 1, parentNode.Right, $"'{lastChild.Name}' is not ended before parent node '{parentNode.Name}'");
            }
            for (int i = 1; i < children.Length; i++)
            {
                var last = GetExampleEntity(children[i - 1]);
                var current = GetExampleEntity(children[i]);
                Assert.AreEqual(last.Right + 1, current.Left, $"'{last.Name}' is not started before '{current.Name}'");
            }
        }

        [TestMethod]
        public void Add_ToRootEmptySet_SetLeftAndRight()
        {
            var item = new TestItem { Name = "Electronics" };
            Context.Items.Add(item, null);
            Assert.AreEqual(item.Left, 1);
            Assert.AreEqual(item.Right, 2);
            Context.SaveChanges();
        }

        [TestMethod]
        public void Add_ToRootWithRootNodes_SetLeftAndRight()
        {
            // Add 'Electronics' parent as if it has children.
            Context.Items.Add(new TestItem
            {
                Name = "Electronics",
                Left = 1,
                Right = 10
            });
            Context.SaveChanges();

            var item = new TestItem { Name = "Clothing" };
            Context.Items.Add(item, null);
            Assert.AreEqual(item.Left, 11);
            Assert.AreEqual(item.Right, 12);
            Context.SaveChanges();
        }

        [TestMethod]
        public void Add_DetachedParent_ThrowsInvalidOperationException()
        {
            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                var electronics = new TestItem { Name = "Electronics" };
                // Did not add root node
                var computers = new TestItem { Name = "Computers" };
                Context.Items.Add(computers, electronics);
                Context.SaveChanges();
            });
        }

        [TestMethod]
        public void Add_SaveChangesAtEnd_ValidTree()
        {
            Context.Items.Add(ExampleItems["Electronics"], null);
            Context.Items.Add(ExampleItems["Clothing"], null);
            Context.Items.Add(ExampleItems["SmartPhones"], ExampleItems["Electronics"]);
            Context.Items.Add(ExampleItems["Laptops"], ExampleItems["Electronics"]);
            Context.Items.Add(ExampleItems["Computers"], ExampleItems["Electronics"]);
            Context.Items.Add(ExampleItems["Android"], ExampleItems["SmartPhones"]);
            Context.Items.Add(ExampleItems["iPhones"], ExampleItems["SmartPhones"]);
            Context.Items.Add(ExampleItems["Windows"], ExampleItems["Laptops"]);
            Context.Items.Add(ExampleItems["MacBooks"], ExampleItems["Laptops"]);
            Context.Items.Add(ExampleItems["iPhone SE"], ExampleItems["iPhones"]);
            Context.Items.Add(ExampleItems["iPhone Pro"], ExampleItems["iPhones"]);
            Context.Items.Add(ExampleItems["Desktops"], ExampleItems["Computers"]);
            Context.Items.Add(ExampleItems["HP"], ExampleItems["Desktops"]);
            Context.Items.Add(ExampleItems["Dell"], ExampleItems["Desktops"]);
            Context.SaveChanges();
            AssertExample();
        }

        [TestMethod]
        public void Add_SaveChangesDuring_ValidTree()
        {
            Context.Items.Add(ExampleItems["Electronics"], null);
            Context.Items.Add(ExampleItems["Clothing"], null);
            Context.Items.Add(ExampleItems["SmartPhones"], ExampleItems["Electronics"]);
            Context.SaveChanges();
            Context.Items.Add(ExampleItems["Laptops"], ExampleItems["Electronics"]);
            Context.Items.Add(ExampleItems["Computers"], ExampleItems["Electronics"]);
            Context.Items.Add(ExampleItems["Android"], ExampleItems["SmartPhones"]);
            Context.SaveChanges();
            Context.Items.Add(ExampleItems["iPhones"], ExampleItems["SmartPhones"]);
            Context.Items.Add(ExampleItems["Windows"], ExampleItems["Laptops"]);
            Context.Items.Add(ExampleItems["MacBooks"], ExampleItems["Laptops"]);
            Context.Items.Add(ExampleItems["iPhone SE"], ExampleItems["iPhones"]);
            Context.Items.Add(ExampleItems["iPhone Pro"], ExampleItems["iPhones"]);
            Context.Items.Add(ExampleItems["Desktops"], ExampleItems["Computers"]);
            Context.Items.Add(ExampleItems["HP"], ExampleItems["Desktops"]);
            Context.Items.Add(ExampleItems["Dell"], ExampleItems["Desktops"]);
            Context.SaveChanges();
            AssertExample();
        }

        [TestMethod]
        public void Add_AlreadyExistingData_ValidTree()
        {
            // Add some ExampleItems
            using var connection = new SqlConnection(Configuration["ConnectionString"]);
            connection.Open();
            var query = """
                INSERT INTO [Items] ([Name], [Left], [Right]) VALUES
                ('Electronics', 1, 4),
                ('SmartPhones', 2, 3);
                """;
            var command = new SqlCommand(query, connection);
            command.ExecuteNonQuery();

            Context.Items.Add(ExampleItems["Clothing"], null);
            var electronics = Context.Items.First(x => x.Name == "Electronics");
            Context.Items.Add(ExampleItems["Laptops"], electronics);
            Context.Items.Add(ExampleItems["Computers"], electronics);
            var smartPhones = Context.Items.First(x => x.Name == "SmartPhones");
            Context.Items.Add(ExampleItems["Android"], smartPhones);
            Context.Items.Add(ExampleItems["iPhones"], smartPhones);
            Context.Items.Add(ExampleItems["Windows"], ExampleItems["Laptops"]);
            Context.Items.Add(ExampleItems["MacBooks"], ExampleItems["Laptops"]);
            Context.Items.Add(ExampleItems["iPhone SE"], ExampleItems["iPhones"]);
            Context.Items.Add(ExampleItems["iPhone Pro"], ExampleItems["iPhones"]);
            Context.Items.Add(ExampleItems["Desktops"], ExampleItems["Computers"]);
            Context.Items.Add(ExampleItems["HP"], ExampleItems["Desktops"]);
            Context.Items.Add(ExampleItems["Dell"], ExampleItems["Desktops"]);
            Context.SaveChanges();
            AssertExample();
        }

        [TestMethod]
        public void Insert_UseSiblings_ValidTree()
        {
            Context.Items.Add(ExampleItems["Electronics"], null);
            Context.Items.Add(ExampleItems["Clothing"], null);
            Context.Items.Add(ExampleItems["SmartPhones"], ExampleItems["Electronics"]);
            Context.Items.Add(ExampleItems["Computers"], ExampleItems["Electronics"]);
            Context.Items.Add(ExampleItems["iPhones"], ExampleItems["SmartPhones"]);
            Context.Items.Insert(ExampleItems["Android"], ExampleItems["iPhones"]);

            Context.Items.Add(ExampleItems["iPhone SE"], ExampleItems["iPhones"]);
            Context.Items.Add(ExampleItems["iPhone Pro"], ExampleItems["iPhones"]);
            Context.Items.Add(ExampleItems["Desktops"], ExampleItems["Computers"]);
            Context.Items.Add(ExampleItems["HP"], ExampleItems["Desktops"]);
            Context.Items.Add(ExampleItems["Dell"], ExampleItems["Desktops"]);

            Context.Items.Insert(ExampleItems["Laptops"], ExampleItems["Computers"]);
            Context.Items.Add(ExampleItems["Windows"], ExampleItems["Laptops"]);
            Context.Items.Add(ExampleItems["MacBooks"], ExampleItems["Laptops"]);
            Context.SaveChanges();
            AssertExample();
        }

        [TestMethod]
        public void GetAllChildren_NestedNode_ValidSequence()
        {
            Context.Items.Add(ExampleItems["Electronics"], null);
            Context.Items.Add(ExampleItems["Clothing"], null);
            Context.Items.Add(ExampleItems["SmartPhones"], ExampleItems["Electronics"]);
            Context.Items.Add(ExampleItems["Computers"], ExampleItems["Electronics"]);
            Context.Items.Add(ExampleItems["iPhones"], ExampleItems["SmartPhones"]);
            Context.Items.Insert(ExampleItems["Android"], ExampleItems["iPhones"]);

            Context.Items.Add(ExampleItems["iPhone SE"], ExampleItems["iPhones"]);
            Context.Items.Add(ExampleItems["iPhone Pro"], ExampleItems["iPhones"]);
            Context.Items.Add(ExampleItems["Desktops"], ExampleItems["Computers"]);
            Context.Items.Add(ExampleItems["HP"], ExampleItems["Desktops"]);
            Context.Items.Add(ExampleItems["Dell"], ExampleItems["Desktops"]);

            Context.Items.Insert(ExampleItems["Laptops"], ExampleItems["Computers"]);
            Context.Items.Add(ExampleItems["Windows"], ExampleItems["Laptops"]);
            Context.Items.Add(ExampleItems["MacBooks"], ExampleItems["Laptops"]);
            Context.SaveChanges();

            var children = Context.Items.GetAllChildren(ExampleItems["SmartPhones"]).ToList();
            var expected = new[] { "Android", "iPhones", "iPhone SE", "iPhone Pro" }.Select(GetExampleEntity).ToList();
            CollectionAssert.AreEquivalent(children, expected);
        }

        [TestMethod]
        public void GetChildren_NestedNode_ValidSequence()
        {
            Context.Items.Add(ExampleItems["Electronics"], null);
            Context.Items.Add(ExampleItems["Clothing"], null);
            Context.Items.Add(ExampleItems["SmartPhones"], ExampleItems["Electronics"]);
            Context.Items.Add(ExampleItems["Computers"], ExampleItems["Electronics"]);
            Context.Items.Add(ExampleItems["iPhones"], ExampleItems["SmartPhones"]);
            Context.Items.Insert(ExampleItems["Android"], ExampleItems["iPhones"]);

            Context.Items.Add(ExampleItems["iPhone SE"], ExampleItems["iPhones"]);
            Context.Items.Add(ExampleItems["iPhone Pro"], ExampleItems["iPhones"]);
            Context.Items.Add(ExampleItems["Desktops"], ExampleItems["Computers"]);
            Context.Items.Add(ExampleItems["HP"], ExampleItems["Desktops"]);
            Context.Items.Add(ExampleItems["Dell"], ExampleItems["Desktops"]);

            Context.Items.Insert(ExampleItems["Laptops"], ExampleItems["Computers"]);
            Context.Items.Add(ExampleItems["Windows"], ExampleItems["Laptops"]);
            Context.Items.Add(ExampleItems["MacBooks"], ExampleItems["Laptops"]);
            Context.SaveChanges();

            var children = Context.Items.GetChildren(ExampleItems["SmartPhones"]).ToList();
            var expected = new[] { "Android", "iPhones" }.Select(GetExampleEntity).ToList();
            CollectionAssert.AreEquivalent(children, expected);
        }


        /// <summary>
        /// Tests case 3 source and target relationship.
        /// </summary>
        /// <remarks>
        /// Initial state:
        ///                                                   ┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
        ///                                           1 Electronics 26                                                      27 Clothing 28
        ///                ┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━╋━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
        ///         2 SmartPhones 11                    12 Laptops 17                     18 Computers 25
        ///         ┏━━━━━━┻━━━━━━┓                    ┏━━━━━━┻━━━━━━┓                           ┃
        ///    3 Android 4   5 iPhones 10       13 Windows 14   15 MacBooks 16             19 Desktops 24
        ///              ┏━━━━━━━━┻━━━━━━━━┓                                              ┏━━━━━━┻━━━━━━┓
        ///       6 iPhone SE 7     8 iPhone Pro 9                                    20 HP 21      22 Dell 23
        ///       
        ///       
        /// After move:
        ///                                                   ┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
        ///                                           1 Electronics 26                                                      27 Clothing 28
        ///                ┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┻━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
        ///         2 SmartPhones 11                                                      12 Computers 25
        ///         ┏━━━━━━┻━━━━━━┓                                            ┏━━━━━━━━━━━━━━━━━┻━━━━━━━━━━━━━━━━━┓
        ///    3 Android 4   5 iPhones 10                               13 Desktops 18                       19 Laptops 24            
        ///              ┏━━━━━━━━┻━━━━━━━━┓                            ┏━━━━━━┻━━━━━━┓                     ┏━━━━━━┻━━━━━━┓           
        ///       6 iPhone SE 7     8 iPhone Pro 9                  14 HP 15      16 Dell 17         20 Windows 21   22 MacBooks 23   
        /// </remarks>
        [TestMethod]
        public void MoveCase3_NodeWithChildren_ValidTree()
        {
            Context.Items.Add(ExampleItems["Electronics"], null);
            Context.Items.Add(ExampleItems["SmartPhones"], ExampleItems["Electronics"]);
            Context.Items.Add(ExampleItems["Android"], ExampleItems["SmartPhones"]);
            Context.Items.Add(ExampleItems["iPhones"], ExampleItems["SmartPhones"]);
            Context.Items.Add(ExampleItems["iPhone SE"], ExampleItems["iPhones"]);
            Context.Items.Add(ExampleItems["iPhone Pro"], ExampleItems["iPhones"]);
            Context.Items.Add(ExampleItems["Laptops"], ExampleItems["Electronics"]);
            Context.Items.Add(ExampleItems["Windows"], ExampleItems["Laptops"]);
            Context.Items.Add(ExampleItems["MacBooks"], ExampleItems["Laptops"]);
            Context.Items.Add(ExampleItems["Computers"], ExampleItems["Electronics"]);
            Context.Items.Add(ExampleItems["Desktops"], ExampleItems["Computers"]);
            Context.Items.Add(ExampleItems["HP"], ExampleItems["Desktops"]);
            Context.Items.Add(ExampleItems["Dell"], ExampleItems["Desktops"]);

            Context.Items.Add(ExampleItems["Clothing"], null);
            Context.SaveChanges();

            //Move it back to 'Electronics'
            Context.Items.Move(ExampleItems["Laptops"], ExampleItems["Computers"]);

            AssertNode(null, "Electronics", "Clothing");
            AssertNode("Electronics", "SmartPhones", "Computers");
            AssertNode("SmartPhones", "Android", "iPhones");
            AssertNode("Laptops", "Windows", "MacBooks");
            AssertNode("Computers", "Desktops", "Laptops");
            AssertNode("iPhones", "iPhone SE", "iPhone Pro");
            AssertNode("Desktops", "HP", "Dell");
        }


        /// <summary>
        /// Tests case 4 source and target relationship.
        /// </summary>
        /// <remarks>
        /// Initial state:
        ///                                                   ┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
        ///                                           1 Electronics 26                                                      27 Clothing 28
        ///                ┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┻━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
        ///         2 SmartPhones 11                                                      12 Computers 25
        ///         ┏━━━━━━┻━━━━━━┓                                            ┏━━━━━━━━━━━━━━━━━┻━━━━━━━━━━━━━━┓
        ///    3 Android 4   5 iPhones 10                               13 Desktops 18                    19 Laptops 24            
        ///              ┏━━━━━━━━┻━━━━━━━━┓                            ┏━━━━━━┻━━━━━━┓                  ┏━━━━━━┻━━━━━━┓           
        ///       6 iPhone SE 7     8 iPhone Pro 9                  14 HP 15      16 Dell 17      20 Windows 21   22 MacBooks 23   
        ///       
        ///       
        /// After move:
        ///                                                   ┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
        ///                                           1 Electronics 26                                                      27 Clothing 28
        ///                ┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┻━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
        ///         2 SmartPhones 5                                                       6 Computers 25
        ///                ┃                                       ┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━╋━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
        ///           3 Android 4                            7 Desktops 12                 13 Laptops 18                     19 iPhones 24          
        ///                                                 ┏━━━━━━┻━━━━━━┓               ┏━━━━━━┻━━━━━━┓                  ┏━━━━━━━━┻━━━━━━━━┓       
        ///                                              8 HP 9       10 Dell 11   14 Windows 15   16 MacBooks 17  20 iPhone SE 21    22 iPhone Pro 23 
        /// </remarks>
        [TestMethod]
        public void MoveCase4_NodeWithChildren_ValidTree()
        {
            Context.Items.Add(ExampleItems["Electronics"], null);
            Context.Items.Add(ExampleItems["SmartPhones"], ExampleItems["Electronics"]);
            Context.Items.Add(ExampleItems["Android"], ExampleItems["SmartPhones"]);
            Context.Items.Add(ExampleItems["iPhones"], ExampleItems["SmartPhones"]);
            Context.Items.Add(ExampleItems["iPhone SE"], ExampleItems["iPhones"]);
            Context.Items.Add(ExampleItems["iPhone Pro"], ExampleItems["iPhones"]);
            Context.Items.Add(ExampleItems["Laptops"], ExampleItems["Electronics"]);
            Context.Items.Add(ExampleItems["Windows"], ExampleItems["Laptops"]);
            Context.Items.Add(ExampleItems["MacBooks"], ExampleItems["Laptops"]);
            Context.Items.Add(ExampleItems["Computers"], ExampleItems["Electronics"]);
            Context.Items.Add(ExampleItems["Desktops"], ExampleItems["Computers"]);
            Context.Items.Add(ExampleItems["HP"], ExampleItems["Desktops"]);
            Context.Items.Add(ExampleItems["Dell"], ExampleItems["Desktops"]);

            Context.Items.Add(ExampleItems["Clothing"], null);
            Context.SaveChanges();

            //Move it back to 'Electronics'
            Context.Items.Move(ExampleItems["iPhones"], ExampleItems["Computers"]);

            AssertNode(null, "Electronics", "Clothing");
            AssertNode("Electronics", "SmartPhones", "Computers");
            AssertNode("SmartPhones", "Android");
            AssertNode("Laptops", "Windows", "MacBooks");
            AssertNode("Computers", "Desktops", "Laptops", "iPhones");
            AssertNode("iPhones", "iPhone SE", "iPhone Pro");
            AssertNode("Desktops", "HP", "Dell");
        }
    }
}