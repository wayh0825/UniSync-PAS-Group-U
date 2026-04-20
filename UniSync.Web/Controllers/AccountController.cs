using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniSync.Core.Data;
using UniSync.Core.Entities;
using UniSync.Web.ViewModels;
using System.Threading.Tasks;
using UniSync.Core.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Linq;
using System;
using System.Collections.Generic;

using Microsoft.AspNetCore.SignalR;
using UniSync.Web.Hubs;

namespace UniSync.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<SignalHub> _signalHubContext;

        public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, ApplicationDbContext context, IHubContext<SignalHub> signalHubContext)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _signalHubContext = signalHubContext;
        }

        [HttpGet]
        public IActionResult Register()
        {
            ViewBag.Roles = new[] { Roles.Student.ToString(), Roles.Supervisor.ToString() }
                .Select(r => new SelectListItem { Value = r, Text = r });
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser 
                { 
                    UserName = model.Email, 
                    Email = model.Email, 
                    FullName = model.FullName 
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    // Ensure only allowed roles are selected.
                    if (model.Role == Roles.Student.ToString() || model.Role == Roles.Supervisor.ToString())
                    {
                        await _userManager.AddToRoleAsync(user, model.Role);
                    }
                    else 
                    {
                        await _userManager.AddToRoleAsync(user, Roles.Student.ToString());
                    }

                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return RedirectToAction("Index", "Home");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            ViewBag.Roles = new[] { Roles.Student.ToString(), Roles.Supervisor.ToString() }
                .Select(r => new SelectListItem { Value = r, Text = r });
            return View(model);
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

                if (result.Succeeded)
                {
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) && returnUrl != "/")
                        return Redirect(returnUrl);

                    var user = await _userManager.FindByEmailAsync(model.Email);
                    if (user != null)
                    {
                        var roles = await _userManager.GetRolesAsync(user);
                        if (roles.Contains(Roles.Administrator.ToString()))
                            return RedirectToAction("Index", "Admin");
                        if (roles.Contains(Roles.ModuleLeader.ToString()))
                            return RedirectToAction("Dashboard", "ModuleLeader");
                        if (roles.Contains(Roles.Supervisor.ToString()))
                            return RedirectToAction("Dashboard", "Supervisor");
                        if (roles.Contains(Roles.Student.ToString()))
                            return RedirectToAction("Dashboard", "Student");
                    }

                    return RedirectToAction("Index", "Home");
                }
                
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public IActionResult ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                // In a real application, token generation and email dispatch occurs here.
                ViewData["Message"] = "A password reset link has been sent to your university email address.";
            }
            return View(model);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            var model = await BuildProfileViewModelAsync(user);
            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            var currentEmail = user.Email ?? string.Empty;
            var isStudent = await _userManager.IsInRoleAsync(user, Roles.Student.ToString());

            // Student accounts must keep the institution-assigned email immutable.
            if (isStudent)
            {
                model.Email = currentEmail;
            }

            if (string.IsNullOrWhiteSpace(model.Email))
            {
                model.Email = currentEmail;
            }

            if (!ModelState.IsValid)
            {
                var fallbackModel = await BuildProfileViewModelAsync(user);
                fallbackModel.FullName = model.FullName;
                fallbackModel.PhoneNumber = model.PhoneNumber;
                fallbackModel.Email = model.Email;
                return View(fallbackModel);
            }

            var hasErrors = false;

            if (!string.Equals(model.Email, currentEmail, StringComparison.OrdinalIgnoreCase))
            {
                var setEmailResult = await _userManager.SetEmailAsync(user, model.Email);
                if (!setEmailResult.Succeeded)
                {
                    hasErrors = true;
                    foreach (var error in setEmailResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }

                var setUserNameResult = await _userManager.SetUserNameAsync(user, model.Email);
                if (!setUserNameResult.Succeeded)
                {
                    hasErrors = true;
                    foreach (var error in setUserNameResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
            }

            user.FullName = model.FullName.Trim();
            user.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                hasErrors = true;
                foreach (var error in updateResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            if (hasErrors)
            {
                var failedModel = await BuildProfileViewModelAsync(user);
                failedModel.FullName = model.FullName;
                failedModel.PhoneNumber = model.PhoneNumber;
                failedModel.Email = model.Email;
                return View(failedModel);
            }

            await _signInManager.RefreshSignInAsync(user);
            
            // Broadcast Identity Change for real-time UI updates
            await _signalHubContext.Clients.All.SendAsync("IdentityChanged", user.Id, user.FullName);

            TempData["ProfileSaved"] = "Profile updated successfully.";

            return RedirectToAction(nameof(Profile));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            if (!ModelState.IsValid)
            {
                var validationErrors = ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .SelectMany(x => x.Value!.Errors)
                    .Select(e => e.ErrorMessage)
                    .Where(msg => !string.IsNullOrWhiteSpace(msg))
                    .ToList();

                TempData["PasswordError"] = validationErrors.Any()
                    ? string.Join(" ", validationErrors)
                    : "Please check password fields and try again.";

                return RedirectToAction(nameof(Profile));
            }

            IdentityResult changeResult;
            var hasPassword = await _userManager.HasPasswordAsync(user);

            if (hasPassword)
            {
                changeResult = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            }
            else
            {
                changeResult = await _userManager.AddPasswordAsync(user, model.NewPassword);
            }

            if (!changeResult.Succeeded)
            {
                TempData["PasswordError"] = string.Join(" ", changeResult.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Profile));
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["PasswordSaved"] = "Password updated successfully.";

            return RedirectToAction(nameof(Profile));
        }

        private async Task<ProfileViewModel> BuildProfileViewModelAsync(ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var roleLabel = roles.FirstOrDefault() ?? "User";
            var isStudent = string.Equals(roleLabel, Roles.Student.ToString(), StringComparison.OrdinalIgnoreCase);
            var isSupervisor = string.Equals(roleLabel, Roles.Supervisor.ToString(), StringComparison.OrdinalIgnoreCase);

            var fullName = string.IsNullOrWhiteSpace(user.FullName) ? (user.UserName ?? "User") : user.FullName;
            var initials = BuildInitials(fullName);

            var totalConversations = await _context.ChatMessages
                .Where(m => m.SenderId == user.Id || m.RecipientId == user.Id)
                .Select(m => m.SenderId == user.Id ? m.RecipientId : m.SenderId)
                .Distinct()
                .CountAsync();

            var unreadNotifications = await _context.AppNotifications
                .CountAsync(n => n.RecipientId == user.Id && !n.IsRead);

            var sentMessages = await _context.ChatMessages
                .CountAsync(m => m.SenderId == user.Id);

            var model = new ProfileViewModel
            {
                UserId = user.Id,
                FullName = fullName,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                UserName = user.UserName ?? string.Empty,
                RoleLabel = roleLabel,
                Initials = initials,
                IsStudent = isStudent,
                IsSupervisor = isSupervisor,
                Metrics = BuildMetrics(roleLabel, user.Id, totalConversations, unreadNotifications, sentMessages)
            };

            if (isSupervisor)
            {
                var expertiseAreas = await _context.SupervisorExpertiseDomains
                    .Where(x => x.SupervisorId == user.Id)
                    .Select(x => x.ExpertiseDomain.Name)
                    .OrderBy(x => x)
                    .ToListAsync();

                model.ExpertiseAreas = expertiseAreas;

                model.SupervisorLinkedCount = await _context.ResearchSubmissions
                    .CountAsync(p => p.LinkedSupervisorId == user.Id && p.Status == SubmissionStatus.Linked);

                model.SupervisorApprovedCount = await _context.ResearchSubmissions
                    .CountAsync(p => p.LinkedSupervisorId == user.Id && p.Status == SubmissionStatus.Approved);

                model.SupervisorInProgressCount = await _context.ResearchSubmissions
                    .CountAsync(p => p.LinkedSupervisorId == user.Id && p.Status == SubmissionStatus.InProgress);

                model.SupervisorCompletedCount = await _context.ResearchSubmissions
                    .CountAsync(p => p.LinkedSupervisorId == user.Id && p.Status == SubmissionStatus.Completed);

                model.SupervisorPendingDecisionCount = await _context.ResearchSubmissions
                    .CountAsync(p => p.LinkedSupervisorId == user.Id &&
                                     (p.Status == SubmissionStatus.Linked || p.Status == SubmissionStatus.ChangesRequested));

                model.SupervisorUnreadMessages = await _context.ChatMessages
                    .CountAsync(m => m.RecipientId == user.Id && !m.IsRead);

                model.RecentSupervisoredSubmissions = await _context.ResearchSubmissions
                    .Include(p => p.ExpertiseDomain)
                    .Where(p => p.LinkedSupervisorId == user.Id)
                    .OrderByDescending(p => p.SubmissionDate)
                    .Take(5)
                    .Select(p => new SupervisorProfileProjectItemViewModel
                    {
                        SubmissionId = p.Id,
                        Title = p.Title,
                        ExpertiseDomainName = p.ExpertiseDomain != null ? p.ExpertiseDomain.Name : "N/A",
                        Status = p.Status.ToString()
                    })
                    .ToListAsync();
            }

            return model;
        }

        private IList<ProfileMetricViewModel> BuildMetrics(string roleLabel, string userId, int conversations, int unreadNotifications, int sentMessages)
        {
            var metrics = new List<ProfileMetricViewModel>();

            if (string.Equals(roleLabel, Roles.Student.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                var totalSubmissions = _context.ResearchSubmissions.Count(p => p.SubmitterId == userId);
                var underReviewCount = _context.ResearchSubmissions.Count(p => p.SubmitterId == userId && p.Status == SubmissionStatus.UnderReview);
                var linkedCount = _context.ResearchSubmissions.Count(p => p.SubmitterId == userId && p.Status == SubmissionStatus.Linked);

                metrics.Add(new ProfileMetricViewModel { Label = "Total Submissions", Value = totalSubmissions.ToString(), Icon = "description", SurfaceClass = "bg-blue-50 text-blue-700 border-blue-100" });
                metrics.Add(new ProfileMetricViewModel { Label = "Under Review", Value = underReviewCount.ToString(), Icon = "query_stats", SurfaceClass = "bg-amber-50 text-amber-700 border-amber-100" });
                metrics.Add(new ProfileMetricViewModel { Label = "Linked", Value = linkedCount.ToString(), Icon = "military_tech", SurfaceClass = "bg-emerald-50 text-emerald-700 border-emerald-100" });
            }
            else if (string.Equals(roleLabel, Roles.Supervisor.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                var supervised = _context.ResearchSubmissions.Count(p => p.LinkedSupervisorId == userId);
                var pendingDecisions = _context.ResearchSubmissions.Count(p => p.LinkedSupervisorId == userId && (p.Status == SubmissionStatus.Linked || p.Status == SubmissionStatus.ChangesRequested));
                var unreadChats = _context.ChatMessages.Count(m => m.RecipientId == userId && !m.IsRead);

                metrics.Add(new ProfileMetricViewModel { Label = "Supervised Projects", Value = supervised.ToString(), Icon = "school", SurfaceClass = "bg-emerald-50 text-emerald-700 border-emerald-100" });
                metrics.Add(new ProfileMetricViewModel { Label = "Pending Decisions", Value = pendingDecisions.ToString(), Icon = "pending_actions", SurfaceClass = "bg-amber-50 text-amber-700 border-amber-100" });
                metrics.Add(new ProfileMetricViewModel { Label = "Unread Messages", Value = unreadChats.ToString(), Icon = "mark_chat_unread", SurfaceClass = "bg-blue-50 text-blue-700 border-blue-100" });
            }
            else
            {
                var totalUsers = _context.Users.Count();
                var totalSubmissions = _context.ResearchSubmissions.Count();
                var pendingSubmissions = _context.ResearchSubmissions.Count(p => p.Status == SubmissionStatus.Pending || p.Status == SubmissionStatus.UnderReview);

                metrics.Add(new ProfileMetricViewModel { Label = "Total Users", Value = totalUsers.ToString(), Icon = "group", SurfaceClass = "bg-blue-50 text-blue-700 border-blue-100" });
                metrics.Add(new ProfileMetricViewModel { Label = "Total Submissions", Value = totalSubmissions.ToString(), Icon = "description", SurfaceClass = "bg-indigo-50 text-indigo-700 border-indigo-100" });
                metrics.Add(new ProfileMetricViewModel { Label = "Pending Queue", Value = pendingSubmissions.ToString(), Icon = "schedule", SurfaceClass = "bg-amber-50 text-amber-700 border-amber-100" });
            }

            metrics.Add(new ProfileMetricViewModel { Label = "Conversations", Value = conversations.ToString(), Icon = "forum", SurfaceClass = "bg-sky-50 text-sky-700 border-sky-100" });
            metrics.Add(new ProfileMetricViewModel { Label = "Unread Notifications", Value = unreadNotifications.ToString(), Icon = "notifications", SurfaceClass = "bg-rose-50 text-rose-700 border-rose-100" });
            metrics.Add(new ProfileMetricViewModel { Label = "Messages Sent", Value = sentMessages.ToString(), Icon = "send", SurfaceClass = "bg-violet-50 text-violet-700 border-violet-100" });

            return metrics;
        }

        private static string BuildInitials(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return "U";
            }

            var parts = fullName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Take(2)
                .Select(p => char.ToUpperInvariant(p[0]));

            return string.Concat(parts);
        }
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetRecentNotifications()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var notifications = await _context.AppNotifications
                .Where(n => n.RecipientId == user.Id)
                .OrderByDescending(n => n.CreatedAt)
                .Take(5)
                .Select(n => new {
                    n.Id,
                    n.Title,
                    n.Message,
                    n.IsRead,
                    TimeLabel = GetRelativeTime(n.CreatedAt),
                    TypeLabel = n.Type.ToString(),
                    ActionUrl = n.ActionUrl
                })
                .ToListAsync();

            return Json(notifications);
        }

        private static string GetRelativeTime(DateTime utcTime)
        {
            var timeSpan = DateTime.UtcNow - utcTime;
            if (timeSpan.TotalMinutes < 1) return "Just now";
            if (timeSpan.TotalHours < 1) return $"{(int)timeSpan.TotalMinutes}m ago";
            if (timeSpan.TotalDays < 1) return $"{(int)timeSpan.TotalHours}h ago";
            return utcTime.ToLocalTime().ToString("MMM dd");
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var unreadNotifications = await _context.AppNotifications
                .Where(n => n.RecipientId == user.Id && !n.IsRead)
                .ToListAsync();

            if (unreadNotifications.Any())
            {
                foreach (var notification in unreadNotifications)
                {
                    notification.IsRead = true;
                }
                await _context.SaveChangesAsync();
            }

            return Ok();
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> OpenNotification(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Challenge();

            var notification = await _context.AppNotifications
                .FirstOrDefaultAsync(n => n.Id == id && n.RecipientId == user.Id);

            if (notification is null) return NotFound();

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            if (!string.IsNullOrWhiteSpace(notification.ActionUrl) && Url.IsLocalUrl(notification.ActionUrl))
            {
                return LocalRedirect(notification.ActionUrl);
            }

            // Default redirection based on role
            if (User.IsInRole("Administrator")) return RedirectToAction("Index", "Admin");
            if (User.IsInRole("ModuleLeader")) return RedirectToAction("Dashboard", "ModuleLeader");
            if (User.IsInRole("Supervisor")) return RedirectToAction("Dashboard", "Supervisor");
            return RedirectToAction("Dashboard", "Student");
        }
    }
}
