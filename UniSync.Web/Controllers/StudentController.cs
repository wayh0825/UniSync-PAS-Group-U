using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniSync.Core.Data;
using UniSync.Core.Entities;
using UniSync.Core.Enums;
using UniSync.Web.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UniSync.Web.Hubs;

namespace UniSync.Web.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly Services.INotificationService _notificationService;
        private readonly IHubContext<SignalHub> _hubContext;

        public StudentController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, Services.INotificationService notificationService, IHubContext<SignalHub> hubContext)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
            _hubContext = hubContext;
        }

        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            var submissions = await _context.ResearchSubmissions
                .Include(p => p.ExpertiseDomain)
                .Include(p => p.LinkedSupervisor)
                .Include(p => p.GroupMembers)
                .Where(p => p.SubmitterId == user.Id || p.GroupMembers.Any(m => m.UserId == user.Id && m.Status == InvitationStatus.Accepted))
                .Where(p => p.Status != SubmissionStatus.Draft)
                .ToListAsync();

            var latestSubmission = submissions
                .OrderByDescending(p => p.SubmissionDate)
                .FirstOrDefault();

            var totalSteps = 3;
            var completedSteps = latestSubmission?.Status switch
            {
                SubmissionStatus.Linked => 3,
                SubmissionStatus.UnderReview => 2,
                SubmissionStatus.Pending => 1,
                SubmissionStatus.Draft => 0,
                _ => 0
            };

            ViewBag.DisplayName = string.IsNullOrWhiteSpace(user.FullName) ? user.UserName : user.FullName;
            ViewBag.HasSubmission = latestSubmission != null;
            ViewBag.ProgressPercent = (int)System.Math.Round((double)completedSteps / totalSteps * 100);
            ViewBag.CurrentThesis = latestSubmission?.Title ?? "No submission submitted yet";
            ViewBag.NextMilestone = latestSubmission?.Status switch
            {
                SubmissionStatus.Pending => "REVIEW PHASE",
                SubmissionStatus.UnderReview => "MATCHING DECISION",
                SubmissionStatus.Linked => "FINALIZED",
                _ => "SUBMIT PROPOSAL"
            };
            ViewBag.AdvisorName = latestSubmission?.LinkedSupervisor?.FullName ?? "Not Assigned";
            ViewBag.AdvisorArea = latestSubmission?.ExpertiseDomain?.Name ?? "General Research";
            ViewBag.TotalSubmissions = submissions.Count;
            ViewBag.PendingCount = submissions.Count(p => p.Status == SubmissionStatus.Pending);
            ViewBag.UnderReviewCount = submissions.Count(p => p.Status == SubmissionStatus.UnderReview);
            ViewBag.LinkedCount = submissions.Count(p => p.Status == SubmissionStatus.Linked);
            ViewBag.LastSubmission = latestSubmission?.SubmissionDate.ToLocalTime().ToString("MMM dd, yyyy");

            return View(submissions);
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboardMetrics()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Unauthorized();

            var submissions = await _context.ResearchSubmissions
                .Include(p => p.ExpertiseDomain)
                .Include(p => p.LinkedSupervisor)
                .Include(p => p.GroupMembers)
                .Where(p => p.SubmitterId == user.Id || p.GroupMembers.Any(m => m.UserId == user.Id && m.Status == InvitationStatus.Accepted))
                .Where(p => p.Status != SubmissionStatus.Draft)
                .ToListAsync();

            var latestSubmission = submissions.OrderByDescending(p => p.SubmissionDate).FirstOrDefault();

            var totalSteps = 3;
            var completedSteps = latestSubmission?.Status switch
            {
                SubmissionStatus.Linked => 3,
                SubmissionStatus.UnderReview => 2,
                SubmissionStatus.Pending => 1,
                _ => 0
            };

            var milestoneLabel = latestSubmission?.Status switch
            {
                SubmissionStatus.Pending => "REVIEW PHASE",
                SubmissionStatus.UnderReview => "MATCHING DECISION",
                SubmissionStatus.Linked => "FINALIZED",
                _ => "SUBMIT PROPOSAL"
            };

            return Json(new
            {
                progress = (int)System.Math.Round((double)completedSteps / totalSteps * 100),
                currentThesis = latestSubmission?.Title ?? "No submission active",
                nextMilestone = milestoneLabel,
                advisorName = latestSubmission?.LinkedSupervisor?.FullName ?? "UNLINKED",
                advisorArea = latestSubmission?.ExpertiseDomain?.Name ?? "Awaiting Allocation",
                totalSubmissions = submissions.Count,
                linkedCount = submissions.Count(p => p.Status == SubmissionStatus.Linked),
                lastSubmission = latestSubmission?.SubmissionDate.ToLocalTime().ToString("MMM dd, yyyy")
            });
        }

        [HttpGet]
        public async Task<IActionResult> CreateSubmission(int? draftId, bool fromGuidelines = false)
        {
            if (!fromGuidelines)
            {
                return RedirectToAction(nameof(Guidelines), new { draftId });
            }

            ViewBag.ExpertiseDomains = await _context.ExpertiseDomains.ToListAsync();

            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            SubmissionSubmissionViewModel model;

            if (draftId.HasValue)
            {
                var draft = await _context.DraftSubmissions
                    .FirstOrDefaultAsync(d => d.Id == draftId.Value && d.StudentId == user.Id);

                if (draft is null)
                {
                    return NotFound();
                }

                model = MapDraftToViewModel(draft);
            }
            else
            {
                var latestDraft = await _context.DraftSubmissions
                    .Where(d => d.StudentId == user.Id)
                    .OrderByDescending(d => d.UpdatedAt)
                    .FirstOrDefaultAsync();

                model = latestDraft is null
                    ? new SubmissionSubmissionViewModel()
                    : MapDraftToViewModel(latestDraft);
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult Guidelines(int? draftId)
        {
            ViewBag.DraftId = draftId;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSubmission(SubmissionSubmissionViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            NormalizeModel(model);
            await ValidateForSubmission(model, user);

            if (!ModelState.IsValid)
            {
                ViewBag.ExpertiseDomains = await _context.ExpertiseDomains.ToListAsync();
                return View(model);
            }

            var submission = new ResearchSubmission
            {
                Title = model.Title ?? string.Empty,
                ExpertiseDomainId = model.ExpertiseDomainId ?? 0,
                ExecutiveSummary = model.ExecutiveSummary ?? string.Empty,
                ProblemStatement = model.ProblemStatement,
                Objectives = model.Objectives,
                Methodology = model.Methodology,
                ExpectedOutcomes = model.ExpectedOutcomes,
                TechStack = model.TechStack ?? string.Empty,
                Keywords = model.Keywords,
                EthicsConsiderations = model.EthicsConsiderations,
                TimelineWeeks = model.TimelineWeeks,
                ReferencesText = model.ReferencesText,
                SubmitterId = user.Id,
                Status = SubmissionStatus.Pending,
                SubmissionDate = DateTime.UtcNow,
                LinkedSupervisorId = null,
                IsGroupProject = model.IsGroupProject,
                GroupMembers = model.IsGroupProject && model.GroupMembers != null
                    ? model.GroupMembers.Select(m => new SubmissionGroupMember
                    {
                        StudentIdIdentifier = m.StudentIdIdentifier,
                        FullName = m.FullName,
                        Email = m.Email
                    }).ToList()
                    : new List<SubmissionGroupMember>()
            };

            _context.ResearchSubmissions.Add(submission);

            if (model.DraftId.HasValue)
            {
                var draft = await _context.DraftSubmissions
                    .FirstOrDefaultAsync(d => d.Id == model.DraftId.Value && d.StudentId == user.Id);
                if (draft is not null)
                {
                    _context.DraftSubmissions.Remove(draft);
                }
            }

            await _context.SaveChangesAsync();

            await CreateNotificationAsync(
                recipientId: user.Id,
                senderId: null,
                type: AlertCategory.Submission,
                title: "Submission submitted",
                message: $"Your submission '{submission.Title}' has been submitted and is now in the review queue.",
                actionUrl: Url.Action(nameof(SubmissionDetails), new { id = submission.Id }));

            var modLeaders = await _userManager.GetUsersInRoleAsync("ModuleLeader");
            var admins = await _userManager.GetUsersInRoleAsync("Administrator");
            var staffToNotify = modLeaders.Concat(admins).Select(u => u.Id).Distinct();

            foreach (var staffId in staffToNotify)
            {
                await CreateNotificationAsync(
                    recipientId: staffId,
                    senderId: user.Id,
                    type: AlertCategory.Submission,
                    title: "New Student Submission",
                    message: $"A new submission '{submission.Title}' has been submitted by {user.FullName ?? user.Email}.",
                    actionUrl: Url.Action("ManageSubmissionEntry", "Admin", new { id = submission.Id }));
            }

            TempData["SuccessMessage"] = "Submission submitted successfully.";
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveDraft([FromBody] SubmissionSubmissionViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Unauthorized();
            }

            NormalizeModel(model);

            var draft = await GetOrCreateDraft(user.Id, model.DraftId);
            draft.Title = model.Title;
            draft.ExpertiseDomainId = model.ExpertiseDomainId;
            draft.ExecutiveSummary = model.ExecutiveSummary;
            draft.ProblemStatement = model.ProblemStatement;
            draft.Objectives = model.Objectives;
            draft.Methodology = model.Methodology;
            draft.ExpectedOutcomes = model.ExpectedOutcomes;
            draft.TechStack = model.TechStack;
            draft.Keywords = model.Keywords;
            draft.EthicsConsiderations = model.EthicsConsiderations;
            draft.TimelineWeeks = model.TimelineWeeks;
            draft.ReferencesText = model.ReferencesText;
            draft.UpdatedAt = DateTime.UtcNow;
            draft.IsGroupProject = model.IsGroupProject;
            if (model.IsGroupProject && model.GroupMembers != null)
            {
                draft.GroupMembersText = System.Text.Json.JsonSerializer.Serialize(model.GroupMembers);
            }
            else
            {
                draft.GroupMembersText = null;
            }

            if (draft.Id == 0)
            {
                _context.DraftSubmissions.Add(draft);
            }

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                draftId = draft.Id,
                savedAt = draft.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckBlindRule([FromBody] SubmissionSubmissionViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Unauthorized();
            }

            var abstractText = model.ExecutiveSummary?.Trim() ?? string.Empty;
            var valid = !ContainsPersonalIdentifiers(abstractText, user, model);

            return Json(new
            {
                isValid = valid,
                message = valid
                    ? "Blind rule check passed."
                    : "Personal identifiers detected in abstract. Remove name/email/username details."
            });
        }
        public async Task<IActionResult> MySubmissions(int? trackId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            var submissions = await _context.ResearchSubmissions
                .Include(p => p.ExpertiseDomain)
                .Include(p => p.LinkedSupervisor)
                .Include(p => p.LinkRequests)
                .Include(p => p.GroupMembers)
                .Where(p => p.SubmitterId == user.Id || p.GroupMembers.Any(m => m.UserId == user.Id && m.Status == InvitationStatus.Accepted))
                .Where(p => p.Status != SubmissionStatus.Draft)
                .OrderByDescending(p => p.SubmissionDate)
                .ToListAsync();

            var selectedSubmission = trackId.HasValue
                ? submissions.FirstOrDefault(p => p.Id == trackId.Value)
                : submissions.FirstOrDefault();

            var reviewStartedAt = selectedSubmission?.LinkRequests
                .OrderBy(m => m.ExpressedAt)
                .Select(m => (DateTime?)m.ExpressedAt)
                .FirstOrDefault();

            var linkedAt = selectedSubmission?.Status == SubmissionStatus.Linked
                ? selectedSubmission.LinkRequests
                    .Where(m => m.SupervisorId == selectedSubmission.LinkedSupervisorId)
                    .OrderByDescending(m => m.ExpressedAt)
                    .Select(m => (DateTime?)m.ExpressedAt)
                    .FirstOrDefault()
                : null;

            var viewModel = new StudentSubmissionsTrackerViewModel
            {
                Submissions = submissions,
                SelectedSubmission = selectedSubmission,
                PendingCount = submissions.Count(p => p.Status == SubmissionStatus.Pending),
                ReviewCount = submissions.Count(p => p.Status == SubmissionStatus.UnderReview),
                LinkedCount = submissions.Count(p => p.Status == SubmissionStatus.Linked)
            };

            if (selectedSubmission is null)
            {
                return View(viewModel);
            }

            viewModel.SubmittedDateLabel = selectedSubmission.SubmissionDate.ToLocalTime().ToString("MMM dd, hh:mm tt");
            viewModel.ReviewDateLabel = reviewStartedAt?.ToLocalTime().ToString("MMM dd, hh:mm tt") ?? "Pending";
            viewModel.BoardDateLabel = selectedSubmission.Status == SubmissionStatus.Linked ? "Completed" : "Pending";
            viewModel.FinalDateLabel = linkedAt?.ToLocalTime().ToString("MMM dd, hh:mm tt") ?? (selectedSubmission.Status == SubmissionStatus.Linked ? "Completed" : "Pending");

            viewModel.IsSubmittedCompleted = true;
            viewModel.IsReviewCompleted = selectedSubmission.Status == SubmissionStatus.UnderReview || selectedSubmission.Status == SubmissionStatus.Linked;
            viewModel.IsReviewActive = selectedSubmission.Status == SubmissionStatus.Pending || selectedSubmission.Status == SubmissionStatus.UnderReview;
            viewModel.IsBoardCompleted = selectedSubmission.Status == SubmissionStatus.Linked;
            viewModel.IsFinalCompleted = selectedSubmission.Status == SubmissionStatus.Linked;

            switch (selectedSubmission.Status)
            {
                case SubmissionStatus.Pending:
                    viewModel.ProgressPercent = 25;
                    viewModel.ReviewLabel = "Pending Queue";
                    viewModel.ReviewState = "Waiting for reviewer assignment";
                    break;
                case SubmissionStatus.UnderReview:
                    viewModel.ProgressPercent = 50;
                    viewModel.ReviewLabel = "Review in Progress";
                    viewModel.ReviewState = "Active State";
                    break;
                case SubmissionStatus.Linked:
                    viewModel.ProgressPercent = 100;
                    viewModel.ReviewLabel = "Review Completed";
                    viewModel.ReviewState = "Completed";
                    break;
                default:
                    viewModel.ProgressPercent = 0;
                    viewModel.ReviewLabel = "No Submission";
                    viewModel.ReviewState = "No Submission";
                    break;
            }

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> SubmissionDetails(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            var submission = await _context.ResearchSubmissions
                .Include(p => p.Submitter)
                .Include(p => p.ExpertiseDomain)
                .Include(p => p.LinkedSupervisor)
                .Include(p => p.LinkRequests)
                .Include(p => p.GroupMembers)
                .FirstOrDefaultAsync(p => p.Id == id && p.SubmitterId == user.Id && p.Status != SubmissionStatus.Draft);

            if (submission is null)
            {
                return NotFound();
            }

            var trackerViewModel = new StudentSubmissionsTrackerViewModel
            {
                SelectedSubmission = submission
            };

            PopulateTrackerState(trackerViewModel, submission);
            return View(trackerViewModel);
        }

        [Authorize(Roles = "Student,Supervisor,Administrator")]
        public async Task<IActionResult> Messages(string? contactId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            var contacts = await _context.ResearchSubmissions
                .Include(p => p.LinkedSupervisor)
                .Include(p => p.GroupMembers)
                .Where(p => (p.SubmitterId == user.Id || p.GroupMembers.Any(m => m.UserId == user.Id && m.Status == InvitationStatus.Accepted)) && p.LinkedSupervisor != null)
                .Select(p => new
                {
                    UserId = p.LinkedSupervisorId,
                    FullName = p.LinkedSupervisor != null ? p.LinkedSupervisor.FullName : ""
                })
                .Where(x => x.UserId != null && !string.IsNullOrWhiteSpace(x.FullName))
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
            foreach (var contact in contacts.Where(c => c.UserId != null))
            {
                var cid = contact.UserId!;
                var lastSentAt = await _context.ChatMessages
                    .Where(m =>
                        (m.SenderId == user.Id && m.RecipientId == cid) ||
                        (m.SenderId == cid && m.RecipientId == user.Id))
                    .OrderByDescending(m => m.SentAt)
                    .Select(m => (DateTime?)m.SentAt)
                    .FirstOrDefaultAsync();

                var hasUnread = await _context.ChatMessages.AnyAsync(m =>
                    m.SenderId == cid &&
                    m.RecipientId == user.Id &&
                    !m.IsRead);

                contactSummaries.Add(new MessageContactViewModel
                {
                    UserId = cid,
                    FullName = contact.FullName,
                    RoleLabel = "Supervisor",
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
                CurrentUserName = string.IsNullOrWhiteSpace(user.FullName) ? user.UserName ?? "Student" : user.FullName,
                CurrentUserRoleLabel = "Student",
                ActiveContactId = activeContactId,
                ActiveContactName = contactSummaries.FirstOrDefault(c => c.UserId == activeContactId)?.FullName,
                Contacts = contactSummaries,
                Messages = threadMessages,
                EmptyStateMessage = contactSummaries.Count == 0
                    ? "No linked supervisors found yet. Messages become available after linking."
                    : "No messages in this conversation yet."
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(string recipientId, string content)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            if (string.IsNullOrWhiteSpace(recipientId) || string.IsNullOrWhiteSpace(content))
            {
                return RedirectToAction(nameof(Messages), new { contactId = recipientId });
            }

            var canMessageRecipient = await _context.ResearchSubmissions
                .AnyAsync(p =>
                (p.SubmitterId == user.Id || p.GroupMembers.Any(m => m.UserId == user.Id && m.Status == InvitationStatus.Accepted)) &&
                p.LinkedSupervisorId == recipientId);

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

            var senderLabel = string.IsNullOrWhiteSpace(user.FullName) ? user.UserName ?? "Student" : user.FullName;
            
            // Push real-time message via SignalR
            await _hubContext.Clients.Group(recipientId).SendAsync("ReceiveMessage", user.Id, senderLabel, content.Trim(), DateTime.UtcNow.ToString("MMM dd • HH:mm"));

            var linkedSubmission = await _context.ResearchSubmissions
                .Where(p => (p.SubmitterId == user.Id || p.GroupMembers.Any(m => m.UserId == user.Id && m.Status == InvitationStatus.Accepted)) && 
                            p.LinkedSupervisorId == recipientId && p.Status == SubmissionStatus.Linked)
                .OrderByDescending(p => p.SubmissionDate)
                .FirstOrDefaultAsync();

            if (linkedSubmission is not null)
            {
                await CreateNotificationAsync(
                    recipientId: recipientId,
                    senderId: user.Id,
                    type: AlertCategory.Message,
                    title: $"New message from {senderLabel}",
                    message: content.Trim(),
                    actionUrl: Url.Action("Messages", "Supervisor", new { contactId = user.Id }));
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true, senderName = senderLabel, content = content.Trim(), sentAt = DateTime.UtcNow.ToString("MMM dd • HH:mm") });
            }

            return RedirectToAction(nameof(Messages), new { contactId = recipientId });
        }

        [HttpGet]
        public async Task<IActionResult> Notifications(string filter = "all", string? search = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            var normalizedFilter = string.IsNullOrWhiteSpace(filter) ? "all" : filter.Trim().ToLowerInvariant();
            var searchTerm = search?.Trim() ?? string.Empty;

            var query = _context.AppNotifications
                .Include(n => n.Sender)
                .Where(n => n.RecipientId == user.Id)
                .OrderByDescending(n => n.CreatedAt)
                .AsQueryable();

            if (normalizedFilter == "unread")
            {
                query = query.Where(n => !n.IsRead);
            }
            else if (normalizedFilter == "read")
            {
                query = query.Where(n => n.IsRead);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(n => n.Title.Contains(searchTerm) || n.Message.Contains(searchTerm));
            }

            var notifications = await query.ToListAsync();

            var pendingInvitations = await _context.SubmissionGroupMembers
                .Include(m => m.ResearchSubmission)
                .Where(m => m.UserId == user.Id && m.Status == InvitationStatus.Pending)
                .ToListAsync();

            var model = new NotificationsPageViewModel
            {
                PendingInvitations = pendingInvitations,
                Filter = normalizedFilter,
                Search = searchTerm,
                TotalCount = await _context.AppNotifications.CountAsync(n => n.RecipientId == user.Id),
                UnreadCount = await _context.AppNotifications.CountAsync(n => n.RecipientId == user.Id && !n.IsRead),
                TodayCount = await _context.AppNotifications.CountAsync(n => n.RecipientId == user.Id && n.CreatedAt >= DateTime.UtcNow.Date),
                Notifications = notifications.Select(n => new NotificationItemViewModel
                {
                    Id = n.Id,
                    TypeLabel = n.Type.ToString(),
                    Icon = n.Type switch
                    {
                        AlertCategory.Submission => "description",
                        AlertCategory.Link => "verified",
                        AlertCategory.Message => "chat_bubble",
                        _ => "notifications"
                    },
                    IconClass = n.Type switch
                    {
                        AlertCategory.Submission => "bg-blue-50 text-blue-600",
                        AlertCategory.Link => "bg-emerald-50 text-emerald-600",
                        AlertCategory.Message => "bg-violet-50 text-violet-600",
                        _ => "bg-slate-50 text-slate-600"
                    },
                    Title = n.Title,
                    Message = n.Message,
                    ActionUrl = n.ActionUrl,
                    ActionLabel = string.IsNullOrWhiteSpace(n.ActionUrl) ? "Open" : "View",
                    TimeLabel = GetRelativeTime(n.CreatedAt),
                    IsRead = n.IsRead
                }).ToList()
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> OpenNotification(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            var notification = await _context.AppNotifications
                .FirstOrDefaultAsync(n => n.Id == id && n.RecipientId == user.Id);

            if (notification is null)
            {
                return NotFound();
            }

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

            return RedirectToAction(nameof(Notifications));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllNotificationsRead()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
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
            }

            return RedirectToAction(nameof(Notifications));
        }

        private static bool ContainsPersonalIdentifiers(string abstractText, ApplicationUser user, SubmissionSubmissionViewModel? model = null)
        {
            if (string.IsNullOrWhiteSpace(abstractText))
            {
                return false;
            }

            var candidates = new List<string>();

            if (!string.IsNullOrWhiteSpace(user.FullName))
            {
                candidates.Add(user.FullName);
                candidates.AddRange(
                    user.FullName
                        .Split(new[] { ' ', '.', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(part => part.Length >= 3));
            }

            if (!string.IsNullOrWhiteSpace(user.UserName))
            {
                candidates.Add(user.UserName);
            }

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                candidates.Add(user.Email);
            }

            if (model != null && model.IsGroupProject && model.GroupMembers != null)
            {
                foreach(var m in model.GroupMembers)
                {
                    if (!string.IsNullOrWhiteSpace(m.FullName)) {
                         candidates.Add(m.FullName);
                         candidates.AddRange(m.FullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Where(p => p.Length >= 3));
                    }
                    if (!string.IsNullOrWhiteSpace(m.Email)) candidates.Add(m.Email);
                }
            }

            return candidates
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Any(token => abstractText.Contains(token, StringComparison.OrdinalIgnoreCase));
        }

        private static SubmissionSubmissionViewModel MapDraftToViewModel(DraftSubmission draft)
        {
            return new SubmissionSubmissionViewModel
            {
                DraftId = draft.Id,
                Title = draft.Title,
                ExpertiseDomainId = draft.ExpertiseDomainId,
                ExecutiveSummary = draft.ExecutiveSummary,
                ProblemStatement = draft.ProblemStatement,
                Objectives = draft.Objectives,
                Methodology = draft.Methodology,
                ExpectedOutcomes = draft.ExpectedOutcomes,
                TechStack = draft.TechStack,
                Keywords = draft.Keywords,
                EthicsConsiderations = draft.EthicsConsiderations,
                TimelineWeeks = draft.TimelineWeeks,
                ReferencesText = draft.ReferencesText,
                LastSavedAt = draft.UpdatedAt
            };
        }

        private static void PopulateTrackerState(StudentSubmissionsTrackerViewModel viewModel, ResearchSubmission selectedSubmission)
        {
            var reviewStartedAt = selectedSubmission.LinkRequests
                .OrderBy(m => m.ExpressedAt)
                .Select(m => (DateTime?)m.ExpressedAt)
                .FirstOrDefault();

            var linkedAt = selectedSubmission.Status == SubmissionStatus.Linked
                ? selectedSubmission.LinkRequests
                    .Where(m => m.SupervisorId == selectedSubmission.LinkedSupervisorId)
                    .OrderByDescending(m => m.ExpressedAt)
                    .Select(m => (DateTime?)m.ExpressedAt)
                    .FirstOrDefault()
                : null;

            viewModel.SubmittedDateLabel = selectedSubmission.SubmissionDate.ToLocalTime().ToString("MMM dd, hh:mm tt");
            viewModel.ReviewDateLabel = reviewStartedAt?.ToLocalTime().ToString("MMM dd, hh:mm tt") ?? "Pending";
            viewModel.BoardDateLabel = selectedSubmission.Status == SubmissionStatus.Linked ? "Completed" : "Pending";
            viewModel.FinalDateLabel = linkedAt?.ToLocalTime().ToString("MMM dd, hh:mm tt") ?? (selectedSubmission.Status == SubmissionStatus.Linked ? "Completed" : "Pending");

            viewModel.IsSubmittedCompleted = true;
            viewModel.IsReviewCompleted = selectedSubmission.Status == SubmissionStatus.UnderReview || selectedSubmission.Status == SubmissionStatus.Linked;
            viewModel.IsReviewActive = selectedSubmission.Status == SubmissionStatus.Pending || selectedSubmission.Status == SubmissionStatus.UnderReview;
            viewModel.IsBoardCompleted = selectedSubmission.Status == SubmissionStatus.Linked;
            viewModel.IsFinalCompleted = selectedSubmission.Status == SubmissionStatus.Linked;

            switch (selectedSubmission.Status)
            {
                case SubmissionStatus.Pending:
                    viewModel.ProgressPercent = 25;
                    viewModel.ReviewLabel = "Pending Queue";
                    viewModel.ReviewState = "Waiting for reviewer assignment";
                    break;
                case SubmissionStatus.UnderReview:
                    viewModel.ProgressPercent = 50;
                    viewModel.ReviewLabel = "Review in Progress";
                    viewModel.ReviewState = "Active State";
                    break;
                case SubmissionStatus.Linked:
                    viewModel.ProgressPercent = 100;
                    viewModel.ReviewLabel = "Review Completed";
                    viewModel.ReviewState = "Completed";
                    break;
                default:
                    viewModel.ProgressPercent = 0;
                    viewModel.ReviewLabel = "No Submission";
                    viewModel.ReviewState = "No Submission";
                    break;
            }
        }

        private async Task CreateNotificationAsync(string recipientId, string? senderId, AlertCategory type, string title, string message, string? actionUrl)
        {
            await _notificationService.SendNotificationAsync(recipientId, title, message, type, actionUrl, senderId);
        }

        private static string GetRelativeTime(DateTime utcTime)
        {
            var timeSpan = DateTime.UtcNow - utcTime;

            if (timeSpan.TotalMinutes < 1)
            {
                return "Just now";
            }

            if (timeSpan.TotalHours < 1)
            {
                var minutes = (int)System.Math.Floor(timeSpan.TotalMinutes);
                return $"{minutes}m ago";
            }

            if (timeSpan.TotalDays < 1)
            {
                var hours = (int)System.Math.Floor(timeSpan.TotalHours);
                return $"{hours}h ago";
            }

            if (timeSpan.TotalDays < 7)
            {
                return $"{(int)System.Math.Floor(timeSpan.TotalDays)}d ago";
            }

            return utcTime.ToLocalTime().ToString("MMM dd");
        }

        private async Task<DraftSubmission> GetOrCreateDraft(string studentId, int? draftId)
        {
            if (draftId.HasValue)
            {
                var existingDraft = await _context.DraftSubmissions
                    .FirstOrDefaultAsync(d => d.Id == draftId.Value && d.StudentId == studentId);
                if (existingDraft is not null)
                {
                    return existingDraft;
                }
            }

            return new DraftSubmission { StudentId = studentId };
        }

        private static void NormalizeModel(SubmissionSubmissionViewModel model)
        {
            model.Title = model.Title?.Trim();
            model.ExecutiveSummary = model.ExecutiveSummary?.Trim();
            model.ProblemStatement = model.ProblemStatement?.Trim();
            model.Objectives = model.Objectives?.Trim();
            model.Methodology = model.Methodology?.Trim();
            model.ExpectedOutcomes = model.ExpectedOutcomes?.Trim();
            model.TechStack = model.TechStack?.Trim();
            model.Keywords = model.Keywords?.Trim();
            model.EthicsConsiderations = model.EthicsConsiderations?.Trim();
            model.ReferencesText = model.ReferencesText?.Trim();
        }

        private async Task ValidateForSubmission(SubmissionSubmissionViewModel model, ApplicationUser user)
        {
            if (string.IsNullOrWhiteSpace(model.Title))
            {
                ModelState.AddModelError(nameof(model.Title), "Title is required.");
            }

            if (!model.ExpertiseDomainId.HasValue)
            {
                ModelState.AddModelError(nameof(model.ExpertiseDomainId), "Research area is required.");
            }
            else
            {
                var areaExists = await _context.ExpertiseDomains.AnyAsync(r => r.Id == model.ExpertiseDomainId.Value);
                if (!areaExists)
                {
                    ModelState.AddModelError(nameof(model.ExpertiseDomainId), "Please select a valid research area.");
                }
            }

            if (string.IsNullOrWhiteSpace(model.ExecutiveSummary) || model.ExecutiveSummary.Length < 100)
            {
                ModelState.AddModelError(nameof(model.ExecutiveSummary), "ExecutiveSummary must be at least 100 characters.");
            }

            if (string.IsNullOrWhiteSpace(model.ProblemStatement))
            {
                ModelState.AddModelError(nameof(model.ProblemStatement), "Problem statement is required.");
            }

            if (string.IsNullOrWhiteSpace(model.Objectives))
            {
                ModelState.AddModelError(nameof(model.Objectives), "Objectives are required.");
            }

            if (string.IsNullOrWhiteSpace(model.Methodology))
            {
                ModelState.AddModelError(nameof(model.Methodology), "Methodology is required.");
            }

            if (string.IsNullOrWhiteSpace(model.ExpectedOutcomes))
            {
                ModelState.AddModelError(nameof(model.ExpectedOutcomes), "Expected outcomes are required.");
            }

            if (string.IsNullOrWhiteSpace(model.TechStack))
            {
                ModelState.AddModelError(nameof(model.TechStack), "Tech stack is required.");
            }

            if (!model.TimelineWeeks.HasValue || model.TimelineWeeks < 1)
            {
                ModelState.AddModelError(nameof(model.TimelineWeeks), "Timeline is required.");
            }

            if (!model.AgreeBlindRule)
            {
                ModelState.AddModelError(nameof(model.AgreeBlindRule), "You must confirm the Blind Rule declaration before submitting.");
            }

            if (model.IsGroupProject && model.GroupMembers != null)
            {
                for (int i = 0; i < model.GroupMembers.Count; i++)
                {
                    var m = model.GroupMembers[i];
                    if (string.IsNullOrWhiteSpace(m.FullName) || string.IsNullOrWhiteSpace(m.Email) || string.IsNullOrWhiteSpace(m.StudentIdIdentifier))
                    {
                        ModelState.AddModelError("", $"Group Member {i + 1} is missing details.");
                    }
                }
            }

            if (ContainsPersonalIdentifiers(model.ExecutiveSummary ?? string.Empty, user, model))
            {
                ModelState.AddModelError(nameof(model.ExecutiveSummary), "ExecutiveSummary must not include your personal details (name, username, or email).");
            }
        }
        [HttpGet]
        public async Task<IActionResult> EditSubmission(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var submission = await _context.ResearchSubmissions
                .Include(p => p.GroupMembers)
                .FirstOrDefaultAsync(p => p.Id == id && p.SubmitterId == user.Id);

            if (submission == null) return NotFound();
            if (submission.Status != SubmissionStatus.Pending)
            {
                TempData["Error"] = "Only pending submissions can be edited.";
                return RedirectToAction(nameof(Dashboard));
            }

            ViewBag.ExpertiseDomains = await _context.ExpertiseDomains.ToListAsync();

            var model = new SubmissionSubmissionViewModel
            {
                DraftId = submission.Id,
                Title = submission.Title,
                ExpertiseDomainId = submission.ExpertiseDomainId,
                ExecutiveSummary = submission.ExecutiveSummary,
                ProblemStatement = submission.ProblemStatement,
                Objectives = submission.Objectives,
                Methodology = submission.Methodology,
                ExpectedOutcomes = submission.ExpectedOutcomes,
                TechStack = submission.TechStack,
                Keywords = submission.Keywords,
                EthicsConsiderations = submission.EthicsConsiderations,
                TimelineWeeks = submission.TimelineWeeks,
                ReferencesText = submission.ReferencesText,
                AgreeBlindRule = true,
                IsGroupProject = submission.IsGroupProject,
                GroupMembers = submission.GroupMembers.Select(g => new GroupMemberViewModel
                {
                    Id = g.Id,
                    StudentIdIdentifier = g.StudentIdIdentifier,
                    FullName = g.FullName,
                    Email = g.Email
                }).ToList()
            };

            return View("CreateSubmission", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSubmission(int id, SubmissionSubmissionViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var submission = await _context.ResearchSubmissions
                .Include(p => p.GroupMembers)
                .FirstOrDefaultAsync(p => p.Id == id && p.SubmitterId == user.Id);

            if (submission == null) return NotFound();
            if (submission.Status != SubmissionStatus.Pending)
            {
                TempData["Error"] = "Only pending submissions can be edited.";
                return RedirectToAction(nameof(Dashboard));
            }

            NormalizeModel(model);
            await ValidateForSubmission(model, user);

            if (!ModelState.IsValid)
            {
                ViewBag.ExpertiseDomains = await _context.ExpertiseDomains.ToListAsync();
                return View("CreateSubmission", model);
            }

            submission.Title = model.Title ?? string.Empty;
            submission.ExpertiseDomainId = model.ExpertiseDomainId ?? 0;
            submission.ExecutiveSummary = model.ExecutiveSummary ?? string.Empty;
            submission.ProblemStatement = model.ProblemStatement;
            submission.Objectives = model.Objectives;
            submission.Methodology = model.Methodology;
            submission.ExpectedOutcomes = model.ExpectedOutcomes;
            submission.TechStack = model.TechStack ?? string.Empty;
            submission.Keywords = model.Keywords;
            submission.EthicsConsiderations = model.EthicsConsiderations;
            submission.TimelineWeeks = model.TimelineWeeks;
            submission.ReferencesText = model.ReferencesText;
            
            submission.IsGroupProject = model.IsGroupProject;
            
            _context.SubmissionGroupMembers.RemoveRange(submission.GroupMembers);
            
            if (model.IsGroupProject && model.GroupMembers != null)
            {
                submission.GroupMembers = model.GroupMembers.Select(m => new SubmissionGroupMember
                {
                    StudentIdIdentifier = m.StudentIdIdentifier,
                    FullName = m.FullName,
                    Email = m.Email
                }).ToList();
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Submission updated successfully.";
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> WithdrawSubmission(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var submission = await _context.ResearchSubmissions
                .FirstOrDefaultAsync(p => p.Id == id && p.SubmitterId == user.Id);

            if (submission == null) return NotFound();

            if (submission.Status != SubmissionStatus.Pending && submission.Status != SubmissionStatus.UnderReview)
            {
                TempData["Error"] = "This submission can no longer be withdrawn.";
                return RedirectToAction(nameof(Dashboard));
            }

            _context.ResearchSubmissions.Remove(submission);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Submission successfully withdrawn.";
            return RedirectToAction(nameof(Dashboard));
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InviteMember(int submissionId, string email, string fullName)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var submission = await _context.ResearchSubmissions
                .Include(p => p.GroupMembers)
                .FirstOrDefaultAsync(p => p.Id == submissionId && p.SubmitterId == user.Id);

            if (submission == null) return NotFound();

            if (submission.GroupMembers.Any(m => m.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
            {
                TempData["Error"] = "Subject is already part of the protocol.";
                return RedirectToAction("SubmissionDetails", new { id = submissionId });
            }

            var member = new SubmissionGroupMember
            {
                Email = email,
                FullName = fullName,
                ResearchSubmissionId = submissionId,
                Status = InvitationStatus.Pending,
                StudentIdIdentifier = "Pending"
            };

            var targetUser = await _userManager.FindByEmailAsync(email);
            if (targetUser != null)
            {
                member.UserId = targetUser.Id;
                member.StudentIdIdentifier = targetUser.UserName ?? "User";
                
                await _notificationService.SendNotificationAsync(targetUser.Id, "Protocol Invitation", $"Invitation to join: {submission.Title}", AlertCategory.Social, Url.Action("Notifications"));
            }

            _context.SubmissionGroupMembers.Add(member);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Uplink invitation dispatched.";
            return RedirectToAction("SubmissionDetails", new { id = submissionId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RespondToInvitation(int invitationId, bool accept)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var invitation = await _context.SubmissionGroupMembers
                .Include(m => m.ResearchSubmission)
                .FirstOrDefaultAsync(m => m.Id == invitationId && m.UserId == user.Id);

            if (invitation == null) return NotFound();

            if (accept)
            {
                invitation.Status = InvitationStatus.Accepted;
                if (invitation.ResearchSubmission != null)
                {
                    await _notificationService.SendNotificationAsync(invitation.ResearchSubmission.SubmitterId, 
                        "Member Joined", $"{user.FullName ?? "A partner"} accepted your invitation.", 
                        AlertCategory.Success);
                }
            }
            else
            {
                _context.SubmissionGroupMembers.Remove(invitation);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Notifications");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitReallocationRequest(int submissionId, string reason)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Challenge();

            var submission = await _context.ResearchSubmissions
                .FirstOrDefaultAsync(s => s.Id == submissionId && s.SubmitterId == user.Id && s.Status == SubmissionStatus.Linked);

            if (submission == null)
            {
                TempData["ErrorMessage"] = "Invalid project or currently unassigned.";
                return RedirectToAction(nameof(Dashboard));
            }

            // Check if there's already a pending request
            var existing = await _context.ReallocationRequests
                .AnyAsync(r => r.ResearchSubmissionId == submissionId && r.Status == ReallocationStatus.Pending);

            if (existing)
            {
                TempData["ErrorMessage"] = "You already have a pending reallocation request for this project.";
                return RedirectToAction(nameof(Dashboard));
            }

            var request = new ReallocationRequest
            {
                ResearchSubmissionId = submissionId,
                RequestedById = user.Id,
                Reason = reason,
                Status = ReallocationStatus.Pending,
                RequestedAt = DateTime.UtcNow
            };

            _context.ReallocationRequests.Add(request);
            await _context.SaveChangesAsync();

            // Notify Module Leader
            var modLeaders = await _userManager.GetUsersInRoleAsync("ModuleLeader");
            foreach (var leader in modLeaders)
            {
                await _notificationService.SendNotificationAsync(leader.Id, 
                    "New Reallocation Requested", 
                    $"Student {user.FullName} has requested a supervisor change for project: {submission.Title}", 
                    AlertCategory.System, 
                    Url.Action("Reallocations", "ModuleLeader"));
            }

            TempData["SuccessMessage"] = "Reallocation request submitted. The Module Leader will review your case shortly.";
            return RedirectToAction(nameof(Dashboard));
        }
    }
}
