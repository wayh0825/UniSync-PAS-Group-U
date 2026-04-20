using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using UniSync.Core.Data;
using UniSync.Core.Entities;
using UniSync.Core.Enums;
using UniSync.Web.Controllers;
using UniSync.Web.Services;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Xunit;

namespace UniSync.Tests
{
    public class LinkingLogicTests
    {
        [Fact]
        public async Task ExpressInterest_SetsStatusToLinked_And_RevealsIdentity()
        {
            // Arrange
            var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
            var userManagerMock = new Mock<UserManager<ApplicationUser>>(userStoreMock.Object, null, null, null, null, null, null, null, null);

            var mentorUser = new ApplicationUser { Id = "sup1", FullName = "Dr. Jane Smith" };
            userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(mentorUser);
            userManagerMock.Setup(u => u.FindByIdAsync(It.IsAny<string>())).ReturnsAsync(mentorUser);
            userManagerMock.Setup(u => u.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns("sup1");

            var linkingServiceMock = new Mock<ISubmissionLinkingService>();

            var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "UniSync_Link_Test")
                .Options;

            using (var context = new ApplicationDbContext(options))
            {
                context.Users.Add(new ApplicationUser { Id = "student1", UserName = "student1", FullName = "Student 1", Email = "student1@test.com" });
                var submission = new ResearchSubmission
                {
                    Id = 1,
                    Title = "Test Project",
                    ExecutiveSummary = "abstract",
                    TechStack = "tech",
                    Status = SubmissionStatus.Pending,
                    SubmitterId = "student1"
                };
                context.ResearchSubmissions.Add(submission);
                context.SaveChanges();
            }

            // Act
            using (var context = new ApplicationDbContext(options))
            {
                var httpContext = new DefaultHttpContext();
                httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "sup1"),
                }, "mock"));

                var urlHelperMock = new Mock<IUrlHelper>();
                urlHelperMock.Setup(x => x.Action(It.IsAny<UrlActionContext>())).Returns("mock/url");

                var controller = new MentorController(context, userManagerMock.Object, linkingServiceMock.Object)
                {
                    ControllerContext = new ControllerContext
                    {
                        HttpContext = httpContext
                    },
                    TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>()),
                    Url = urlHelperMock.Object
                };
                var initial = await context.ResearchSubmissions.FindAsync(1);
                Assert.NotNull(initial);
                Assert.Equal(SubmissionStatus.Pending, initial.Status);

                var result = await controller.ExpressInterest(1);

                Assert.IsType<RedirectToActionResult>(result);

                // Assert
                var updatedSubmission = await context.ResearchSubmissions.FindAsync(1);
                Assert.NotNull(updatedSubmission);
                Assert.Equal(SubmissionStatus.Linked, updatedSubmission.Status);
                Assert.Equal("sup1", updatedSubmission.LinkedMentorId);
            }
        }
    }
}
