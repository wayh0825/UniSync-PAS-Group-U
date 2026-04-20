using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniSync.Core.Data;
using UniSync.Core.Entities;
using UniSync.Web.ViewModels;
using UniSync.Web.Services;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using System.Linq;
using System.Collections.Generic;
using System;
using UniSync.Core.Enums;

namespace UniSync.Web.Controllers
{
    [Authorize(Roles = "ModuleLeader,Administrator")]
    public class ModuleLeaderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ISubmissionLinkingService _linkingService;
        private readonly IAuditLogService _auditService;

        public ModuleLeaderController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, ISubmissionLinkingService linkingService, IAuditLogService auditService)
        {
            _context = context;
            _userManager = userManager;
            _linkingService = linkingService;
            _auditService = auditService;
        }

        public async Task<IActionResult> Dashboard()
        {
            var submissions = await _context.ResearchSubmissions
                .Include(p => p.ExpertiseDomain)
                .Include(p => p.Submitter)
                .Include(p => p.LinkedSupervisor)
                .Where(p => p.Status != SubmissionStatus.Draft)
                .ToListAsync();

            var areas = await _context.ExpertiseDomains.ToListAsync();

            var totalStudents = await _userManager.GetUsersInRoleAsync("Student");
            var totalSupervisors = await _userManager.GetUsersInRoleAsync("Supervisor");

            // Submission intake over last 24h (6-hour windows)
            var nowTime = DateTime.Now;
            var intakeLabels = new List<string>();
            var intakeValues = new List<int>();
            var windowStart = nowTime.AddHours(-24);
            for (var i = 0; i < 4; i++)
            {
                var segmentStart = windowStart.AddHours(i * 6);
                var segmentEnd = segmentStart.AddHours(6);
                intakeLabels.Add(segmentStart.ToString("HH:mm"));
                intakeValues.Add(submissions.Count(p => p.SubmissionDate >= segmentStart && p.SubmissionDate < segmentEnd));
            }
            intakeLabels.Add(nowTime.ToString("HH:mm"));
            intakeValues.Add(submissions.Count(p => p.SubmissionDate >= nowTime.AddHours(-6) && p.SubmissionDate <= nowTime));

            // Submission Activity Trends (Last 6 Months)
            var months = Enumerable.Range(0, 6)
                .Select(i => nowTime.AddMonths(-i))
                .OrderBy(d => d)
                .ToList();

            // Phase 2: Expertise Saturation Heatmap Data
            var heatmapLabels = areas.Select(a => a.Name).ToList();
            var studentConcentration = new List<int>();
            var supervisorCapacity = new List<int>();

            foreach(var area in areas)
            {
                studentConcentration.Add(submissions.Count(s => s.ExpertiseDomainId == area.Id));
                supervisorCapacity.Add(await _context.SupervisorExpertiseDomains.CountAsync(sd => sd.ExpertiseDomainId == area.Id));
            }

            // Phase 2: Performance Latency Metrics
            var reviewedSubmissions = submissions.Where(s => s.FeedbackDate.HasValue).ToList();
            double avgLatencyDays = reviewedSubmissions.Any() 
                ? reviewedSubmissions.Average(s => (s.FeedbackDate.Value - s.SubmissionDate).TotalDays)
                : 0;

            var viewModel = new AdminDashboardViewModel
            {
                TotalPlatformUsers = totalStudents.Count + totalSupervisors.Count,
                TotalStudents = totalStudents.Count,
                ActiveSubmissions = submissions.Count(p => p.Status == SubmissionStatus.Pending || p.Status == SubmissionStatus.UnderReview),
                SuccessfulLinks = submissions.Count(p => p.Status == SubmissionStatus.Linked),
                SubmissionLoadPercent = submissions.Any() ? (int)Math.Round((double)submissions.Count(p => p.Status != SubmissionStatus.Linked) / submissions.Count * 100) : 0,
                LinkRatePercent = submissions.Any() ? (int)Math.Round((double)submissions.Count(p => p.Status == SubmissionStatus.Linked) / submissions.Count * 100) : 0,
                RecentSubmissions = submissions.OrderByDescending(p => p.SubmissionDate).Take(5).ToList(),
                ExpertiseDomains = areas,
                ApiResponseLabels = intakeLabels,
                ApiResponseValues = intakeValues,
                ApiAveragePerWindow = intakeValues.Count > 0 ? Math.Round(intakeValues.Average(), 1) : 0,
                ChartLabels = months.Select(m => m.ToString("MMM").ToUpper()).ToList(),
                ChartValues = months.Select(m => submissions.Count(p => p.SubmissionDate.Month == m.Month && p.SubmissionDate.Year == m.Year)).ToList()
            };

            ViewBag.HeatmapLabels = heatmapLabels;
            ViewBag.StudentConcentration = studentConcentration;
            ViewBag.SupervisorCapacity = supervisorCapacity;
            ViewBag.AvgLatency = Math.Round(avgLatencyDays, 1);

            return View(viewModel);
        }

        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfile(string fullName)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(fullName))
            {
                user.FullName = fullName;
                await _userManager.UpdateAsync(user);
                await _auditService.LogAsync("Security", user.Email!, "Updated profile information");
                TempData["SuccessMessage"] = "Profile synchronized successfully.";
            }

            return RedirectToAction(nameof(Profile));
        }

        public async Task<IActionResult> ViewAccounts(string? search, string? roleFilter, string? statusFilter, int page = 1)
        {
            const int pageSize = 10;
            var query = _userManager.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(u =>
                    (u.FullName != null && u.FullName.Contains(search)) ||
                    (u.Email != null && u.Email.Contains(search)));

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                bool isActive = (statusFilter == "Active");
                query = query.Where(u => u.EmailConfirmed == isActive);
            }

            var allEligibleUsers = await query.ToListAsync();
            var allUserRoles = new List<(ApplicationUser User, string Role)>();

            foreach (var u in allEligibleUsers)
            {
                var roles = await _userManager.GetRolesAsync(u);
                string primaryRole = roles.FirstOrDefault() ?? "N/A";

                if (!string.IsNullOrWhiteSpace(roleFilter) && primaryRole != roleFilter)
                    continue;

                // Module Leaders ONLY see Students and Supervisors
                if (!(primaryRole == "Student" || primaryRole == "Supervisor"))
                    continue;

                allUserRoles.Add((u, primaryRole));
            }

            int totalItems = allUserRoles.Count;
            var paginatedUsers = allUserRoles
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.ActiveStudents = allUserRoles.Count(ur => ur.Role == "Student" && ur.User.EmailConfirmed);
            ViewBag.Supervisors = allUserRoles.Count(ur => ur.Role == "Supervisor" && ur.User.EmailConfirmed);
            ViewBag.SearchTerm = search;
            ViewBag.RoleFilter = roleFilter;
            ViewBag.StatusFilter = statusFilter;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            return View(paginatedUsers);
        }

        public async Task<IActionResult> AllocationOversight()
        {
            var submissions = await _context.ResearchSubmissions
                .Include(p => p.Submitter)
                .Include(p => p.LinkedSupervisor)
                .Include(p => p.ExpertiseDomain)
                .ToListAsync();

            ViewBag.TotalProjects = submissions.Count;
            ViewBag.LinkedProjects = submissions.Count(p => p.Status == SubmissionStatus.Linked);
            ViewBag.PendingProjects = submissions.Count(p => p.Status == SubmissionStatus.Pending || p.Status == SubmissionStatus.UnderReview);
            ViewBag.FinalDeadline = "May 20, 2026"; 

            return View(submissions);
        }

        public async Task<IActionResult> ManageSubmissionEntry(int id)
        {
            var submission = await _context.ResearchSubmissions
                .Include(p => p.Submitter)
                .Include(p => p.ExpertiseDomain)
                .Include(p => p.LinkedSupervisor)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (submission == null) return NotFound();

            var supervisors = await _userManager.GetUsersInRoleAsync("Supervisor");
            ViewBag.AvailableSupervisors = supervisors;

            return View(submission);
        }

        public async Task<IActionResult> ExpertiseDomains()
        {
            var areas = await _context.ExpertiseDomains.ToListAsync();
            
            // Replicate some viewbag data that research areas view might expect
            var frequencies = new Dictionary<int, int>();
            var areaCounts = new Dictionary<int, int>();
            
            foreach(var area in areas)
            {
                int count = await _context.ResearchSubmissions.CountAsync(s => s.ExpertiseDomainId == area.Id);
                areaCounts[area.Id] = count;
                frequencies[area.Id] = areas.Count > 0 ? (int)Math.Round((double)count / Math.Max(1, await _context.ResearchSubmissions.CountAsync()) * 100) : 0;
            }
            
            ViewBag.Frequencies = frequencies;
            ViewBag.AreaCounts = areaCounts;
            ViewBag.TrendingName = areas.Any() ? areas.First().Name : "N/A";
            ViewBag.TrendingCount = areaCounts.Values.Any() ? areaCounts.Values.Max() : 0;
            ViewBag.LowCoverageName = areas.Count > 1 ? areas.Last().Name : "N/A";
            ViewBag.LowCoverageCount = areaCounts.Values.Any() ? areaCounts.Values.Min() : 0;

            return View(areas);
        }

        [HttpPost]
        public async Task<IActionResult> ForceLink(int id, string supervisorId)
        {
            var submission = await _context.ResearchSubmissions.FindAsync(id);
            if (submission == null) return NotFound();

            submission.LinkedSupervisorId = supervisorId;
            submission.Status = SubmissionStatus.Linked;
            await _context.SaveChangesAsync();
            await _auditService.LogAsync("Allocation", User.Identity?.Name ?? "System", $"Manually linked submission {id} to supervisor {supervisorId}");
            return RedirectToAction(nameof(ManageSubmissionEntry), new { id = submission.Id });
        }

        [HttpPost]
        public async Task<IActionResult> BreakLink(int id)
        {
            var submission = await _context.ResearchSubmissions.FindAsync(id);
            if (submission == null) return NotFound();

            submission.LinkedSupervisorId = null;
            submission.Status = SubmissionStatus.Pending;
            await _context.SaveChangesAsync();
            await _auditService.LogAsync("Allocation", User.Identity?.Name ?? "System", $"Broke link for submission {id}");
            return RedirectToAction(nameof(ManageSubmissionEntry), new { id = submission.Id });
        }

        [HttpPost]
        public async Task<IActionResult> AddExpertiseDomain(string AreaName)
        {
            if (!string.IsNullOrWhiteSpace(AreaName))
            {
                var area = new ExpertiseDomain { Name = AreaName };
                _context.ExpertiseDomains.Add(area);
                await _context.SaveChangesAsync();
                await _auditService.LogAsync("ExpertiseDomain", User.Identity?.Name ?? "System", $"Created new domain: {AreaName}");
            }
            return RedirectToAction(nameof(ExpertiseDomains));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteExpertiseDomain(int id)
        {
            var area = await _context.ExpertiseDomains.FindAsync(id);
            if (area != null)
            {
                _context.ExpertiseDomains.Remove(area);
                await _context.SaveChangesAsync();
                await _auditService.LogAsync("ExpertiseDomain", User.Identity?.Name ?? "System", $"Deleted domain: {area.Name}");
            }
            return RedirectToAction(nameof(ExpertiseDomains));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkAssignPending()
        {
            var pendingSubmissions = await _context.ResearchSubmissions
                .Include(s => s.ExpertiseDomain)
                .Where(s => s.Status == SubmissionStatus.Pending)
                .ToListAsync();
                
            var supervisorUsers = await _userManager.GetUsersInRoleAsync("Supervisor");
            
            if (!supervisorUsers.Any() || !pendingSubmissions.Any())
            {
                TempData["Error"] = "No pending submissions or available supervisors found.";
                return RedirectToAction(nameof(AllocationOversight));
            }
            
            var workloads = new Dictionary<string, int>();
            var supervisorAreas = new Dictionary<string, List<int>>();

            foreach(var sup in supervisorUsers) 
            {
                workloads[sup.Id] = await _context.ResearchSubmissions.CountAsync(s => s.LinkedSupervisorId == sup.Id && s.Status == SubmissionStatus.Linked);
                supervisorAreas[sup.Id] = await _context.SupervisorExpertiseDomains.Where(a => a.SupervisorId == sup.Id).Select(a => a.ExpertiseDomainId).ToListAsync();
            }
            
            int assignedCount = 0;

            foreach(var submission in pendingSubmissions)
            {
                var availableSups = supervisorUsers.Where(s => workloads[s.Id] < s.MaxSupervisionCapacity).ToList();
                    
                if (!availableSups.Any()) break;

                // Enhanced Phase 2 AI Logic: Match scoring
                ApplicationUser? matchingSup = null;
                double maxScore = 0;

                foreach(var sup in availableSups)
                {
                    double currentScore = 0;
                    
                    // 1. Expertise Match (60% weight)
                    if (supervisorAreas[sup.Id].Contains(submission.ExpertiseDomainId))
                        currentScore += 60;
                    
                    // 2. Capacity Match (40% weight - prefers those with more free slots)
                    double loadFactor = (double)(sup.MaxSupervisionCapacity - workloads[sup.Id]) / sup.MaxSupervisionCapacity;
                    currentScore += (loadFactor * 40);

                    if (currentScore > maxScore)
                    {
                        maxScore = currentScore;
                        matchingSup = sup;
                    }
                }
                
                if (matchingSup != null)
                {
                    submission.LinkedSupervisorId = matchingSup.Id;
                    submission.Status = SubmissionStatus.Linked;
                    submission.MatchScore = Math.Round(maxScore, 1);
                    workloads[matchingSup.Id]++;
                    assignedCount++;
                }
            }
            
            await _context.SaveChangesAsync();
            await _auditService.LogAsync("Allocation", User.Identity?.Name ?? "System", $"Executed Bulk Assignment. Assigned: {assignedCount}");
            
            TempData["SuccessMessage"] = $"Bulk assignment complete. {assignedCount} projects allocated.";
            return RedirectToAction(nameof(AllocationOversight));
        }

        [HttpGet]
        public async Task<IActionResult> ExportAllocations()
        {
            var allocations = await _context.ResearchSubmissions
                .Include(p => p.Submitter)
                .Include(p => p.LinkedSupervisor)
                .Include(p => p.ExpertiseDomain)
                .ToListAsync();

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("ID,Title,Student,Supervisor,Domain,Status");

            foreach (var item in allocations)
            {
                csv.AppendLine($"{item.Id},{item.Title?.Replace(",", " ")},{item.Submitter?.FullName},{item.LinkedSupervisor?.FullName ?? "N/A"},{item.ExpertiseDomain?.Name},{item.Status}");
            }

            return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"Allocations_{DateTime.Now:yyyyMMdd}.csv");
        }
        [HttpGet]
        public async Task<IActionResult> Settings()
        {
            var deadline = await _context.GlobalSettings.FirstOrDefaultAsync(s => s.Key == "Academic_SubmissionDeadline");
            var academicYear = await _context.GlobalSettings.FirstOrDefaultAsync(s => s.Key == "Academic_YearName");

            ViewBag.DeadlineValue = deadline?.Value ?? "2026-05-20";
            ViewBag.YearNameValue = academicYear?.Value ?? "2025/26 - Semester 2";

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UpdateSettings(string deadline, string yearName)
        {
            var deadlineSetting = await _context.GlobalSettings.FirstOrDefaultAsync(s => s.Key == "Academic_SubmissionDeadline") 
                                  ?? new GlobalSetting { Key = "Academic_SubmissionDeadline", Category = "Academic" };
            
            var yearSetting = await _context.GlobalSettings.FirstOrDefaultAsync(s => s.Key == "Academic_YearName")
                              ?? new GlobalSetting { Key = "Academic_YearName", Category = "Academic" };

            deadlineSetting.Value = deadline;
            deadlineSetting.LastUpdatedAt = DateTime.UtcNow;
            deadlineSetting.LastUpdatedBy = User.Identity?.Name;

            yearSetting.Value = yearName;
            yearSetting.LastUpdatedAt = DateTime.UtcNow;
            yearSetting.LastUpdatedBy = User.Identity?.Name;

            if (_context.Entry(deadlineSetting).State == EntityState.Detached) _context.GlobalSettings.Add(deadlineSetting);
            if (_context.Entry(yearSetting).State == EntityState.Detached) _context.GlobalSettings.Add(yearSetting);

            await _context.SaveChangesAsync();
            await _auditService.LogAsync("Academic", User.Identity?.Name ?? "System", $"Updated academic settings: Deadline={deadline}, Year={yearName}");

            TempData["SuccessMessage"] = "Academic configurations updated successfully.";
            return RedirectToAction(nameof(Settings));
        }

        [HttpGet]
        public IActionResult Broadcast()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SendBroadcast(string targetRole, string title, string message)
        {
            IList<ApplicationUser> recipients;
            if (targetRole == "All")
            {
                var students = await _userManager.GetUsersInRoleAsync("Student");
                var supervisors = await _userManager.GetUsersInRoleAsync("Supervisor");
                recipients = students.Concat(supervisors).ToList();
            }
            else
            {
                recipients = await _userManager.GetUsersInRoleAsync(targetRole);
            }

            foreach (var user in recipients)
            {
                var notification = new AppNotification
                {
                    RecipientId = user.Id,
                    SenderId = (await _userManager.GetUserAsync(User))?.Id ?? "",
                    Title = title,
                    Message = message,
                    Type = AlertCategory.Broadcast,
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                };
                _context.AppNotifications.Add(notification);
            }

            await _context.SaveChangesAsync();
            await _auditService.LogAsync("Broadcast", User.Identity?.Name ?? "System", $"Sent mass broadcast to {targetRole}: {title}");

            TempData["SuccessMessage"] = $"Broadcast dispatched successfully to {recipients.Count} nodes.";
            return RedirectToAction(nameof(Broadcast));
        }

        [HttpGet]
        public async Task<IActionResult> Reallocations()
        {
            var requests = await _context.ReallocationRequests
                .Include(r => r.Submission)
                .Include(r => r.RequestedBy)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();

            return View(requests);
        }

        [HttpPost]
        public async Task<IActionResult> ProcessReallocation(int id, bool approve, string note)
        {
            var request = await _context.ReallocationRequests
                .Include(r => r.Submission)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null) return NotFound();

            request.Status = approve ? ReallocationStatus.Approved : ReallocationStatus.Rejected;
            request.ResponseNote = note;
            request.RespondedAt = DateTime.UtcNow;

            if (approve)
            {
                // Break link so it goes back to pending/manual allocation
                request.Submission.LinkedSupervisorId = null;
                request.Submission.Status = SubmissionStatus.Pending;
                request.Submission.MatchScore = null;
            }

            await _context.SaveChangesAsync();
            
            // Notify Student
            var notificationService = HttpContext.RequestServices.GetRequiredService<INotificationService>();
            await notificationService.SendNotificationAsync(request.RequestedById, 
                $"Reallocation {request.Status}", 
                $"Your request for supervisor reallocation has been {request.Status.ToString().ToLower()}. Note: {note}", 
                approve ? AlertCategory.Success : AlertCategory.System, 
                Url.Action("Dashboard", "Student"));

            await _auditService.LogAsync("Governance", User.Identity?.Name ?? "System", $"Processed reallocation request {id}. Decision: {request.Status}");

            TempData["SuccessMessage"] = $"Reallocation {request.Status} successfully.";
            return RedirectToAction(nameof(Reallocations));
        }
    }
}
