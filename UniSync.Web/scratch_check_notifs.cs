
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using UniSync.Core.Data;
using UniSync.Core.Entities;
using Microsoft.Extensions.DependencyInjection;

public class Scratch
{
    public static void CheckNotifications(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var notifs = db.AppNotifications.OrderByDescending(n => n.CreatedAt).Take(5).ToList();
        
        foreach (var n in notifs)
        {
            Console.WriteLine($"ID: {n.Id}, Msg: {n.Message}, URL: {n.ActionUrl}, IsLocal: {!string.IsNullOrEmpty(n.ActionUrl) && n.ActionUrl.StartsWith("/")}");
        }
    }
}
