using System;
using System.Collections.Generic;
using System.Linq;
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
using UniSync.Web.ViewModels;
using Xunit;

namespace UniSync.Tests
{
    public class StudentControllerTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
        private readonly StudentController _controller;

        public StudentControllerTests()
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
                new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
            }, "mock"));

            _controller = new StudentController(_context, _userManagerMock.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = httpContext
                },
                TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
            };
        }

        [Fact]
        public async Task EditSubmission_Post_ValidModel_UpdatesSubmissionAndGroupMembers()
        {
            // Arrange
            var user = new ApplicationUser { Id = "test-user-id", FullName = "Test User" };
            _userManagerMock.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

            var submission = new ResearchSubmission
            {
                Id = 1,
                SubmitterId = "test-user-id",
                Status = SubmissionStatus.Pending,
                Title = "Old Title",
                ExecutiveSummary = "Old ExecutiveSummary",
                TechStack = "Tech"
            };
            _context.ExpertiseDomains.Add(new ExpertiseDomain { Id = 1, Name = "Test Area" });
            _context.ResearchSubmissions.Add(submission);
            await _context.SaveChangesAsync();

            var model = new SubmissionSubmissionViewModel
            {
                Title = "New Title",
                ExecutiveSummary = "This is a sufficiently long abstract string to pass the greater than 100 characters validation requirement. Yes indeed it is. 1234567890 1234567890",
                ProblemStatement = "Valid",
                Objectives = "Valid",
                Methodology = "Valid",
                ExpectedOutcomes = "Valid",
                TechStack = "Tech",
                Keywords = "Valid",
                EthicsConsiderations = "Valid",
                ReferencesText = "Valid",
                TimelineWeeks = 10,
                AgreeBlindRule = true,
                ExpertiseDomainId = 1,
                IsGroupProject = true,
                GroupMembers = new List<GroupMemberViewModel>
                {
                    new GroupMemberViewModel { FullName = "Group Member 1", Email = "m1@test.com", StudentIdIdentifier = "ID1" }
                }
            };

            // Act
            _controller.ModelState.Clear();
            var result = await _controller.EditSubmission(1, model);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Dashboard", redirectResult.ActionName);

            var updatedSubmission = await _context.ResearchSubmissions.Include(p => p.GroupMembers).FirstAsync();
            Assert.Equal("New Title", updatedSubmission.Title);
            Assert.Equal("This is a sufficiently long abstract string to pass the greater than 100 characters validation requirement. Yes indeed it is. 1234567890 1234567890", updatedSubmission.ExecutiveSummary);
            Assert.True(updatedSubmission.IsGroupProject);
            Assert.Single(updatedSubmission.GroupMembers);
            Assert.Equal("Group Member 1", updatedSubmission.GroupMembers.First().FullName);
        }

        [Fact]
        public async Task WithdrawSubmission_Post_PendingSubmission_RemovesSubmission()
        {
            // Arrange
            var user = new ApplicationUser { Id = "test-user-id", FullName = "Test User" };
            _userManagerMock.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

            var submission = new ResearchSubmission
            {
                Id = 2,
                SubmitterId = "test-user-id",
                Status = SubmissionStatus.Pending,
                Title = "Delete Me",
                ExecutiveSummary = "A",
                TechStack = "T"
            };
            _context.ResearchSubmissions.Add(submission);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.WithdrawSubmission(2);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Dashboard", redirectResult.ActionName);

            var p = await _context.ResearchSubmissions.FirstOrDefaultAsync(x => x.Id == 2);
            Assert.Null(p);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }
}
