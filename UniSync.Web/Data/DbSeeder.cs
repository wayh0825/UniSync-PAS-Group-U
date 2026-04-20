using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using UniSync.Core.Data;
using UniSync.Core.Entities;
using UniSync.Core.Enums;

namespace UniSync.Web.Data
{
    public static class DbSeeder
    {
        public static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();

            // 0. Database Schema Migration
            await MigrateSchemaAsync(dbContext);

            // 1. Seed Roles
            string[] roleNames = {
                Roles.Student.ToString(),
                Roles.Supervisor.ToString(),
                Roles.ModuleLeader.ToString(),
                Roles.Administrator.ToString()
            };

            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // 2. Seed System Administrator (1)
            await SeedUser(userManager, "admin@unisync.edu", "Admin Gamage", Roles.Administrator.ToString(), "P@ssw0rd");

            // 3. Seed Module Leaders (Actual names from PUSL2020 Brief)
            var moduleLeaders = new[]
            {
                ("Ms. Pavithra Subhashini", "pavithras@nsbm.ac.lk"),
                ("Mr. Anton Jayakody", "anton.j@nsbm.ac.lk")
            };
            foreach (var (name, email) in moduleLeaders)
                await SeedUser(userManager, email, name, Roles.ModuleLeader.ToString(), "P@ssw0rd");

            // 4. Seed Supervisors (15)
            var supervisors = new[]
            {
                ("Dr. Tharindu Silva", "tharindu.s@unisync.edu"),
                ("Dr. Samanthi Perera", "samanthi.p@unisync.edu"),
                ("Prof. Kasun Rajitha", "kasun.r@unisync.edu"),
                ("Dr. Roshan Abeysinghe", "roshan.a@unisync.edu"),
                ("Dr. Angelo Mathews", "angelo.m@unisync.edu"),
                ("Prof. Maheesh Theekshana", "maheesh.t@unisync.edu"),
                ("Dr. Dhananjaya de Silva", "dhananjaya.d@unisync.edu"),
                ("Dr. Dinusha Weeraratne", "dinusha.w@unisync.edu"),
                ("Prof. Bandula Gunawardena", "bandula.g@unisync.edu"),
                ("Dr. Nalaka Godahewa", "nalaka.g@unisync.edu"),
                ("Prof. Sarath Amunugama", "sarath.a@unisync.edu"),
                ("Dr. Harini Amarasuriya", "harini.a@unisync.edu"),
                ("Prof. Tissa Vitarana", "tissa.v@unisync.edu"),
                ("Dr. Ramesh Pathirana", "ramesh.p@unisync.edu"),
                ("Prof. G.L. Peiris", "gl.peiris@unisync.edu")
            };
            foreach (var (name, email) in supervisors)
                await SeedUser(userManager, email, name, Roles.Supervisor.ToString(), "P@ssw0rd", "Senior Academic with extensive experience in research methodology and implementation.");

            // 5. Seed Students (25)
            var studentPool = new[]
            {
                ("Nuwan Perera", "nuwan.p@unisync.edu"),
                ("Kavindi Jayasinghe", "kavindi.j@unisync.edu"),
                ("Dulaj Munasinghe", "dulaj.m@unisync.edu"),
                ("Pathum Nissanka", "pathum.n@unisync.edu"),
                ("Hiruni Perera", "hiruni.p@unisync.edu"),
                ("Ishara Madushanka", "ishara.m@unisync.edu"),
                ("Nadeesha Dilhani", "nadeesha.d@unisync.edu"),
                ("Minoli Fernandopulle", "minoli.f@unisync.edu"),
                ("Dimuth Karunaratne", "dimuth.k@unisync.edu"),
                ("Kusal Perera", "kusal.p@unisync.edu"),
                ("Wanindu Hasaranga", "wanindu.h@unisync.edu"),
                ("Chamika Karunaratne", "chamika.k@unisync.edu"),
                ("Sehara Senanayake", "sehara.s@unisync.edu"),
                ("Binura Fernando", "binura.f@unisync.edu"),
                ("Gayashan Wickramasinghe", "gayashan.w@unisync.edu"),
                ("Kasun Rajitha", "kasun.student@unisync.edu"),
                ("Lasith Malinga", "lasith.m@unisync.edu"),
                ("Mahela Jayawardene", "mahela.j@unisync.edu"),
                ("Kumar Sangakkara", "kumar.s@unisync.edu"),
                ("Charith Asalanka", "charith.a@unisync.edu"),
                ("Kusal Mendis", "kusal.m@unisync.edu"),
                ("Roshen Silva", "roshen.s@unisync.edu"),
                ("Vishwa Fernando", "vishwa.f@unisync.edu"),
                ("Asitha Fernando", "asitha.f@unisync.edu"),
                ("Praveen Jayawickrama", "praveen.j@unisync.edu")
            };
            foreach (var (name, email) in studentPool)
                await SeedUser(userManager, email, name, Roles.Student.ToString(), "P@ssw0rd");

            // 6. Data Matrix
            await SeedExpertiseDomainsAsync(dbContext);
            await SeedSupervisorExpertiseAsync(dbContext, userManager);
            await SeedBulkSubmissionsAsync(dbContext, userManager);
        }

        private static async Task SeedExpertiseDomainsAsync(ApplicationDbContext dbContext)
        {
            if (await dbContext.ExpertiseDomains.AnyAsync()) return;
            var areas = new[] { "Artificial Intelligence", "Data Science", "Cybersecurity", "Software Engineering", "Cloud Computing", "IoT", "Mobile Development", "Blockchain", "Digital Forensics", "UX/UI Design" };
            foreach (var area in areas) dbContext.ExpertiseDomains.Add(new ExpertiseDomain { Name = area });
            await dbContext.SaveChangesAsync();
        }

        private static async Task SeedSupervisorExpertiseAsync(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
        {
            if (await dbContext.SupervisorExpertiseDomains.AnyAsync()) return;

            var supervisors = await userManager.GetUsersInRoleAsync(Roles.Supervisor.ToString());
            var domains = await dbContext.ExpertiseDomains.ToListAsync();
            var random = new Random();

            foreach (var supervisor in supervisors)
            {
                var count = random.Next(2, 4);
                var shuffledDomains = domains.OrderBy(x => random.Next()).Take(count).ToList();
                foreach (var domain in shuffledDomains)
                {
                    dbContext.SupervisorExpertiseDomains.Add(new SupervisorExpertiseDomain
                    {
                        SupervisorId = supervisor.Id,
                        ExpertiseDomainId = domain.Id
                    });
                }
            }
            await dbContext.SaveChangesAsync();
        }

        private static async Task SeedBulkSubmissionsAsync(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
        {
            if (await dbContext.ResearchSubmissions.AnyAsync()) return;

            var students = await userManager.GetUsersInRoleAsync(Roles.Student.ToString());
            var domains = await dbContext.ExpertiseDomains.ToListAsync();
            var random = new Random();

            var projectTitles = new[] {
                "AI-Driven Smart Agriculture for Paddy Fields", "Secure E-Channelling System Architecture", "IoT Based Elephant Intrusion Detection", "Blockchain for Tea Supply Chain", "Traffic Management using Deep Learning",
                "Mobile Health Solution for Rural Communities", "Cyber Threat Intelligence in SL Banking", "Energy Optimized Smart Homes", "Predictive Analytics for GCE A/L Results", "Indigenous Medicine Digitization Hub"
            };

            for (int i = 0; i < 50; i++)
            {
                var mainStudent = students[i % students.Count];
                var domain = domains[random.Next(domains.Count)];
                var isGroup = random.NextDouble() > 0.6;
                
                var submission = new ResearchSubmission
                {
                    Title = $"{projectTitles[i % projectTitles.Length]} - Protocol {i + 1}",
                    ExpertiseDomainId = domain.Id,
                    ExecutiveSummary = "This project focuses on implementing localized technological solutions for Sri Lankan community challenges using modern software frameworks.",
                    ProblemStatement = "Identified critical gap in current infrastructure leading to operational inefficiencies.",
                    Objectives = "1. Feasibility Study, 2. Prototype Development, 3. Field Testing.",
                    Methodology = "Requirement gathering followed by iterative implementation using Agile principles.",
                    ExpectedOutcomes = "A fully functional software artifact ready for deployment.",
                    TechStack = "ASP.NET Core, React, SQL Server",
                    Keywords = "sri lanka, innovation, technology",
                    TimelineWeeks = 16 + random.Next(8),
                    SubmitterId = mainStudent.Id,
                    Status = SubmissionStatus.Pending,
                    SubmissionDate = DateTime.UtcNow.AddDays(-random.Next(30)),
                    IsGroupProject = isGroup
                };

                dbContext.ResearchSubmissions.Add(submission);
                await dbContext.SaveChangesAsync();

                if (isGroup)
                {
                    var memberCount = random.Next(1, 3);
                    var shuffledStudents = students.Where(s => s.Id != mainStudent.Id).OrderBy(x => random.Next()).ToList();
                    
                    for (int j = 0; j < memberCount; j++)
                    {
                        var member = shuffledStudents[j];
                        dbContext.SubmissionGroupMembers.Add(new SubmissionGroupMember
                        {
                            ResearchSubmissionId = submission.Id,
                            UserId = member.Id,
                            FullName = member.FullName,
                            Email = member.Email,
                            Status = InvitationStatus.Accepted,
                            StudentIdIdentifier = "ST-" + random.Next(1000, 9999)
                        });
                    }
                }

                dbContext.AppNotifications.Add(new AppNotification
                {
                    RecipientId = mainStudent.Id,
                    Type = AlertCategory.Submission,
                    Title = "Submission Protocol Initiated",
                    Message = $"Your research proposal '{submission.Title}' has been successfully indexed in the hub.",
                    ActionUrl = $"/Student/SubmissionDetails/{submission.Id}",
                    CreatedAt = submission.SubmissionDate.AddMinutes(5),
                    IsRead = random.NextDouble() > 0.5
                });
            }

            await dbContext.SaveChangesAsync();
        }

        private static async Task MigrateSchemaAsync(ApplicationDbContext context)
        {
            try {
                await context.Database.ExecuteSqlRawAsync(@"
                    IF EXISTS (SELECT * FROM sys.tables WHERE name = 'MentorExpertiseDomains')
                    EXEC sp_rename 'MentorExpertiseDomains', 'SupervisorExpertiseDomains';
                    
                    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ResearchSubmissions') AND name = 'LinkedMentorId')
                    EXEC sp_rename 'ResearchSubmissions.LinkedMentorId', 'LinkedSupervisorId', 'COLUMN';
                ");
            } catch { }
        }

        private static async Task SeedUser(UserManager<ApplicationUser> userManager, string email, string fullName, string role, string password, string? biography = null)
        {
            if (await userManager.FindByEmailAsync(email) != null) return;
            var user = new ApplicationUser { UserName = email, Email = email, FullName = fullName, EmailConfirmed = true, MaxSupervisionCapacity = 30, Biography = biography };
            var result = await userManager.CreateAsync(user, password);
            if (result.Succeeded) await userManager.AddToRoleAsync(user, role);
        }
    }
}
