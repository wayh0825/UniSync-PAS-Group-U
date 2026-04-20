using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using UniSync.Core.Data;
using UniSync.Core.Entities;
using UniSync.Core.Enums;
using UniSync.Web.Controllers;
using Xunit;

namespace UniSync.Tests
{
    public class AdminControllerTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
        private readonly AdminController _controller;

        public AdminControllerTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);

            var store = new Mock<IUserStore<ApplicationUser>>();
            _userManagerMock = new Mock<UserManager<ApplicationUser>>(store.Object, null, null, null, null, null, null, null, null);

            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.Name, "AdminUser"),
                new Claim(ClaimTypes.Role, "ModuleLeader")
            }, "mock"));

            _controller = new AdminController(_context, _userManagerMock.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = httpContext
                },
                TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
            };
        }

        [Fact]
        public async Task ForceLink_ValidData_UpdatesSubmissionAndReturnsRedirect()
        {
            // Arrange
            var submission = new ResearchSubmission
            {
                Id = 10,
                Status = SubmissionStatus.Pending,
                Title = "Unlinked Project",
                ExecutiveSummary = "ExecutiveSummary required",
                TechStack = "Tech",
                SubmitterId = "user1"
            };
            
            _context.ResearchSubmissions.Add(submission);
            await _context.SaveChangesAsync();

            var mentor = new ApplicationUser { Id = "super-1", FullName = "Dr. Mentor" };
            _userManagerMock.Setup(x => x.FindByIdAsync("super-1")).ReturnsAsync(mentor);

            // Act
            var result = await _controller.ForceLink(10, "super-1");

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("ManageSubmissionDetails", redirectResult.ActionName);

            var updatedSubmission = await _context.ResearchSubmissions.FindAsync(10);
            Assert.NotNull(updatedSubmission);
            Assert.Equal(SubmissionStatus.Linked, updatedSubmission.Status);
            Assert.Equal("super-1", updatedSubmission.LinkedMentorId);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }
}
