using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UniSync.Core.Data;
using UniSync.Core.Entities;

namespace UniSync.Web.Services
{
    public interface ISubmissionLinkingService
    {
        Task<int> CalculateLinkScoreAsync(ResearchSubmission submission, ApplicationUser supervisor);
        Task<List<(ResearchSubmission Entry, int LinkScore)>> GetRankedSubmissionsForSupervisorAsync(string supervisorId);
    }

    public class SubmissionLinkingService : ISubmissionLinkingService
    {
        private readonly ApplicationDbContext _context;

        public SubmissionLinkingService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Calculates a link score (0-100) between a submission and a supervisor.
        /// Score components:
        /// - Research Area Link: 40%
        /// - Tech Stack Overlap: 30%
        /// - Keywords Similarity: 20%
        /// - Supervisor Workload Balance: 10%
        /// </summary>
        public async Task<int> CalculateLinkScoreAsync(ResearchSubmission submission, ApplicationUser supervisor)
        {
            if (submission == null || supervisor == null)
                return 0;

            int totalScore = 0;

            // Component 1: Research Area Link (40%)
            var supervisorAreaIds = await _context.SupervisorExpertiseDomains
                .Where(x => x.SupervisorId == supervisor.Id)
                .Select(x => x.ExpertiseDomainId)
                .ToListAsync();

            int areaScore = supervisorAreaIds.Contains(submission.ExpertiseDomainId) ? 40 : 0;
            totalScore += areaScore;

            // Component 2: Tech Stack Overlap (30%)
            int techScore = CalculateTechStackOverlap(submission.TechStack);
            totalScore += techScore;

            // Component 3: Keywords Similarity (20%)
            int keywordScore = CalculateKeywordSimilarity(submission.Keywords);
            totalScore += keywordScore;

            // Component 4: Workload Balance (10%)
            int workloadScore = await CalculateWorkloadBalanceAsync(supervisor.Id);
            totalScore += workloadScore;

            return Math.Min(totalScore, 100);
        }

        public async Task<List<(ResearchSubmission Entry, int LinkScore)>> GetRankedSubmissionsForSupervisorAsync(string supervisorId)
        {
            var supervisor = await _context.Users
                .Include(u => u.SupervisorAreas)
                .FirstOrDefaultAsync(u => u.Id == supervisorId);

            if (supervisor == null)
                return new List<(ResearchSubmission, int)>();

            var selectedAreaIds = supervisor.SupervisorAreas
                .Select(a => a.ExpertiseDomainId)
                .ToList();

            var submissions = await _context.ResearchSubmissions
                .Include(p => p.ExpertiseDomain)
                .Include(p => p.LinkRequests)
                .Where(p => p.Status == UniSync.Core.Enums.SubmissionStatus.Pending &&
                           (!selectedAreaIds.Any() || selectedAreaIds.Contains(p.ExpertiseDomainId)))
                .ToListAsync();

            var rankedSubmissions = new List<(ResearchSubmission, int)>();

            foreach (var submission in submissions)
            {
                var score = await CalculateLinkScoreAsync(submission, supervisor);
                rankedSubmissions.Add((submission, score));
            }

            // Sort by score descending, then by submission date descending
            return rankedSubmissions
                .OrderByDescending(x => x.Item2)
                .ThenByDescending(x => x.Item1.SubmissionDate)
                .ToList();
        }

        private int CalculateTechStackOverlap(string? submissionTechStack)
        {
            // Base score for having a tech stack
            if (string.IsNullOrWhiteSpace(submissionTechStack))
                return 0;

            // Simple: Award points based on tech stack length (indicates complexity)
            // More comprehensive tech stacks = better linking opportunity
            var techCount = submissionTechStack.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;

            if (techCount >= 5) return 30;      // Full points for comprehensive tech stack
            if (techCount >= 3) return 20;      // 20 points for moderate complexity
            return 10;                           // 10 points for simple tech stack
        }

        private int CalculateKeywordSimilarity(string? keywords)
        {
            // Keywords indicate research clarity and focus
            if (string.IsNullOrWhiteSpace(keywords))
                return 0;

            var keywordCount = keywords.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Length;

            if (keywordCount >= 5) return 20;
            if (keywordCount >= 3) return 12;
            return 5;
        }

        private async Task<int> CalculateWorkloadBalanceAsync(string supervisorId)
        {
            // Count current linked projects
            var currentWorkload = await _context.ResearchSubmissions
                .CountAsync(p => p.LinkedSupervisorId == supervisorId &&
                                 p.Status == UniSync.Core.Enums.SubmissionStatus.Linked);

            // Award more points to supervisors with lighter workload
            // This encourages fair distribution
            if (currentWorkload == 0) return 10;      // Full points for zero projects
            if (currentWorkload <= 2) return 8;       // 8 points for 1-2 projects
            if (currentWorkload <= 4) return 5;       // 5 points for 3-4 projects
            if (currentWorkload <= 6) return 3;       // 3 points for 5-6 projects
            return 1;                                  // 1 point for 7+ projects
        }
    }
}
