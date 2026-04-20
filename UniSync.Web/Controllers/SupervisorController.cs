using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using UniSync.Core.Data;
using UniSync.Core.Entities;
using UniSync.Core.Enums;
using UniSync.Web.Hubs;
using UniSync.Web.Services;
using UniSync.Web.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UniSync.Web.Controllers
{
    [Authorize(Roles = "Supervisor")]
    public class SupervisorController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ISubmissionLinkingService _linkingService;
        private readonly INotificationService _notificationService;
        private readonly IHubContext<LinkingHub>? _hubContext;
        private readonly IHubContext<SignalHub> _signalHubContext;

        public SupervisorController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ISubmissionLinkingService linkingService,
            INotificationService notificationService,
            IHubContext<SignalHub> signalHubContext,
            IHubContext<LinkingHub>? hubContext = null)
        {
            _context = context;
            _userManager = userManager;
            _linkingService = linkingService;
            _notificationService = notificationService;
            _signalHubContext = signalHubContext;
            _hubContext = hubContext;
        }

        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var selectedAreaIds = await _context.SupervisorExpertiseDomains
                .Where(x => x.SupervisorId == user.Id)
                .Select(x => x.ExpertiseDomainId)
                .ToListAsync();

            var allExpertiseDomains = await _context.ExpertiseDomains
                .OrderBy(x => x.Name)
                .ToListAsync();

            var pendingQuery = _context.ResearchSubmissions
                .Include(p => p.ExpertiseDomain)
                .Include(p => p.LinkRequests)
                .Where(p => p.Status == SubmissionStatus.Pending);

            if (selectedAreaIds.Any())
            {
                pendingQuery = pendingQuery.Where(p => selectedAreaIds.Contains(p.ExpertiseDomainId));
            }

            var availableProjectsCount = await pendingQuery.CountAsync();
            var recommendedProjects = await pendingQuery
                .OrderByDescending(p => p.SubmissionDate)
                .Take(6)
                .ToListAsync();

            // Real-time stats from database
            var myLinksCount = await _context.ResearchSubmissions
                .CountAsync(p => p.LinkedSupervisorId == user.Id && p.Status == SubmissionStatus.Linked);

            var approvedCount = await _context.ResearchSubmissions
                .CountAsync(p => p.LinkedSupervisorId == user.Id && p.Status == SubmissionStatus.Approved);

            var inProgressCount = await _context.ResearchSubmissions
                .CountAsync(p => p.LinkedSupervisorId == user.Id && p.Status == SubmissionStatus.InProgress);

            var completedCount = await _context.ResearchSubmissions
                .CountAsync(p => p.LinkedSupervisorId == user.Id && p.Status == SubmissionStatus.Completed);

            var pendingChangesCount = await _context.ResearchSubmissions
                .CountAsync(p => p.LinkedSupervisorId == user.Id && p.Status == SubmissionStatus.ChangesRequested);

            var unreadMessagesCount = await _context.ChatMessages
                .CountAsync(m => m.RecipientId == user.Id && !m.IsRead);

            var viewModel = new SupervisorDashboardViewModel
            {
                SupervisorName = string.IsNullOrWhiteSpace(user.FullName) ? "Supervisor" : user.FullName,
                ExpertiseDomains = allExpertiseDomains,
                SelectedAreaIds = selectedAreaIds.ToHashSet(),
                RecommendedProjects = recommendedProjects,
                AvailableProjectsCount = availableProjectsCount,
                MyLinksCount = myLinksCount,
                ApprovedCount = approvedCount,
                InProgressCount = inProgressCount,
                CompletedCount = completedCount,
                PendingChangesCount = pendingChangesCount,
                UnreadMessagesCount = unreadMessagesCount
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveExpertise(List<int> selectedAreaIds)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            selectedAreaIds ??= new List<int>();

            var validAreaIds = await _context.ExpertiseDomains
                .Where(r => selectedAreaIds.Contains(r.Id))
                .Select(r => r.Id)
                .ToListAsync();

            var existing = await _context.SupervisorExpertiseDomains
                .Where(x => x.SupervisorId == user.Id)
                .ToListAsync();

            var keep = validAreaIds.ToHashSet();
            var removeItems = existing.Where(x => !keep.Contains(x.ExpertiseDomainId)).ToList();
            if (removeItems.Count > 0)
            {
                _context.SupervisorExpertiseDomains.RemoveRange(removeItems);
            }

            var existingIds = existing.Select(x => x.ExpertiseDomainId).ToHashSet();
            var addIds = validAreaIds.Where(id => !existingIds.Contains(id));
            foreach (var areaId in addIds)
            {
                _context.SupervisorExpertiseDomains.Add(new SupervisorExpertiseDomain
                {
                    SupervisorId = user.Id,
                    ExpertiseDomainId = areaId
                });
            }

            await _context.SaveChangesAsync();
            await BroadcastLinkingUpdateAsync("expertise-updated");

            TempData["ExpertiseSaved"] = "Expertise preferences updated successfully.";

            return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExpressInterest(int submissionId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }
            
            var submission = await _context.ResearchSubmissions
                .Include(p => p.Submitter)
                .FirstOrDefaultAsync(p => p.Id == submissionId);
            if (submission != null && submission.Status == SubmissionStatus.Pending)
            {
                var linkReq = new LinkRequest 
                { 
                    SubmissionId = submissionId, 
                    SupervisorId = user.Id 
                };
                
                _context.LinkRequests.Add(linkReq);
                
                // Approve blindly
                submission.Status = SubmissionStatus.Linked;
                submission.LinkedSupervisorId = user.Id;
                
                await _context.SaveChangesAsync();

                var studentName = string.IsNullOrWhiteSpace(submission.Submitter?.FullName) ? submission.Submitter?.UserName ?? "Student" : submission.Submitter.FullName;
                await CreateNotificationAsync(
                    recipientId: submission.SubmitterId,
                    senderId: user.Id,
                    type: AlertCategory.Link,
                    title: "Supervisor linked your submission",
                    message: $"{studentName}, your submission '{submission.Title}' has been linked and is now under supervision.",
                    actionUrl: Url.Action("SubmissionDetails", "Student", new { id = submission.Id }));

                await BroadcastLinkingUpdateAsync("submission-linked");
            }

            return RedirectToAction(nameof(Dashboard));
        }

        public async Task<IActionResult> LinkedProjects(string? search, string status = "all", string progress = "all", string sort = "newest", int page = 1, int pageSize = 8)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var supervisorId = user.Id;
            var normalizedSearch = string.IsNullOrWhiteSpace(search) ? string.Empty : search.Trim();
            var normalizedStatus = string.IsNullOrWhiteSpace(status) ? "all" : status.Trim().ToLowerInvariant();
            var normalizedProgress = string.IsNullOrWhiteSpace(progress) ? "all" : progress.Trim().ToLowerInvariant();
            var normalizedSort = string.IsNullOrWhiteSpace(sort) ? "newest" : sort.Trim().ToLowerInvariant();
            var normalizedPage = page < 1 ? 1 : page;
            var normalizedPageSize = pageSize is 8 or 12 or 20 ? pageSize : 8;

            var baseQuery = _context.ResearchSubmissions
                .Include(p => p.ExpertiseDomain)
                .Include(p => p.Submitter)
                .Include(p => p.GroupMembers)
                .Include(p => p.LinkRequests)
                .Where(p => p.LinkedSupervisorId == supervisorId);

            var totalCount = await baseQuery.CountAsync();

            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                baseQuery = baseQuery.Where(p =>
                    p.Title.Contains(normalizedSearch) ||
                    (p.ExpertiseDomain != null && p.ExpertiseDomain.Name.Contains(normalizedSearch)) ||
                    (p.Submitter != null && p.Submitter.FullName != null && p.Submitter.FullName.Contains(normalizedSearch)) ||
                    (p.Submitter != null && p.Submitter.Email != null && p.Submitter.Email.Contains(normalizedSearch)) ||
                    (p.TechStack != null && p.TechStack.Contains(normalizedSearch)));
            }

            if (!string.Equals(normalizedStatus, "all", StringComparison.OrdinalIgnoreCase) &&
                Enum.TryParse<SubmissionStatus>(normalizedStatus, true, out var parsedStatus))
            {
                baseQuery = baseQuery.Where(p => p.Status == parsedStatus);
            }

            baseQuery = ApplyProgressFilter(baseQuery, normalizedProgress);

            var filteredCount = await baseQuery.CountAsync();

            baseQuery = normalizedSort switch
            {
                "oldest" => baseQuery.OrderBy(p => p.SubmissionDate),
                "linkednew" => baseQuery.OrderByDescending(p => p.LinkRequests
                    .Where(m => m.SupervisorId == supervisorId)
                    .Select(m => (DateTime?)m.ExpressedAt)
                    .FirstOrDefault()),
                "title" => baseQuery.OrderBy(p => p.Title),
                "status" => baseQuery.OrderBy(p => p.Status),
                _ => baseQuery.OrderByDescending(p => p.SubmissionDate)
            };

            var totalPages = Math.Max(1, (int)Math.Ceiling(filteredCount / (double)normalizedPageSize));
            if (normalizedPage > totalPages)
            {
                normalizedPage = totalPages;
            }

            var linked = await baseQuery
                .Skip((normalizedPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .ToListAsync();

            var projectCards = linked.Select(project =>
            {
                var linkedDate = project.LinkRequests
                    .Where(m => m.SupervisorId == supervisorId)
                    .OrderByDescending(m => m.ExpressedAt)
                    .Select(m => (DateTime?)m.ExpressedAt)
                    .FirstOrDefault() ?? project.SubmissionDate;

                var studentName = string.IsNullOrWhiteSpace(project.Submitter?.FullName)
                    ? (project.Submitter?.UserName ?? "Student")
                    : project.Submitter!.FullName;

                var studentInitial = string.IsNullOrWhiteSpace(studentName)
                    ? "S"
                    : studentName.Substring(0, 1).ToUpperInvariant();

                var (statusLabel, statusClass) = GetStatusPresentation(project.Status);
                var (progressPercent, progressLabel) = GetProgressPresentation(project.Status);

                return new SupervisorLinkedProjectCardViewModel
                {
                    SubmissionId = project.Id,
                    Title = project.Title,
                    ExpertiseDomainName = project.ExpertiseDomain?.Name ?? "N/A",
                    StudentName = studentName,
                    StudentEmail = project.Submitter?.Email ?? string.Empty,
                    StudentInitial = studentInitial,
                    Status = project.Status,
                    StatusLabel = statusLabel,
                    StatusClass = statusClass,
                    ProgressPercent = progressPercent,
                    ProgressLabel = progressLabel,
                    SubmissionDate = project.SubmissionDate,
                    LinkedDate = linkedDate,
                    ExecutiveSummary = project.ExecutiveSummary,
                    TechStack = project.TechStack,
                    IsGroupProject = project.IsGroupProject,
                    GroupMembers = project.GroupMembers?.ToList() ?? new List<SubmissionGroupMember>()
                };
            }).ToList();

            var activeCount = await _context.ResearchSubmissions.CountAsync(p =>
                p.LinkedSupervisorId == supervisorId &&
                (p.Status == SubmissionStatus.Linked || p.Status == SubmissionStatus.Approved || p.Status == SubmissionStatus.InProgress));

            var attentionCount = await _context.ResearchSubmissions.CountAsync(p =>
                p.LinkedSupervisorId == supervisorId && p.Status == SubmissionStatus.ChangesRequested);

            var completedCount = await _context.ResearchSubmissions.CountAsync(p =>
                p.LinkedSupervisorId == supervisorId && p.Status == SubmissionStatus.Completed);

            var model = new SupervisorLinkedProjectsPageViewModel
            {
                Search = normalizedSearch,
                StatusFilter = normalizedStatus,
                ProgressFilter = normalizedProgress,
                SortBy = normalizedSort,
                Page = normalizedPage,
                PageSize = normalizedPageSize,
                TotalCount = totalCount,
                FilteredCount = filteredCount,
                ActiveCount = activeCount,
                AttentionCount = attentionCount,
                CompletedCount = completedCount,
                TotalPages = totalPages,
                Projects = projectCards
            };

            return View(model);
        }

        [Authorize(Roles = "Supervisor,Administrator")]
        public async Task<IActionResult> Messages(string? contactId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var contacts = await _context.ResearchSubmissions
                .Include(p => p.Submitter)
                .Where(p => p.LinkedSupervisorId == user.Id)
                .Select(p => new
                {
                    UserId = p.SubmitterId,
                    FullName = p.Submitter != null ? p.Submitter.FullName : null,
                    UserName = p.Submitter != null ? p.Submitter.UserName : null
                })
                .Distinct()
                .ToListAsync();

            var contactIds = contacts
                .Select(c => c.UserId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!)
                .ToHashSet();

            if (!string.IsNullOrWhiteSpace(contactId) && !contactIds.Contains(contactId))
            {
                contactId = null;
            }

            var activeContactId = contactId ?? contactIds.FirstOrDefault();

            var contactSummaries = new List<MessageContactViewModel>();
            foreach (var contact in contacts)
            {
                var lastSentAt = await _context.ChatMessages
                    .Where(m =>
                        (m.SenderId == user.Id && m.RecipientId == contact.UserId) ||
                        (m.SenderId == contact.UserId && m.RecipientId == user.Id))
                    .OrderByDescending(m => m.SentAt)
                    .Select(m => (DateTime?)m.SentAt)
                    .FirstOrDefaultAsync();

                var hasUnread = await _context.ChatMessages.AnyAsync(m =>
                    m.SenderId == contact.UserId &&
                    m.RecipientId == user.Id &&
                    !m.IsRead);

                contactSummaries.Add(new MessageContactViewModel
                {
                    UserId = contact.UserId!,
                    FullName = string.IsNullOrWhiteSpace(contact.FullName) ? (contact.UserName ?? "Student") : contact.FullName,
                    RoleLabel = "Student",
                    LastMessageAt = lastSentAt,
                    HasUnreadMessages = hasUnread
                });
            }

            contactSummaries = contactSummaries
                .OrderByDescending(c => c.LastMessageAt ?? DateTime.MinValue)
                .ThenBy(c => c.FullName)
                .ToList();

            var threadMessages = new List<MessageItemViewModel>();
            if (!string.IsNullOrWhiteSpace(activeContactId))
            {
                var history = await _context.ChatMessages
                    .Include(m => m.Sender)
                    .Where(m =>
                        (m.SenderId == user.Id && m.RecipientId == activeContactId) ||
                        (m.SenderId == activeContactId && m.RecipientId == user.Id))
                    .OrderBy(m => m.SentAt)
                    .ToListAsync();

                var unread = history
                    .Where(m => m.SenderId == activeContactId && m.RecipientId == user.Id && !m.IsRead)
                    .ToList();

                if (unread.Count > 0)
                {
                    foreach (var item in unread)
                    {
                        item.IsRead = true;
                    }

                    await _context.SaveChangesAsync();
                }

                threadMessages = history.Select(m => new MessageItemViewModel
                {
                    Id = m.Id,
                    SenderId = m.SenderId,
                    SenderName = string.IsNullOrWhiteSpace(m.Sender.FullName) ? (m.Sender.UserName ?? "User") : m.Sender.FullName,
                    Content = m.Content,
                    SentAt = m.SentAt,
                    IsMine = m.SenderId == user.Id
                }).ToList();
            }

            var model = new MessagesPageViewModel
            {
                CurrentUserId = user.Id,
                CurrentUserName = string.IsNullOrWhiteSpace(user.FullName) ? user.UserName ?? "Supervisor" : user.FullName,
                CurrentUserRoleLabel = "Supervisor",
                ActiveContactId = activeContactId,
                ActiveContactName = contactSummaries.FirstOrDefault(c => c.UserId == activeContactId)?.FullName,
                Contacts = contactSummaries,
                Messages = threadMessages,
                EmptyStateMessage = contactSummaries.Count == 0
                    ? "No linked students found yet. Contacts appear after linking."
                    : "No messages in this conversation yet."
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(string recipientId, string content)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (string.IsNullOrWhiteSpace(recipientId) || string.IsNullOrWhiteSpace(content))
            {
                return RedirectToAction(nameof(Messages), new { contactId = recipientId });
            }

            var canMessageRecipient = await _context.ResearchSubmissions.AnyAsync(p =>
                p.LinkedSupervisorId == user.Id &&
                p.SubmitterId == recipientId &&
                p.Status != SubmissionStatus.Rejected);

            if (!canMessageRecipient)
            {
                return Forbid();
            }

            _context.ChatMessages.Add(new ChatMessage
            {
                SenderId = user.Id,
                RecipientId = recipientId,
                Content = content.Trim(),
                SentAt = DateTime.UtcNow,
                IsRead = false
            });

            await _context.SaveChangesAsync();

            var supervisorLabel = string.IsNullOrWhiteSpace(user.FullName) ? user.UserName ?? "Supervisor" : user.FullName;

            // Push real-time message via SignalR
            await _signalHubContext.Clients.Group(recipientId).SendAsync("ReceiveMessage", user.Id, supervisorLabel, content.Trim(), DateTime.UtcNow.ToString("MMM dd • HH:mm"));

            var studentSubmission = await _context.ResearchSubmissions
                .Where(p => p.LinkedSupervisorId == user.Id && p.SubmitterId == recipientId && p.Status != SubmissionStatus.Rejected)
                .OrderByDescending(p => p.SubmissionDate)
                .FirstOrDefaultAsync();

            if (studentSubmission is not null)
            {
                await CreateNotificationAsync(
                    recipientId: recipientId,
                    senderId: user.Id,
                    type: AlertCategory.Message,
                    title: "New message from your supervisor",
                    message: content.Trim(),
                    actionUrl: Url.Action("Messages", "Student", new { contactId = user.Id }));
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true, senderName = supervisorLabel, content = content.Trim(), sentAt = DateTime.UtcNow.ToString("MMM dd • HH:mm") });
            }

            return RedirectToAction(nameof(Messages), new { contactId = recipientId });
        }

        public async Task<IActionResult> AvailableProjects()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            // Get ranked submissions with linking scores
            var rankedSubmissions = await _linkingService.GetRankedSubmissionsForSupervisorAsync(user.Id);

            var interestBySubmission = await _context.LinkRequests
                .GroupBy(mr => mr.SubmissionId)
                .Select(g => new { SubmissionId = g.Key, Count = g.Count() })
                .ToListAsync();

            var interestDict = interestBySubmission.ToDictionary(x => x.SubmissionId, x => x.Count);

            var submissions = new List<SubmissionLinkViewModel>();
            foreach (var (submission, linkScore) in rankedSubmissions)
            {
                var interestCount = interestDict.ContainsKey(submission.Id) ? interestDict[submission.Id] : 0;
                var hasInterest = await _context.LinkRequests.AnyAsync(mr =>
                    mr.SubmissionId == submission.Id && mr.SupervisorId == user.Id);

                submissions.Add(new SubmissionLinkViewModel
                {
                    SubmissionId = submission.Id,
                    AnonymousId = $"PRJ-{submission.Id:D4}",
                    Title = submission.Title ?? string.Empty,
                    ExpertiseDomainName = submission.ExpertiseDomain?.Name ?? "N/A",
                    SubmissionDate = submission.SubmissionDate,
                    ExecutiveSummary = submission.ExecutiveSummary ?? string.Empty,
                    TechStack = submission.TechStack ?? string.Empty,
                    Keywords = submission.Keywords ?? string.Empty,
                    LinkScore = linkScore,
                    InterestCount = interestCount,
                    HasExpressedInterest = hasInterest
                });
            }

            var submissionsByAreaData = await _context.ResearchSubmissions
                .Include(p => p.ExpertiseDomain)
                .Where(p => p.Status == SubmissionStatus.Pending)
                .GroupBy(p => p.ExpertiseDomain.Name)
                .Select(g => new { g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            var submissionsByArea = submissionsByAreaData
                .Select(x => (x.Key, x.Count))
                .ToList();

            var viewModel = new AvailableProjectsViewModel
            {
                Submissions = submissions,
                TotalSubmissionCount = await _context.ResearchSubmissions
                    .CountAsync(p => p.Status == SubmissionStatus.Pending),
                LinkedSubmissionCount = await _context.ResearchSubmissions
                    .CountAsync(p => p.LinkedSupervisorId == user.Id && p.Status == SubmissionStatus.Linked),
                SubmissionsByArea = submissionsByArea
            };

            return View(viewModel);
        }

        public async Task<IActionResult> ReviewProject(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var submission = await _context.ResearchSubmissions
                .Include(p => p.ExpertiseDomain)
                .Include(p => p.Submitter)
                .Include(p => p.GroupMembers)
                .Include(p => p.LinkRequests)
                    .ThenInclude(r => r.Supervisor)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (submission == null)
            {
                return NotFound();
            }

            var isInSupervisorScope = submission.Status == SubmissionStatus.Pending || submission.LinkedSupervisorId == user.Id;
            if (!isInSupervisorScope)
            {
                return Forbid();
            }

            // Calculate accurate link score
            var linkScore = await _linkingService.CalculateLinkScoreAsync(submission, user);
            var interestCount = submission.LinkRequests.Count;
            var hasExpressedInterest = submission.LinkRequests.Any(r => r.SupervisorId == user.Id);

            ViewBag.LinkScore = linkScore;
            ViewBag.InterestCount = interestCount;
            ViewBag.HasExpressedInterest = hasExpressedInterest;

            return View(submission);
        }

        private async Task CreateNotificationAsync(string recipientId, string? senderId, AlertCategory type, string title, string message, string? actionUrl)
        {
            await _notificationService.SendNotificationAsync(recipientId, title, message, type, actionUrl, senderId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveSubmission(int submissionId, string feedback = "")
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var submission = await _context.ResearchSubmissions
                .Include(p => p.Submitter)
                .FirstOrDefaultAsync(p => p.Id == submissionId && p.LinkedSupervisorId == user.Id);

            if (submission == null) return NotFound();

            submission.Status = SubmissionStatus.Approved;
            submission.ApprovedDate = DateTime.UtcNow;
            submission.SupervisorFeedback = feedback;
            submission.FeedbackDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await BroadcastLinkingUpdateAsync("submission-approved");

            await CreateNotificationAsync(
                recipientId: submission.SubmitterId,
                senderId: user.Id,
                type: AlertCategory.Submission,
                title: "Your submission was approved",
                message: $"Your submission '{submission.Title}' has been approved.",
                actionUrl: Url.Action("SubmissionDetails", "Student", new { id = submission.Id }));

            TempData["Success"] = "Submission approved successfully";
            return RedirectToAction(nameof(LinkedProjects));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectSubmission(int submissionId, string feedback = "")
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var submission = await _context.ResearchSubmissions
                .Include(p => p.Submitter)
                .FirstOrDefaultAsync(p => p.Id == submissionId && p.LinkedSupervisorId == user.Id);

            if (submission == null) return NotFound();

            submission.Status = SubmissionStatus.Rejected;
            submission.SupervisorFeedback = feedback;
            submission.FeedbackDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await BroadcastLinkingUpdateAsync("submission-rejected");

            await CreateNotificationAsync(
                recipientId: submission.SubmitterId,
                senderId: user.Id,
                type: AlertCategory.Submission,
                title: "Your submission was rejected",
                message: $"Your submission '{submission.Title}' has been rejected. Please review feedback.",
                actionUrl: Url.Action("SubmissionDetails", "Student", new { id = submission.Id }));

            TempData["Success"] = "Submission rejected";
            return RedirectToAction(nameof(LinkedProjects));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestChanges(int submissionId, string feedback = "")
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var submission = await _context.ResearchSubmissions
                .Include(p => p.Submitter)
                .FirstOrDefaultAsync(p => p.Id == submissionId && p.LinkedSupervisorId == user.Id);

            if (submission == null) return NotFound();

            if (string.IsNullOrWhiteSpace(feedback))
            {
                TempData["Error"] = "Please provide feedback for requested changes";
                return RedirectToAction(nameof(ReviewProject), new { id = submissionId });
            }

            submission.Status = SubmissionStatus.ChangesRequested;
            submission.SupervisorFeedback = feedback;
            submission.FeedbackDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await BroadcastLinkingUpdateAsync("changes-requested");

            await CreateNotificationAsync(
                recipientId: submission.SubmitterId,
                senderId: user.Id,
                type: AlertCategory.Submission,
                title: "Changes requested for your submission",
                message: $"Your supervisor requested changes to '{submission.Title}'. Review the feedback.",
                actionUrl: Url.Action("SubmissionDetails", "Student", new { id = submission.Id }));

            TempData["Success"] = "Changes requested from student";
            return RedirectToAction(nameof(ReviewProject), new { id = submissionId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkInProgress(int submissionId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var submission = await _context.ResearchSubmissions
                .Include(p => p.Submitter)
                .FirstOrDefaultAsync(p => p.Id == submissionId && p.LinkedSupervisorId == user.Id);

            if (submission == null) return NotFound();

            submission.Status = SubmissionStatus.InProgress;

            await _context.SaveChangesAsync();
            await BroadcastLinkingUpdateAsync("in-progress");

            await CreateNotificationAsync(
                recipientId: submission.SubmitterId,
                senderId: user.Id,
                type: AlertCategory.Submission,
                title: "Project marked as in progress",
                message: $"'{submission.Title}' is now in progress.",
                actionUrl: Url.Action("SubmissionDetails", "Student", new { id = submission.Id }));

            TempData["Success"] = "Project marked as in progress";
            return RedirectToAction(nameof(LinkedProjects));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkCompleted(int submissionId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var submission = await _context.ResearchSubmissions
                .Include(p => p.Submitter)
                .FirstOrDefaultAsync(p => p.Id == submissionId && p.LinkedSupervisorId == user.Id);

            if (submission == null) return NotFound();

            submission.Status = SubmissionStatus.Completed;
            submission.CompletedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await BroadcastLinkingUpdateAsync("completed");

            await CreateNotificationAsync(
                recipientId: submission.SubmitterId,
                senderId: user.Id,
                type: AlertCategory.Submission,
                title: "Project completed",
                message: $"'{submission.Title}' has been marked as completed.",
                actionUrl: Url.Action("SubmissionDetails", "Student", new { id = submission.Id }));

            TempData["Success"] = "Project marked as completed";
            return RedirectToAction(nameof(LinkedProjects));
        }

        [HttpGet]
        public async Task<IActionResult> OpenNotification(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var notification = await _context.AppNotifications
                .FirstOrDefaultAsync(n => n.Id == id && n.RecipientId == user.Id);

            if (notification == null)
            {
                return NotFound();
            }

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            if (notification.Type == AlertCategory.Message && !string.IsNullOrWhiteSpace(notification.SenderId))
            {
                return RedirectToAction(nameof(Messages), new { contactId = notification.SenderId });
            }

            if (!string.IsNullOrWhiteSpace(notification.ActionUrl) && Url.IsLocalUrl(notification.ActionUrl))
            {
                return LocalRedirect(notification.ActionUrl);
            }

            return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllNotificationsRead()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var unreadNotifications = await _context.AppNotifications
                .Where(n => n.RecipientId == user.Id && !n.IsRead)
                .ToListAsync();

            if (unreadNotifications.Count > 0)
            {
                foreach (var notification in unreadNotifications)
                {
                    notification.IsRead = true;
                    notification.ReadAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                await BroadcastLinkingUpdateAsync("supervisor-notifications-read");
            }

            return RedirectToAction(nameof(Dashboard));
        }

        private static IQueryable<ResearchSubmission> ApplyProgressFilter(IQueryable<ResearchSubmission> query, string progress)
        {
            return progress switch
            {
                "active" => query.Where(p =>
                    p.Status == SubmissionStatus.Linked ||
                    p.Status == SubmissionStatus.Approved ||
                    p.Status == SubmissionStatus.InProgress),
                "attention" => query.Where(p => p.Status == SubmissionStatus.ChangesRequested),
                "completed" => query.Where(p => p.Status == SubmissionStatus.Completed),
                "closed" => query.Where(p => p.Status == SubmissionStatus.Rejected),
                _ => query
            };
        }

        private static (string Label, string ClassName) GetStatusPresentation(SubmissionStatus status)
        {
            return status switch
            {
                SubmissionStatus.Linked => ("Linked", "bg-emerald-100 text-emerald-700"),
                SubmissionStatus.Approved => ("Approved", "bg-blue-100 text-blue-700"),
                SubmissionStatus.ChangesRequested => ("Changes Requested", "bg-amber-100 text-amber-700"),
                SubmissionStatus.InProgress => ("In Progress", "bg-indigo-100 text-indigo-700"),
                SubmissionStatus.Completed => ("Completed", "bg-teal-100 text-teal-700"),
                SubmissionStatus.Rejected => ("Rejected", "bg-rose-100 text-rose-700"),
                _ => (status.ToString(), "bg-slate-100 text-slate-700")
            };
        }

        private static (int Percent, string Label) GetProgressPresentation(SubmissionStatus status)
        {
            return status switch
            {
                SubmissionStatus.Linked => (30, "Initiated"),
                SubmissionStatus.Approved => (55, "Approved"),
                SubmissionStatus.ChangesRequested => (45, "Action Needed"),
                SubmissionStatus.InProgress => (80, "Execution"),
                SubmissionStatus.Completed => (100, "Completed"),
                SubmissionStatus.Rejected => (100, "Closed"),
                _ => (20, "Open")
            };
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var model = new ProfileViewModel
            {
                FullName = user.FullName,
                Email = user.Email ?? "",
                PhoneNumber = user.PhoneNumber,
                Biography = user.Biography,
                MaxSupervisionCapacity = user.MaxSupervisionCapacity,
                UserId = user.Id,
                UserName = user.UserName ?? "",
                Initials = user.FullName?.Substring(0, 1).ToUpper() ?? "S",
                IsSupervisor = true,
                RoleLabel = "Supervisor"
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (!ModelState.IsValid) return View(model);

            user.FullName = model.FullName;
            user.PhoneNumber = model.PhoneNumber;
            user.Biography = model.Biography;
            user.MaxSupervisionCapacity = model.MaxSupervisionCapacity;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Faculty profile synchronized successfully.";
                return RedirectToAction(nameof(Profile));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }

        private async Task BroadcastLinkingUpdateAsync(string reason)
        {
            if (_hubContext == null)
            {
                return;
            }

            await _hubContext.Clients.All.SendAsync("LinkingDataChanged", reason, DateTime.UtcNow);
        }
    }
}
