using System.Linq;
using Microsoft.EntityFrameworkCore;
using UniSync.Core.Data;
using UniSync.Core.Entities;
using Xunit;

namespace UniSync.Tests
{
    public class DatabaseIntegrationTests
    {
        [Fact]
        public void CanAddAndRetrieveExpertiseDomains()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "UniSync_Integration_QA")
                .Options;

            using (var context = new ApplicationDbContext(options))
            {
                var newArea = new ExpertiseDomain { Name = "Artificial Intelligence" };
                
                // Act
                context.ExpertiseDomains.Add(newArea);
                context.SaveChanges();
            }

            using (var context = new ApplicationDbContext(options))
            {
                // Assert
                var areas = context.ExpertiseDomains.ToList();
                Assert.Single(areas);
                Assert.Equal("Artificial Intelligence", areas.First().Name);
            }
        }
    }
}
